---
name: Banking Deployment Setup
description: Setup Docker, docker-compose, CI/CD GitHub Actions, และ Cloud deployment config สำหรับ Banking System
command: bank-deploy
argument-hint: "<target: docker|compose|ci|cd|cloud|all>"
allowed-tools:
  - Read
  - Write
  - Edit
  - Glob
  - Bash
  - Grep
---

# Banking Deployment Setup Skill

คุณคือผู้เชี่ยวชาญ DevOps/Deployment สำหรับ Banking System ที่ต้อง deploy บน cloud (free tier)

## Input
- **argument:** target ที่ต้องการ (required)
  - `docker` — Dockerfile สำหรับ API + Frontend
  - `compose` — docker-compose.yml สำหรับ local development
  - `ci` — GitHub Actions CI pipeline (build + test)
  - `cd` — GitHub Actions CD pipeline (deploy)
  - `cloud` — Cloud platform config (Railway/Vercel)
  - `all` — ทุกอย่าง

## Project Context
- **Backend:** ASP.NET Core 10 (.NET 10)
- **Frontend:** Next.js 15
- **Database:** PostgreSQL
- **Cache:** Redis
- **Message Broker:** RabbitMQ (optional)

## ขั้นตอนการทำงาน

### Step 1: อ่าน Context
1. อ่าน `BankingSystem/` project structure
2. อ่าน `Banking.Api/Banking.Api.csproj` — ดู target framework
3. อ่าน `Banking.Api/Program.cs` — ดู startup config
4. อ่าน `Banking.Api/appsettings.json` — ดู connection strings
5. ตรวจ existing Dockerfile/docker-compose ถ้ามี

### Step 2: Dockerfile — API
**Location:** `BankingSystem/Banking.Api/Dockerfile`
```dockerfile
# Multi-stage build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
# Copy csproj files first (layer caching)
COPY ["Banking.Api/Banking.Api.csproj", "Banking.Api/"]
COPY ["Banking.Domain/Banking.Domain.csproj", "Banking.Domain/"]
COPY ["Banking.Application/Banking.Application.csproj", "Banking.Application/"]
COPY ["Banking.Infrastructure/Banking.Infrastructure.csproj", "Banking.Infrastructure/"]
RUN dotnet restore "Banking.Api/Banking.Api.csproj"
COPY . .
RUN dotnet publish "Banking.Api/Banking.Api.csproj" -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
RUN adduser --disabled-password appuser && chown -R appuser /app
USER appuser
COPY --from=build /app .
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
HEALTHCHECK CMD curl --fail http://localhost:8080/health || exit 1
ENTRYPOINT ["dotnet", "Banking.Api.dll"]
```

### Step 3: docker-compose.yml — Local Development
**Location:** `BankingSystem/docker-compose.yml`
```yaml
services:
  api:
    build:
      context: .
      dockerfile: Banking.Api/Dockerfile
    ports: ["8080:8080"]
    depends_on: [postgres, redis]
    environment:
      - ConnectionStrings__DefaultConnection=Host=postgres;Database=banking;Username=postgres;Password=postgres123
      - ConnectionStrings__Redis=redis:6379

  postgres:
    image: postgres:16-alpine
    ports: ["5432:5432"]
    environment:
      POSTGRES_DB: banking
      POSTGRES_USER: postgres
      POSTGRES_PASSWORD: root1234
    volumes:
      - postgres_data:/var/lib/postgresql/data

  redis:
    image: redis:7-alpine
    ports: ["6379:6379"]

  pgadmin:
    image: dpage/pgadmin4
    ports: ["5050:80"]
    environment:
      PGADMIN_DEFAULT_EMAIL: admin@admin.com
      PGADMIN_DEFAULT_PASSWORD: admin

volumes:
  postgres_data:
```

### Step 4: GitHub Actions CI
**Location:** `.github/workflows/ci.yml`
- Trigger: push to main/develop, PR to main
- Jobs: backend (dotnet build + test), frontend (npm build + test)
- Services: PostgreSQL + Redis containers for integration tests
- Artifacts: build outputs + test results

### Step 5: GitHub Actions CD
**Location:** `.github/workflows/cd.yml`
- Trigger: push to main (after CI passes)
- Steps: Docker build → push to GHCR → deploy to Railway/Render
- Secrets needed: RAILWAY_TOKEN, VERCEL_TOKEN, etc.

### Step 6: Cloud Config

**Railway (Backend):**
- `railway.toml` — build config + health check
- Environment variables setup guide

**Vercel (Frontend):**
- `vercel.json` — routing + env config
- Build command + output directory

**Free Tier Summary:**
- Frontend: Vercel (free 100GB bandwidth/month)
- Backend: Railway (free $5/month) or Render (free 750hrs/month)
- Database: Neon PostgreSQL (free 0.5GB) or Supabase (free 500MB)
- Redis: Upstash (free 10,000 commands/day)
- CI/CD: GitHub Actions (free 2,000 min/month)

### Step 7: Health Check Endpoint
เพิ่ม `/health` endpoint ใน Program.cs:
```csharp
app.MapHealthChecks("/health");
// Add health check for DB + Redis connectivity
```

### Step 8: ตรวจสอบ
1. `docker build` สำเร็จ
2. `docker-compose up` ทุก service ขึ้นได้
3. API accessible ที่ localhost:8080
4. DB migration รันอัตโนมัติ
5. Health check endpoint ตอบ 200

## ตัวอย่างการใช้งาน
```
/bank-deploy docker     → สร้าง Dockerfile
/bank-deploy compose    → สร้าง docker-compose.yml
/bank-deploy ci         → สร้าง CI pipeline
/bank-deploy cloud      → สร้าง cloud deployment config
/bank-deploy all        → ทุกอย่าง
```
