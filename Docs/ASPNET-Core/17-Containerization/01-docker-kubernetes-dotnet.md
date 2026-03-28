# Docker & Kubernetes สำหรับ .NET

> Package app เป็น container แล้ว deploy ด้วย Kubernetes

---

## 1. Dockerfile สำหรับ .NET

```dockerfile
# Dockerfile — Multi-stage build (production-ready)

# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy csproj แยก → cache NuGet restore
COPY ["src/MyApi.Api/MyApi.Api.csproj", "src/MyApi.Api/"]
RUN dotnet restore "src/MyApi.Api/MyApi.Api.csproj"

# Copy source code ทั้งหมด
COPY . .

# Build
RUN dotnet publish "src/MyApi.Api/MyApi.Api.csproj" -c Release -o /app/publish

# Stage 2: Runtime (image เล็กกว่ามาก!)
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

# ไม่รัน root (security!)
RUN adduser --disabled-password --gecos "" appuser
USER appuser

COPY --from=build /app/publish .

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "MyApi.Api.dll"]
```

```bash
# Build
docker build -t myapi:latest .

# Run
docker run -d -p 8080:8080 \
  -e ConnectionStrings__DefaultConnection="Server=host.docker.internal;..." \
  -e Jwt__Key="your-secret-key" \
  --name myapi \
  myapi:latest

# docker-compose (API + DB + Redis)
```

```yaml
# docker-compose.yml
services:
  api:
    build: .
    ports:
      - "8080:8080"
    environment:
      - ConnectionStrings__DefaultConnection=Server=db;Database=MyApp;User Id=sa;Password=Pass123!;TrustServerCertificate=true;
      - ConnectionStrings__Redis=redis:6379
    depends_on:
      - db
      - redis

  db:
    image: mcr.microsoft.com/mssql/server:2022-latest
    environment:
      - ACCEPT_EULA=Y
      - SA_PASSWORD=Pass123!
    ports:
      - "1433:1433"
    volumes:
      - sqldata:/var/opt/mssql

  redis:
    image: redis:7-alpine
    ports:
      - "6379:6379"

volumes:
  sqldata:
```

```bash
docker compose up -d        # start ทั้งหมด
docker compose logs -f api  # ดู logs
docker compose down         # stop ทั้งหมด
```

---

## 2. Kubernetes Basics สำหรับ .NET

```yaml
# k8s/deployment.yml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: myapi
  labels:
    app: myapi
spec:
  replicas: 3                    # รัน 3 instances!
  selector:
    matchLabels:
      app: myapi
  template:
    metadata:
      labels:
        app: myapi
    spec:
      containers:
        - name: myapi
          image: myorg/myapi:latest
          ports:
            - containerPort: 8080
          env:
            - name: ConnectionStrings__DefaultConnection
              valueFrom:
                secretKeyRef:
                  name: myapi-secrets
                  key: db-connection
          resources:
            requests:
              memory: "128Mi"
              cpu: "100m"
            limits:
              memory: "512Mi"
              cpu: "500m"
          readinessProbe:
            httpGet:
              path: /health
              port: 8080
            initialDelaySeconds: 5
            periodSeconds: 10
          livenessProbe:
            httpGet:
              path: /health
              port: 8080
            initialDelaySeconds: 15
            periodSeconds: 30
---
apiVersion: v1
kind: Service
metadata:
  name: myapi-service
spec:
  selector:
    app: myapi
  ports:
    - port: 80
      targetPort: 8080
  type: ClusterIP
---
apiVersion: networking.k8s.io/v1
kind: Ingress
metadata:
  name: myapi-ingress
  annotations:
    nginx.ingress.kubernetes.io/ssl-redirect: "true"
spec:
  rules:
    - host: api.mycompany.com
      http:
        paths:
          - path: /
            pathType: Prefix
            backend:
              service:
                name: myapi-service
                port:
                  number: 80
```

### Health Checks ใน .NET

```csharp
// Program.cs — Kubernetes ใช้ health checks ตรวจสอบ app
builder.Services.AddHealthChecks()
    .AddSqlServer(connectionString, name: "database")
    .AddRedis(redisConnectionString, name: "redis");

app.MapHealthChecks("/health");        // liveness
app.MapHealthChecks("/ready", new HealthCheckOptions   // readiness
{
    Predicate = check => check.Tags.Contains("ready")
});
```

```bash
# Deploy
kubectl apply -f k8s/
kubectl get pods                        # ดู pods
kubectl logs -f deployment/myapi        # ดู logs
kubectl scale deployment/myapi --replicas=5   # scale up
kubectl rollout restart deployment/myapi      # restart
```
