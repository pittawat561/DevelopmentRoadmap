# Containerization — Docker & Kubernetes

---

## 1. Container คืออะไร

Container คือ **กล่องที่บรรจุ application + dependencies ทั้งหมด** เพื่อให้รันได้ทุกที่เหมือนกัน

```
ปัญหา: "It works on my machine!" 🤷
- dev ใช้ Node 18, server ใช้ Node 16
- dev ใช้ Windows, server ใช้ Linux
- ลืมติดตั้ง library บน server

Container แก้ปัญหา:
- บรรจุทุกอย่างไว้ในกล่อง (Node, dependencies, config)
- รันที่ไหนก็ได้ ผลลัพธ์เหมือนกัน 100%

Container vs Virtual Machine:
┌─────────────────────┐    ┌─────────────────────┐
│   Virtual Machine   │    │     Container        │
│ ┌─────┐ ┌─────┐    │    │ ┌─────┐ ┌─────┐     │
│ │App 1│ │App 2│    │    │ │App 1│ │App 2│     │
│ ├─────┤ ├─────┤    │    │ ├─────┤ ├─────┤     │
│ │OS   │ │OS   │    │    │ │Bins │ │Bins │     │
│ │(GB) │ │(GB) │    │    │ │(MB) │ │(MB) │     │
│ └─────┘ └─────┘    │    │ └─────┘ └─────┘     │
│ ┌───────────────┐   │    │ ┌───────────────────┐│
│ │  Hypervisor   │   │    │ │  Container Engine  ││
│ └───────────────┘   │    │ │  (Docker)          ││
│ ┌───────────────┐   │    │ └───────────────────┘│
│ │   Host OS     │   │    │ ┌───────────────────┐│
│ └───────────────┘   │    │ │     Host OS        ││
└─────────────────────┘    │ └───────────────────┘│
                           └─────────────────────┘
   หนัก, ช้า, GB             เบา, เร็ว, MB
```

---

## 2. Docker — พื้นฐาน

### Dockerfile — สร้าง Image

```dockerfile
# Dockerfile สำหรับ Node.js
FROM node:20-alpine            # Base image (เล็ก, ปลอดภัย)

WORKDIR /app                   # กำหนด working directory

COPY package*.json ./          # Copy package.json ก่อน (ใช้ cache)
RUN npm ci --production        # ติดตั้ง dependencies

COPY . .                       # Copy source code

EXPOSE 3000                    # บอกว่า app ใช้ port 3000

CMD ["node", "server.js"]      # คำสั่งเริ่ม app
```

```dockerfile
# Dockerfile สำหรับ .NET
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY *.csproj .
RUN dotnet restore
COPY . .
RUN dotnet publish -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:8.0    # Runtime image (เล็กกว่า)
WORKDIR /app
COPY --from=build /app .
EXPOSE 8080
ENTRYPOINT ["dotnet", "MyApp.dll"]
```

### Docker Commands

```bash
# Build image
docker build -t myapp:1.0 .

# Run container
docker run -d -p 3000:3000 --name myapp myapp:1.0
# -d         = background
# -p 3000:3000 = map port host:container
# --name     = ตั้งชื่อ

# ดู containers ที่รันอยู่
docker ps
docker ps -a                   # รวมที่หยุดแล้ว

# ดู logs
docker logs myapp
docker logs -f myapp           # follow (real-time)

# เข้าไปใน container
docker exec -it myapp sh

# หยุด / ลบ
docker stop myapp
docker rm myapp

# ดู images
docker images
docker rmi myapp:1.0           # ลบ image
```

### Docker Compose — รันหลาย containers

```yaml
# docker-compose.yml
version: '3.8'

services:
  app:
    build: .                      # Build จาก Dockerfile
    ports:
      - "3000:3000"
    environment:
      - DATABASE_URL=postgresql://user:pass@db:5432/myapp
      - REDIS_URL=redis://cache:6379
    depends_on:
      - db
      - cache

  db:
    image: postgres:16
    environment:
      POSTGRES_DB: myapp
      POSTGRES_USER: user
      POSTGRES_PASSWORD: pass
    volumes:
      - postgres_data:/var/lib/postgresql/data    # เก็บข้อมูลถาวร
    ports:
      - "5432:5432"

  cache:
    image: redis:7-alpine
    ports:
      - "6379:6379"

volumes:
  postgres_data:                  # Named volume

# คำสั่ง:
# docker compose up -d           ← start ทั้งหมด
# docker compose down            ← stop ทั้งหมด
# docker compose logs -f app     ← ดู logs
# docker compose build           ← rebuild images
```

---

## 3. Kubernetes (K8s) — ภาพรวม

Kubernetes คือ **container orchestration** — จัดการ containers จำนวนมากอัตโนมัติ

```
Docker: รัน 1 container
Kubernetes: จัดการ 100+ containers

K8s ทำอะไร:
├── Auto-scaling     — เพิ่ม/ลด containers ตาม traffic
├── Self-healing     — container ตาย → สร้างใหม่อัตโนมัติ
├── Load Balancing   — กระจาย traffic
├── Rolling Updates  — อัปเดตโดยไม่ downtime
├── Service Discovery — containers หากันเจอ
└── Secret Management — จัดการ passwords/keys

K8s Architecture:
┌──────────── Cluster ─────────────┐
│ ┌─────────────────────────────┐  │
│ │ Control Plane (Master)      │  │
│ │ - API Server                │  │
│ │ - Scheduler                 │  │
│ │ - Controller Manager        │  │
│ └─────────────────────────────┘  │
│                                  │
│ ┌─── Node 1 ──┐ ┌─── Node 2 ──┐│
│ │ ┌─Pod──┐    │ │ ┌─Pod──┐    ││
│ │ │ App  │    │ │ │ App  │    ││
│ │ └──────┘    │ │ └──────┘    ││
│ │ ┌─Pod──┐    │ │ ┌─Pod──┐    ││
│ │ │ App  │    │ │ │ DB   │    ││
│ │ └──────┘    │ │ └──────┘    ││
│ └─────────────┘ └─────────────┘│
└──────────────────────────────────┘
```

### K8s Config ตัวอย่าง

```yaml
# deployment.yml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: myapp
spec:
  replicas: 3                    # รัน 3 instances
  selector:
    matchLabels:
      app: myapp
  template:
    metadata:
      labels:
        app: myapp
    spec:
      containers:
        - name: myapp
          image: myapp:1.0
          ports:
            - containerPort: 3000
          resources:
            requests:
              memory: "128Mi"
              cpu: "250m"
            limits:
              memory: "256Mi"
              cpu: "500m"
---
# service.yml — expose app
apiVersion: v1
kind: Service
metadata:
  name: myapp-service
spec:
  type: LoadBalancer
  selector:
    app: myapp
  ports:
    - port: 80
      targetPort: 3000
```

```bash
# K8s Commands
kubectl apply -f deployment.yml      # deploy
kubectl get pods                     # ดู pods
kubectl get services                 # ดู services
kubectl logs myapp-xxx               # ดู logs
kubectl scale deployment myapp --replicas=5  # scale เป็น 5
kubectl rollout status deployment myapp      # ดูสถานะ update
```
