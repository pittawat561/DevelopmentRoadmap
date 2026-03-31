# Phase 5: Load Balancing & Scaling — คู่มือทำทีละขั้นตอน

> Docker + docker-compose + Nginx Load Balancer + DB Read Replicas + Redis Cluster + Monitoring + Load Testing
> ทุกขั้นตอนอธิบายว่า "ทำไม" ต้องทำ + config พร้อมใช้งาน

---

## สิ่งที่ต้องเสร็จก่อน (จาก Phase 4.5)

```
☑ Backend API ทำงานครบ: Auth, Accounts, Transactions, Admin
☑ PIN Verification + Fraud Detection + Audit Logging
☑ Redis: Cache, Lock, Rate Limit, Token Blacklist, SignalR Backplane
☑ SignalR Hub: real-time notifications
☑ Health Check endpoint: /health (DB + Redis status)
☑ Unit Tests 27+ passed, Integration Tests passed
☑ dotnet build ผ่าน 0 errors
```

---

## ภาพรวม Phase 5 — ทำไมต้อง Scale?

```
ปัญหาปัจจุบัน (Phase 1-4.5):

ทุกอย่างรันบน 1 เครื่อง:
┌──────────────────────────────────┐
│ localhost                         │
│  ├── Next.js    (port 3000)      │
│  ├── API        (port 7001)      │
│  ├── PostgreSQL (port 5432)      │
│  └── Redis      (port 6379)      │
└──────────────────────────────────┘

ปัญหา:
  1. API ตาย 1 ตัว = ระบบทั้งหมดล่ม (Single Point of Failure)
  2. 10,000 users พร้อมกัน → 1 API server รับไม่ไหว
  3. DB ตาย = ข้อมูลหาย (ไม่มี replica)
  4. Deploy ใหม่ = ระบบหยุดทำงาน (downtime)

Phase 5 แก้ทุกปัญหา:
┌─────────────────────────────────────────────────────────┐
│                    Nginx (Load Balancer)                  │
│            SSL Termination + Health Checks                │
│                 ┌───────┬───────┐                        │
│                 ▼       ▼       ▼                        │
│            ┌─────┐ ┌─────┐ ┌─────┐                      │
│            │API-1│ │API-2│ │API-3│  ← Horizontal Scale   │
│            └──┬──┘ └──┬──┘ └──┬──┘                      │
│               │       │       │                          │
│          ┌────▼───────▼───────▼────┐                    │
│          │    Redis Cluster (3)     │  ← High Availability│
│          └────────────┬────────────┘                    │
│                       │                                  │
│          ┌────────────▼────────────┐                    │
│          │  PostgreSQL Primary      │                    │
│          │  ┌──────────────────┐   │                    │
│          │  │  Read Replica(s) │   │  ← Read Scaling    │
│          │  └──────────────────┘   │                    │
│          └─────────────────────────┘                    │
└─────────────────────────────────────────────────────────┘

ข้อดี:
  1. API ตาย 1 ตัว → 2 ตัวที่เหลือรับงานต่อ (High Availability)
  2. 10,000 users → กระจายไป 3 API servers (Load Balancing)
  3. DB Primary ตาย → promote Replica เป็น Primary (Failover)
  4. Deploy ใหม่ → ทำทีละตัว ไม่มี downtime (Rolling Update)
```

---

## ขั้นตอนที่ 1: Dockerize ทุก Service

### 1.1 ทำไมต้อง Docker?

```
ปัญหาไม่มี Docker:
  "มันทำงานบนเครื่องผม" → แต่พังบน server!
  ติดตั้ง .NET, PostgreSQL, Redis, Node.js ทีละตัว → ช้า, ผิดพลาดง่าย
  ทุกเครื่องต้อง config เหมือนกัน → เป็นไปไม่ได้

Docker แก้:
  ทุก service อยู่ใน container → environment เหมือนกันทุกที่
  docker compose up → ทุกอย่างพร้อมใช้ใน 30 วินาที
  Scale ง่าย: docker compose up --scale api=3
```

### 1.2 Dockerfile สำหรับ ASP.NET Core API

```
📁 BankingSystem/Banking.Api/Dockerfile

ทำไมใช้ Multi-stage build:
  Stage 1 (build): ใช้ SDK image (ใหญ่ ~800MB) → compile code
  Stage 2 (runtime): ใช้ ASP.NET runtime image (เล็ก ~200MB) → รัน app
  ผลลัพธ์: image เล็ก, เร็ว, ปลอดภัย (ไม่มี SDK ใน production)
```

```dockerfile
# Banking.Api/Dockerfile

# ===== Stage 1: Build =====
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy csproj files first → ใช้ Docker layer cache
# ถ้า csproj ไม่เปลี่ยน → ไม่ต้อง restore ใหม่ (เร็วขึ้นมาก)
COPY BankingSystem.slnx ./
COPY Banking.Domain/Banking.Domain.csproj Banking.Domain/
COPY Banking.Application/Banking.Application.csproj Banking.Application/
COPY Banking.Infrastructure/Banking.Infrastructure.csproj Banking.Infrastructure/
COPY Banking.Api/Banking.Api.csproj Banking.Api/

# Restore dependencies (cached ถ้า csproj ไม่เปลี่ยน)
RUN dotnet restore Banking.Api/Banking.Api.csproj

# Copy source code ทั้งหมด
COPY . .

# Build + Publish (Release mode, no self-contained)
WORKDIR /src/Banking.Api
RUN dotnet publish -c Release -o /app/publish --no-self-contained

# ===== Stage 2: Runtime =====
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Security: ไม่รันด้วย root
RUN addgroup --system appgroup && adduser --system --ingroup appgroup appuser
USER appuser

# Copy published output จาก build stage
COPY --from=build /app/publish .

# Expose port (Kestrel default)
EXPOSE 8080

# Health check ภายใน Docker
HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
    CMD curl -f http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "Banking.Api.dll"]
```

```
คำอธิบาย HEALTHCHECK:
  --interval=30s    → ตรวจทุก 30 วินาที
  --timeout=5s      → ถ้าไม่ตอบใน 5 วินาที = fail
  --start-period=10s → รอ 10 วินาทีก่อนเริ่มตรวจ (app กำลัง start)
  --retries=3       → fail 3 ครั้งติดกัน = unhealthy
```

### 1.3 .dockerignore

```
📁 BankingSystem/.dockerignore

ทำไม: ไม่ copy ไฟล์ที่ไม่จำเป็นเข้า Docker context
ทำให้ build เร็วขึ้น + image เล็กลง
```

```
# BankingSystem/.dockerignore

**/bin/
**/obj/
**/node_modules/
**/.git/
**/.vs/
**/.vscode/
**/Dockerfile*
**/.dockerignore
**/*.md
**/Banking.Tests.Unit/
**/Banking.Tests.Integration/
```

---

## ขั้นตอนที่ 2: Docker Compose — รันทุก Service

### 2.1 docker-compose.yml

```
📁 BankingSystem/docker-compose.yml

ทำไม: รันทุก service ด้วยคำสั่งเดียว
กำหนด network, volumes, environment variables, dependencies
```

```yaml
# BankingSystem/docker-compose.yml

version: "3.9"

services:
  # ===== PostgreSQL Primary =====
  postgres-primary:
    image: postgres:16-alpine
    container_name: banking-postgres-primary
    environment:
      POSTGRES_DB: banking_db
      POSTGRES_USER: postgres
      POSTGRES_PASSWORD: ${DB_PASSWORD:-root1234}
      # Replication settings
      POSTGRES_INITDB_ARGS: "--data-checksums"
    volumes:
      - postgres-primary-data:/var/lib/postgresql/data
      - ./docker/postgres/init-primary.sh:/docker-entrypoint-initdb.d/init-primary.sh
      - ./docker/postgres/pg_hba.conf:/etc/postgresql/pg_hba.conf
    ports:
      - "5432:5432"
    networks:
      - banking-network
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U postgres"]
      interval: 10s
      timeout: 5s
      retries: 5
    # Performance tuning
    command: >
      postgres
      -c wal_level=replica
      -c max_wal_senders=3
      -c max_replication_slots=3
      -c hot_standby=on
      -c shared_buffers=256MB
      -c effective_cache_size=768MB
      -c work_mem=4MB
      -c max_connections=200

  # ===== PostgreSQL Read Replica =====
  postgres-replica:
    image: postgres:16-alpine
    container_name: banking-postgres-replica
    environment:
      PGUSER: postgres
      PGPASSWORD: ${DB_PASSWORD:-root1234}
    volumes:
      - postgres-replica-data:/var/lib/postgresql/data
      - ./docker/postgres/init-replica.sh:/docker-entrypoint-initdb.d/init-replica.sh
    ports:
      - "5433:5432"
    networks:
      - banking-network
    depends_on:
      postgres-primary:
        condition: service_healthy

  # ===== Redis (Standalone สำหรับ development) =====
  redis:
    image: redis:7-alpine
    container_name: banking-redis
    ports:
      - "6379:6379"
    volumes:
      - redis-data:/data
      - ./docker/redis/redis.conf:/usr/local/etc/redis/redis.conf
    networks:
      - banking-network
    healthcheck:
      test: ["CMD", "redis-cli", "ping"]
      interval: 10s
      timeout: 5s
      retries: 5
    command: redis-server /usr/local/etc/redis/redis.conf

  # ===== API Instance 1 =====
  api-1:
    build:
      context: .
      dockerfile: Banking.Api/Dockerfile
    container_name: banking-api-1
    environment:
      ASPNETCORE_ENVIRONMENT: Production
      ASPNETCORE_URLS: http://+:8080
      ConnectionStrings__DefaultConnection: >-
        Host=postgres-primary;Database=banking_db;Username=postgres;Password=${DB_PASSWORD:-root1234}
      ConnectionStrings__Redis: "redis:6379,abortConnect=false"
      Jwt__Key: ${JWT_KEY:-YourSuperSecretKeyAtLeast32CharactersLong}
      Jwt__Issuer: banking-api
      Jwt__Audience: banking-frontend
      Frontend__Url: ${FRONTEND_URL:-http://localhost:3000}
      Swagger__Enabled: "false"
    networks:
      - banking-network
    depends_on:
      postgres-primary:
        condition: service_healthy
      redis:
        condition: service_healthy
    restart: unless-stopped

  # ===== API Instance 2 =====
  api-2:
    build:
      context: .
      dockerfile: Banking.Api/Dockerfile
    container_name: banking-api-2
    environment:
      ASPNETCORE_ENVIRONMENT: Production
      ASPNETCORE_URLS: http://+:8080
      ConnectionStrings__DefaultConnection: >-
        Host=postgres-primary;Database=banking_db;Username=postgres;Password=${DB_PASSWORD:-root1234}
      ConnectionStrings__Redis: "redis:6379,abortConnect=false"
      Jwt__Key: ${JWT_KEY:-YourSuperSecretKeyAtLeast32CharactersLong}
      Jwt__Issuer: banking-api
      Jwt__Audience: banking-frontend
      Frontend__Url: ${FRONTEND_URL:-http://localhost:3000}
      Swagger__Enabled: "false"
    networks:
      - banking-network
    depends_on:
      postgres-primary:
        condition: service_healthy
      redis:
        condition: service_healthy
    restart: unless-stopped

  # ===== API Instance 3 =====
  api-3:
    build:
      context: .
      dockerfile: Banking.Api/Dockerfile
    container_name: banking-api-3
    environment:
      ASPNETCORE_ENVIRONMENT: Production
      ASPNETCORE_URLS: http://+:8080
      ConnectionStrings__DefaultConnection: >-
        Host=postgres-primary;Database=banking_db;Username=postgres;Password=${DB_PASSWORD:-root1234}
      ConnectionStrings__Redis: "redis:6379,abortConnect=false"
      Jwt__Key: ${JWT_KEY:-YourSuperSecretKeyAtLeast32CharactersLong}
      Jwt__Issuer: banking-api
      Jwt__Audience: banking-frontend
      Frontend__Url: ${FRONTEND_URL:-http://localhost:3000}
      Swagger__Enabled: "false"
    networks:
      - banking-network
    depends_on:
      postgres-primary:
        condition: service_healthy
      redis:
        condition: service_healthy
    restart: unless-stopped

  # ===== Nginx Load Balancer =====
  nginx:
    image: nginx:alpine
    container_name: banking-nginx
    ports:
      - "80:80"
      - "443:443"
    volumes:
      - ./docker/nginx/nginx.conf:/etc/nginx/nginx.conf:ro
      - ./docker/nginx/ssl:/etc/nginx/ssl:ro
    networks:
      - banking-network
    depends_on:
      - api-1
      - api-2
      - api-3
    restart: unless-stopped

  # ===== Prometheus (Monitoring) =====
  prometheus:
    image: prom/prometheus:latest
    container_name: banking-prometheus
    ports:
      - "9090:9090"
    volumes:
      - ./docker/prometheus/prometheus.yml:/etc/prometheus/prometheus.yml:ro
      - prometheus-data:/prometheus
    networks:
      - banking-network
    restart: unless-stopped

  # ===== Grafana (Dashboard) =====
  grafana:
    image: grafana/grafana:latest
    container_name: banking-grafana
    ports:
      - "3001:3000"
    environment:
      GF_SECURITY_ADMIN_USER: admin
      GF_SECURITY_ADMIN_PASSWORD: ${GRAFANA_PASSWORD:-admin123}
    volumes:
      - grafana-data:/var/lib/grafana
    networks:
      - banking-network
    restart: unless-stopped

# ===== Volumes =====
volumes:
  postgres-primary-data:
  postgres-replica-data:
  redis-data:
  prometheus-data:
  grafana-data:

# ===== Network =====
networks:
  banking-network:
    driver: bridge
```

### 2.2 Environment Variables (.env)

```bash
# BankingSystem/.env — ห้าม commit ลง git!

DB_PASSWORD=StrongPassword123!
JWT_KEY=YourSuperSecretKeyAtLeast32CharactersLong!@#$
FRONTEND_URL=http://localhost:3000
GRAFANA_PASSWORD=admin123
```

```bash
# เพิ่มใน .gitignore
echo ".env" >> .gitignore
```

---

## ขั้นตอนที่ 3: Nginx Load Balancer Configuration

### 3.1 nginx.conf

```
📁 docker/nginx/nginx.conf

ทำไม: Nginx เป็น reverse proxy + load balancer
รับ request จาก client → กระจายไปยัง API instances

Load Balancing Algorithms:
  round-robin: วนไปทีละตัว (default, ง่ายที่สุด)
  least_conn: ส่งไปตัวที่มี connection น้อยที่สุด (ดีกว่าสำหรับ long requests)
  ip_hash: client เดิมไปตัวเดิมเสมอ (sticky session)

เราใช้ least_conn เพราะ:
  Banking transaction ใช้เวลาต่างกัน (ฝาก 50ms vs โอน 200ms)
  least_conn จะกระจาย load ได้สม่ำเสมอกว่า round-robin
```

```nginx
# docker/nginx/nginx.conf

# Worker processes = จำนวน CPU cores
worker_processes auto;

events {
    worker_connections 2048;
    multi_accept on;
}

http {
    # ===== Basic Settings =====
    sendfile on;
    tcp_nopush on;
    tcp_nodelay on;
    keepalive_timeout 65;
    types_hash_max_size 2048;
    client_max_body_size 10M;

    # ===== Logging =====
    log_format main '$remote_addr - $remote_user [$time_local] '
                    '"$request" $status $body_bytes_sent '
                    '"$http_referer" "$http_user_agent" '
                    'upstream=$upstream_addr '
                    'response_time=$upstream_response_time';

    access_log /var/log/nginx/access.log main;
    error_log /var/log/nginx/error.log warn;

    # ===== Gzip Compression =====
    gzip on;
    gzip_vary on;
    gzip_proxied any;
    gzip_comp_level 6;
    gzip_types
        text/plain
        text/css
        text/javascript
        application/json
        application/javascript;

    # ===== Rate Limiting (First Layer) =====
    # จำกัด 10 requests/second per IP (burst 20)
    # ทำงานร่วมกับ Redis rate limiting ใน API (Second Layer)
    limit_req_zone $binary_remote_addr zone=api_limit:10m rate=10r/s;
    limit_req_zone $binary_remote_addr zone=auth_limit:10m rate=3r/s;

    # ===== API Backend (Upstream) =====
    # least_conn: ส่ง request ไปตัวที่ busy น้อยที่สุด
    upstream api_backend {
        least_conn;

        server api-1:8080 max_fails=3 fail_timeout=30s;
        server api-2:8080 max_fails=3 fail_timeout=30s;
        server api-3:8080 max_fails=3 fail_timeout=30s;

        # Keepalive connections ไปยัง backend
        # ลด overhead ของ TCP handshake
        keepalive 32;
    }

    # ===== Main Server Block =====
    server {
        listen 80;
        server_name localhost;

        # === Security Headers ===
        add_header X-Frame-Options "SAMEORIGIN" always;
        add_header X-Content-Type-Options "nosniff" always;
        add_header X-XSS-Protection "1; mode=block" always;
        add_header Referrer-Policy "strict-origin-when-cross-origin" always;
        add_header Content-Security-Policy "default-src 'self'" always;

        # === Health Check Endpoint (Nginx level) ===
        location /nginx-health {
            access_log off;
            return 200 '{"status":"healthy","service":"nginx"}';
            add_header Content-Type application/json;
        }

        # === API Proxy ===
        location /api/ {
            # Rate limiting (burst=20, nodelay=ไม่ queue แค่ reject)
            limit_req zone=api_limit burst=20 nodelay;
            limit_req_status 429;

            proxy_pass http://api_backend;

            # Proxy headers — ส่งข้อมูล client จริงไป API
            proxy_set_header Host $host;
            proxy_set_header X-Real-IP $remote_addr;
            proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
            proxy_set_header X-Forwarded-Proto $scheme;

            # Keepalive
            proxy_http_version 1.1;
            proxy_set_header Connection "";

            # Timeouts
            proxy_connect_timeout 5s;
            proxy_send_timeout 30s;
            proxy_read_timeout 30s;

            # Retry ถ้า backend fail (เฉพาะ idempotent methods)
            proxy_next_upstream error timeout http_502 http_503;
            proxy_next_upstream_tries 2;
        }

        # === Auth endpoints — Rate limit เข้มกว่า ===
        location /api/auth/ {
            limit_req zone=auth_limit burst=5 nodelay;
            limit_req_status 429;

            proxy_pass http://api_backend;
            proxy_set_header Host $host;
            proxy_set_header X-Real-IP $remote_addr;
            proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
            proxy_set_header X-Forwarded-Proto $scheme;
            proxy_http_version 1.1;
            proxy_set_header Connection "";
        }

        # === SignalR WebSocket ===
        # WebSocket ต้อง config พิเศษ: Connection: Upgrade
        location /hubs/ {
            proxy_pass http://api_backend;

            proxy_set_header Host $host;
            proxy_set_header X-Real-IP $remote_addr;
            proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
            proxy_set_header X-Forwarded-Proto $scheme;

            # ⚠️ WebSocket upgrade headers
            proxy_http_version 1.1;
            proxy_set_header Upgrade $http_upgrade;
            proxy_set_header Connection "upgrade";

            # WebSocket timeout (ยาวกว่า HTTP ปกติ)
            proxy_read_timeout 3600s;
            proxy_send_timeout 3600s;
        }

        # === Health Check (API level) ===
        location /health {
            proxy_pass http://api_backend;
            proxy_set_header Host $host;
            access_log off;
        }
    }

    # ===== HTTPS Server Block (Production) =====
    # Uncomment เมื่อมี SSL certificate
    # server {
    #     listen 443 ssl http2;
    #     server_name yourdomain.com;
    #
    #     ssl_certificate /etc/nginx/ssl/cert.pem;
    #     ssl_certificate_key /etc/nginx/ssl/key.pem;
    #     ssl_protocols TLSv1.2 TLSv1.3;
    #     ssl_ciphers HIGH:!aNULL:!MD5;
    #     ssl_prefer_server_ciphers on;
    #
    #     # ... same location blocks as above ...
    # }

    # Redirect HTTP → HTTPS (Production)
    # server {
    #     listen 80;
    #     server_name yourdomain.com;
    #     return 301 https://$server_name$request_uri;
    # }
}
```

```
สรุป Rate Limiting 2 ชั้น:

Layer 1 — Nginx (ชั้นนอก):
  ทุก endpoint: 10 req/s per IP
  /api/auth/*: 3 req/s per IP (ป้องกัน brute force)
  → กรอง bot/DDoS ก่อนเข้า API

Layer 2 — Redis Middleware (ชั้นใน):
  10 req/min per user per endpoint
  → กรอง abuse จาก authenticated users
```

---

## ขั้นตอนที่ 4: PostgreSQL Replication (Primary + Replica)

### 4.1 Init Script สำหรับ Primary

```bash
#!/bin/bash
# docker/postgres/init-primary.sh
# สร้าง replication user + slot สำหรับ replica

set -e

psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" <<-EOSQL
    -- สร้าง user สำหรับ replication
    CREATE USER replicator WITH REPLICATION ENCRYPTED PASSWORD 'replicator_password';

    -- สร้าง replication slot (ป้องกัน WAL ถูกลบก่อน replica อ่านเสร็จ)
    SELECT pg_create_physical_replication_slot('replica_slot_1');

    -- สร้าง read-only user สำหรับ app อ่านจาก replica
    CREATE USER readonly WITH PASSWORD 'readonly_password';
    GRANT CONNECT ON DATABASE banking_db TO readonly;
    GRANT USAGE ON SCHEMA public TO readonly;
    GRANT SELECT ON ALL TABLES IN SCHEMA public TO readonly;
    ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT SELECT ON TABLES TO readonly;
EOSQL

echo "Primary initialization completed."
```

### 4.2 pg_hba.conf (Authentication)

```
# docker/postgres/pg_hba.conf
# ควบคุมว่า ใครเชื่อมต่อได้จากไหน

# TYPE  DATABASE        USER            ADDRESS                 METHOD
local   all             all                                     trust
host    all             all             127.0.0.1/32            scram-sha-256
host    all             all             ::1/128                 scram-sha-256

# อนุญาต replication จาก Docker network
host    replication     replicator      172.16.0.0/12           scram-sha-256
host    all             all             172.16.0.0/12           scram-sha-256
```

### 4.3 Init Script สำหรับ Replica

```bash
#!/bin/bash
# docker/postgres/init-replica.sh
# Clone data จาก Primary แล้วตั้งค่าเป็น standby

set -e

# ถ้ามี data อยู่แล้ว → ข้าม (ไม่ init ซ้ำ)
if [ -s "/var/lib/postgresql/data/PG_VERSION" ]; then
    echo "Replica data already exists, skipping init."
    exit 0
fi

# รอ Primary พร้อม
until pg_isready -h postgres-primary -U postgres; do
    echo "Waiting for primary..."
    sleep 2
done

# ลบ data directory เดิม (ถ้ามี)
rm -rf /var/lib/postgresql/data/*

# Clone จาก Primary ด้วย pg_basebackup
pg_basebackup \
    -h postgres-primary \
    -U replicator \
    -D /var/lib/postgresql/data \
    -Fp -Xs -P -R \
    -S replica_slot_1

# -Fp: plain format
# -Xs: stream WAL ระหว่าง backup
# -P: show progress
# -R: สร้าง standby.signal + primary_conninfo อัตโนมัติ

echo "Replica initialization completed."
```

### 4.4 อัปเดต ASP.NET Core — Read/Write Splitting

```
📁 ทำไมต้อง Read/Write Split:
  Write (INSERT/UPDATE/DELETE) → Primary เท่านั้น
  Read (SELECT) → Replica ได้ (ลด load ของ Primary)

  Banking app: อ่านยอดเงิน (read) บ่อยกว่าฝาก/ถอน (write) 10:1
  → ส่ง read ไป Replica ลด load Primary ได้ 90%
```

```csharp
// Banking.Infrastructure/Data/ReadOnlyDbContext.cs

using Microsoft.EntityFrameworkCore;

namespace Banking.Infrastructure.Data;

/// <summary>
/// Read-Only DbContext — ใช้เฉพาะ read queries
/// เชื่อมต่อ PostgreSQL Read Replica
/// ไม่มี SaveChanges (ป้องกันเขียนลง replica)
/// </summary>
public class ReadOnlyDbContext : AppDbContext
{
    public ReadOnlyDbContext(DbContextOptions<ReadOnlyDbContext> options)
        : base(options) { }

    // ปิด change tracking → เร็วขึ้นสำหรับ read-only queries
    // ไม่ track entity changes → ไม่ใช้ memory เก็บ snapshot
    public override int SaveChanges()
    {
        throw new InvalidOperationException("This is a read-only context.");
    }

    public override Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        throw new InvalidOperationException("This is a read-only context.");
    }
}
```

```csharp
// เพิ่มใน Program.cs — Register ReadOnlyDbContext

// Write DbContext → Primary
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection"), ...));

// Read DbContext → Replica
builder.Services.AddDbContext<ReadOnlyDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("ReadConnection"))
    .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking));
```

```json
// appsettings.Production.json

{
  "ConnectionStrings": {
    "DefaultConnection": "Host=postgres-primary;Database=banking_db;Username=postgres;Password=...",
    "ReadConnection": "Host=postgres-replica;Database=banking_db;Username=readonly;Password=..."
  }
}
```

---

## ขั้นตอนที่ 5: Redis Configuration

### 5.1 redis.conf

```
# docker/redis/redis.conf

# ===== Memory =====
maxmemory 256mb
maxmemory-policy allkeys-lru
# allkeys-lru: เมื่อ memory เต็ม → ลบ key ที่ไม่ได้ใช้นานสุด (LRU)

# ===== Persistence =====
# RDB snapshot: เก็บ snapshot ทุก 60 วินาที ถ้ามี >= 1000 keys เปลี่ยน
save 60 1000
# AOF: บันทึกทุก write command (ป้องกัน data loss)
appendonly yes
appendfsync everysec

# ===== Network =====
bind 0.0.0.0
protected-mode no
tcp-keepalive 300

# ===== Performance =====
tcp-backlog 511
databases 16
```

---

## ขั้นตอนที่ 6: Monitoring (Prometheus + Grafana)

### 6.1 ทำไมต้อง Monitor?

```
ไม่มี Monitoring = ขับรถไม่มี dashboard
  - ไม่รู้ว่า API ช้าแค่ไหน
  - ไม่รู้ว่า DB ใกล้เต็มไหม
  - ไม่รู้ว่ามี error spike ไหม
  - รู้ตัวเมื่อ user แจ้ง (สายเกินไป!)

Prometheus + Grafana:
  Prometheus → เก็บ metrics (CPU, memory, request count, latency)
  Grafana → แสดง dashboard สวยๆ + alerting
```

### 6.2 เพิ่ม Prometheus Metrics ใน ASP.NET Core

```bash
# เพิ่ม NuGet package
dotnet add Banking.Api/Banking.Api.csproj package prometheus-net.AspNetCore
```

```csharp
// เพิ่มใน Program.cs

using Prometheus;

// ก่อน app.Run()
app.UseHttpMetrics(); // วัด HTTP request metrics อัตโนมัติ
app.MapMetrics();     // Expose /metrics endpoint สำหรับ Prometheus
```

### 6.3 prometheus.yml

```yaml
# docker/prometheus/prometheus.yml

global:
  scrape_interval: 15s      # เก็บ metrics ทุก 15 วินาที
  evaluation_interval: 15s   # evaluate rules ทุก 15 วินาที

scrape_configs:
  # ===== API Servers =====
  - job_name: "banking-api"
    metrics_path: /metrics
    static_configs:
      - targets:
          - "api-1:8080"
          - "api-2:8080"
          - "api-3:8080"
        labels:
          service: "banking-api"

  # ===== Nginx =====
  - job_name: "nginx"
    static_configs:
      - targets: ["nginx:80"]
        labels:
          service: "nginx"

  # ===== PostgreSQL (ต้องติดตั้ง postgres_exporter) =====
  # - job_name: "postgres"
  #   static_configs:
  #     - targets: ["postgres-exporter:9187"]

  # ===== Redis (ต้องติดตั้ง redis_exporter) =====
  # - job_name: "redis"
  #   static_configs:
  #     - targets: ["redis-exporter:9121"]
```

### 6.4 Grafana Dashboard

```
เข้า Grafana: http://localhost:3001
  Username: admin
  Password: admin123 (จาก .env)

สร้าง Dashboard:
  1. Add Data Source → Prometheus → URL: http://prometheus:9090
  2. Import Dashboard → ID: 10427 (ASP.NET Core dashboard)
  3. Import Dashboard → ID: 1860 (Node Exporter)

Metrics ที่สำคัญ:
  - http_requests_total → จำนวน requests
  - http_request_duration_seconds → latency (p50, p95, p99)
  - process_cpu_seconds_total → CPU usage
  - process_working_set_bytes → Memory usage
  - dotnet_gc_collection_count_total → GC frequency
```

---

## ขั้นตอนที่ 7: Load Testing (k6)

### 7.1 ทำไมต้อง Load Test?

```
ถ้าไม่ test load:
  Deploy ขึ้น production → 1,000 users เข้าพร้อมกัน → ระบบล่ม!

k6 คืออะไร:
  Load testing tool ของ Grafana
  เขียน test ด้วย JavaScript
  จำลอง users หลายพัน → วัด performance
```

### 7.2 ติดตั้ง k6

```bash
# Windows (winget)
winget install k6

# macOS
brew install k6

# Docker
docker run -i grafana/k6 run - < test.js
```

### 7.3 Load Test Scripts

```javascript
// docker/k6/load-test.js
// ทดสอบ Banking API ด้วย k6

import http from 'k6/http';
import { check, sleep, group } from 'k6';
import { Rate, Trend } from 'k6/metrics';

// Custom metrics
const errorRate = new Rate('errors');
const depositDuration = new Trend('deposit_duration');

// Test configuration
export const options = {
    stages: [
        { duration: '30s', target: 10 },   // Ramp up: 0 → 10 users ใน 30 วินาที
        { duration: '1m',  target: 50 },   // Ramp up: 10 → 50 users ใน 1 นาที
        { duration: '2m',  target: 50 },   // Hold: 50 users คงที่ 2 นาที
        { duration: '30s', target: 100 },  // Spike: 50 → 100 users
        { duration: '1m',  target: 100 },  // Hold: 100 users คงที่ 1 นาที
        { duration: '30s', target: 0 },    // Ramp down: 100 → 0
    ],
    thresholds: {
        http_req_duration: ['p(95)<500'],  // 95% ของ requests ต้องเสร็จใน 500ms
        http_req_failed: ['rate<0.01'],    // Error rate < 1%
        errors: ['rate<0.05'],             // Custom error rate < 5%
    },
};

const BASE_URL = __ENV.API_URL || 'http://localhost:80';

// Shared test data
let authToken = '';
let testAccountId = '';

export function setup() {
    // Register a test user
    const uniqueId = Date.now();
    const registerRes = http.post(`${BASE_URL}/api/auth/register`, JSON.stringify({
        firstName: 'LoadTest',
        lastName: `User${uniqueId}`,
        email: `loadtest-${uniqueId}@test.com`,
        phone: `08${String(uniqueId).slice(-8)}`,
        password: 'Password1',
        confirmPassword: 'Password1',
    }), { headers: { 'Content-Type': 'application/json' } });

    const data = JSON.parse(registerRes.body);
    return {
        token: data.data.accessToken,
        userId: data.data.userId,
    };
}

export default function (data) {
    const headers = {
        'Content-Type': 'application/json',
        'Authorization': `Bearer ${data.token}`,
    };

    group('Health Check', () => {
        const res = http.get(`${BASE_URL}/health`);
        check(res, {
            'health status 200': (r) => r.status === 200,
        });
    });

    group('Get Accounts', () => {
        const res = http.get(
            `${BASE_URL}/api/accounts?userId=${data.userId}`,
            { headers }
        );
        check(res, {
            'accounts status 200': (r) => r.status === 200,
        });

        if (res.status === 200) {
            const body = JSON.parse(res.body);
            if (body.data && body.data.length > 0) {
                testAccountId = body.data[0].id;
            }
        }
    });

    group('Get Balance', () => {
        if (!testAccountId) return;

        const res = http.get(
            `${BASE_URL}/api/accounts/${testAccountId}/balance`,
            { headers }
        );
        check(res, {
            'balance status 200': (r) => r.status === 200,
        });
        errorRate.add(res.status !== 200);
    });

    sleep(1); // Wait 1 second between iterations (simulate real user)
}

export function teardown(data) {
    // Cleanup: logout
    http.post(`${BASE_URL}/api/auth/logout`, null, {
        headers: { 'Authorization': `Bearer ${data.token}` },
    });
}
```

### 7.4 รัน Load Test

```bash
# Basic run
k6 run docker/k6/load-test.js

# กำหนด API URL
k6 run -e API_URL=http://localhost:80 docker/k6/load-test.js

# Output ไปยัง Prometheus (ดูใน Grafana)
k6 run --out experimental-prometheus-rw docker/k6/load-test.js

# Quick smoke test (10 users, 30 seconds)
k6 run --vus 10 --duration 30s docker/k6/load-test.js
```

```
อ่านผลลัพธ์ k6:

  http_req_duration:
    avg=45ms    → เฉลี่ย 45ms (ดี)
    p(90)=120ms → 90% เสร็จใน 120ms (ดี)
    p(95)=250ms → 95% เสร็จใน 250ms (ยอมรับได้)
    p(99)=500ms → 99% เสร็จใน 500ms (ต้องปรับถ้าเกิน)

  http_req_failed:
    0.5% → Error rate 0.5% (ดี, ต่ำกว่า threshold 1%)

  iterations:
    12,500 → ทำ 12,500 requests ใน 5 นาที

เป้าหมาย Banking App:
  p(95) < 500ms
  Error rate < 1%
  Support 100+ concurrent users
```

---

## ขั้นตอนที่ 8: รันทุกอย่าง

### 8.1 สร้าง Directory Structure

```bash
mkdir -p docker/nginx/ssl
mkdir -p docker/postgres
mkdir -p docker/redis
mkdir -p docker/prometheus
mkdir -p docker/k6
```

### 8.2 Start ทุก Service

```bash
# Build + Start ทุก service
docker compose up -d --build

# ดู logs
docker compose logs -f

# ดู status
docker compose ps

# ทดสอบ
curl http://localhost/health
curl http://localhost/api/auth/register -X POST -H "Content-Type: application/json" \
  -d '{"firstName":"Test","lastName":"User","email":"test@test.com","phone":"0812345678","password":"Password1","confirmPassword":"Password1"}'
```

### 8.3 Scale API Servers

```bash
# เพิ่ม API instances เป็น 5 ตัว
docker compose up -d --scale api-1=1 --scale api-2=1 --scale api-3=1

# หรือใช้ docker compose profiles สำหรับ scaling
# ดู docker compose ps → ทุกตัวต้อง healthy
```

### 8.4 ทดสอบ Failover

```bash
# หยุด API instance 1 → Nginx ส่ง traffic ไป 2 และ 3
docker stop banking-api-1
curl http://localhost/health  # ยังทำงาน!

# Start กลับ
docker start banking-api-1
```

### 8.5 เข้า Monitoring

```
Prometheus: http://localhost:9090
  → Status → Targets → ดูว่า api-1, api-2, api-3 เป็น UP

Grafana: http://localhost:3001
  → Login: admin / admin123
  → Add Data Source → Prometheus → http://prometheus:9090
  → Import Dashboard
```

---

## ขั้นตอนที่ 9: ASP.NET Core Production Configuration

### 9.1 Forwarded Headers (ใช้กับ Nginx)

```csharp
// เพิ่มใน Program.cs — ก่อน middleware pipeline

// บอก ASP.NET Core ว่าอยู่หลัง reverse proxy
// ให้ใช้ X-Forwarded-For แทน RemoteIpAddress
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor
        | Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

// ใน middleware pipeline — ต้องอยู่ก่อน UseAuthentication
app.UseForwardedHeaders();
```

```
ทำไมต้อง ForwardedHeaders:
  Client → Nginx → API

  ไม่มี ForwardedHeaders:
    HttpContext.Connection.RemoteIpAddress = IP ของ Nginx (172.x.x.x)
    → Rate limit ทุก user ด้วย IP เดียวกัน!
    → Audit log บันทึก IP ของ Nginx ไม่ใช่ client

  มี ForwardedHeaders:
    Nginx ส่ง header: X-Forwarded-For: 203.x.x.x (client IP จริง)
    ASP.NET Core อ่าน header → RemoteIpAddress = 203.x.x.x (ถูกต้อง!)
```

---

## Checklist — สิ่งที่ต้องเสร็จก่อนไป Phase 6

```
Docker:
☐ Dockerfile สำหรับ API (multi-stage build)
☐ .dockerignore ครบ
☐ docker-compose.yml รวมทุก service
☐ .env สำหรับ secrets (ไม่ commit git)
☐ docker compose up -d → ทุก service healthy

Nginx:
☐ nginx.conf: least_conn upstream, proxy_pass
☐ Rate limiting 2 layers (Nginx + Redis)
☐ WebSocket support สำหรับ SignalR (/hubs/)
☐ Security headers (X-Frame-Options, CSP, etc.)
☐ Health check endpoint (/nginx-health)
☐ Gzip compression

PostgreSQL:
☐ Primary + Replica replication
☐ init-primary.sh: replication user + slot
☐ init-replica.sh: pg_basebackup clone
☐ ReadOnlyDbContext สำหรับ read queries
☐ Connection string แยก Write/Read

Redis:
☐ redis.conf: memory limit, persistence, LRU policy

Monitoring:
☐ Prometheus scraping API metrics
☐ Grafana dashboard (http_request_duration, error rate)
☐ prometheus-net.AspNetCore ติดตั้งใน API
☐ /metrics endpoint exposed

Load Testing:
☐ k6 load test script
☐ Health check, Get accounts, Get balance scenarios
☐ p(95) < 500ms threshold
☐ Error rate < 1% threshold

Production Config:
☐ ForwardedHeaders สำหรับ Nginx proxy
☐ HTTPS ready (SSL config commented, พร้อมเปิด)
☐ Non-root user ใน Dockerfile
☐ Docker HEALTHCHECK

Testing:
☐ docker compose up → ทุก service healthy
☐ curl /health → 200
☐ หยุด 1 API → ระบบยังทำงาน (failover)
☐ k6 load test ผ่าน thresholds
☐ Grafana แสดง metrics ได้

เมื่อ checklist ครบ → พร้อมไป Phase 6: CI/CD Pipeline (GitHub Actions)
```

---

## Troubleshooting

### docker compose up ล้มเหลว "port already in use"
```
Port 5432 ถูกใช้โดย PostgreSQL local:
  Option 1: หยุด PostgreSQL local → net stop postgresql
  Option 2: เปลี่ยน port ใน docker-compose.yml → "5433:5432"
```

### Replica ไม่ sync กับ Primary
```
1. ตรวจ replication slot:
   docker exec banking-postgres-primary psql -U postgres -c "SELECT * FROM pg_replication_slots;"
2. ตรวจ WAL receiver:
   docker exec banking-postgres-replica psql -U postgres -c "SELECT * FROM pg_stat_wal_receiver;"
3. ลบ replica data แล้ว init ใหม่:
   docker compose down postgres-replica
   docker volume rm bankingsystem_postgres-replica-data
   docker compose up -d postgres-replica
```

### Nginx 502 Bad Gateway
```
API container ยังไม่พร้อม:
  docker compose logs api-1 → ดู error
  ตรวจ healthcheck: docker inspect banking-api-1 | grep Health

เพิ่ม timeout ใน nginx.conf:
  proxy_connect_timeout 10s;
```

### SignalR ผ่าน Nginx ไม่ทำงาน
```
ตรวจว่า nginx.conf มี WebSocket upgrade:
  proxy_set_header Upgrade $http_upgrade;
  proxy_set_header Connection "upgrade";

ตรวจว่า location /hubs/ มี proxy_read_timeout ยาวพอ (3600s)
```

### k6 "connection refused"
```
ตรวจว่า API_URL ถูกต้อง:
  k6 run -e API_URL=http://localhost:80 test.js

ไม่ใช่ https (ถ้าไม่มี SSL):
  ❌ https://localhost
  ✅ http://localhost
```

### Grafana ไม่แสดง metrics
```
1. ตรวจ Prometheus targets: http://localhost:9090/targets
   → api-1, api-2, api-3 ต้องเป็น UP
2. ตรวจว่า API มี /metrics endpoint:
   curl http://localhost/metrics
3. ตรวจ Grafana data source: URL ต้องเป็น http://prometheus:9090
   (ไม่ใช่ localhost — เพราะอยู่คนละ container)
```

### Memory leak ใน Docker
```
ตั้ง memory limit ใน docker-compose.yml:
  deploy:
    resources:
      limits:
        memory: 512M

ดู memory usage:
  docker stats
```
