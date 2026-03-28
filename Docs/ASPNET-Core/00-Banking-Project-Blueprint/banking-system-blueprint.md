# Blueprint: ระบบฝากถอนเงิน (Banking System)

## Context
สร้างระบบธนาคาร ฝาก/ถอน/โอนเงิน รองรับ 10,000-100,000+ users พร้อมกัน
Stack: **Next.js** (Frontend) + **ASP.NET Core** (Backend API) + **Redis** (Cache/Real-time) + **Load Balancer** (Nginx/HAProxy)

---

## System Architecture Overview

```
                          ┌─────────────────┐
                          │   Cloudflare     │
                          │   CDN / WAF      │
                          └────────┬─────────┘
                                   │
                          ┌────────▼─────────┐
                          │  Load Balancer   │
                          │  (Nginx/HAProxy) │
                          └──┬─────┬─────┬───┘
                             │     │     │
                    ┌────────▼┐ ┌──▼───┐ ┌▼────────┐
                    │Next.js  │ │Next.js│ │Next.js  │   ← Frontend (SSR)
                    │Instance1│ │Inst.2 │ │Inst.3   │
                    └────┬────┘ └──┬───┘ └┬────────┘
                         │        │       │
                    ┌────▼────────▼───────▼───┐
                    │    API Gateway (YARP)    │
                    └──┬──────┬──────┬────────┘
                       │      │      │
              ┌────────▼┐ ┌──▼────┐ ┌▼─────────┐
              │ API      │ │ API   │ │ API      │   ← ASP.NET Core
              │ Node 1   │ │Node 2 │ │ Node 3   │      (Stateless)
              └──┬───┬───┘ └─┬──┬─┘ └┬───┬─────┘
                 │   │       │  │     │   │
          ┌──────▼───▼───────▼──▼─────▼───▼──────┐
          │         Redis Cluster                  │   ← Cache, Session,
          │   (Cache / Pub-Sub / Rate Limit)       │      Real-time, Lock
          └──────────────────┬─────────────────────┘
                             │
          ┌──────────────────▼─────────────────────┐
          │      PostgreSQL (Primary + Replica)     │   ← Main Database
          │   ┌──────────┐    ┌──────────────────┐  │
          │   │ Primary  │───►│ Read Replica(s)  │  │
          │   │ (Write)  │    │ (Read queries)   │  │
          │   └──────────┘    └──────────────────┘  │
          └────────────────────────────────────────-┘
                             │
          ┌──────────────────▼─────────────────────┐
          │      RabbitMQ (Message Broker)          │   ← Async Processing
          │  (Notifications, Audit, Reports)        │
          └────────────────────────────────────────-┘
```

---

## Phase 1: Database Design (ทำก่อนเลย!)

### Core Tables

```
accounts
├── id (PK, UUID)
├── user_id (FK → users)
├── account_number (UNIQUE, เช่น "1234-5678-9012")
├── account_type (savings, checking, fixed_deposit)
├── currency (THB, USD)
├── balance (DECIMAL 18,2) ← ยอดเงินปัจจุบัน
├── available_balance (DECIMAL 18,2) ← ยอดที่ใช้ได้ (หัก hold)
├── daily_withdrawal_limit (DECIMAL 18,2)
├── status (active, frozen, closed)
├── created_at, updated_at

users
├── id (PK, UUID)
├── national_id_hash (เก็บ hash ไม่เก็บ plain!)
├── first_name, last_name
├── email (UNIQUE), phone (UNIQUE)
├── password_hash
├── kyc_status (pending, verified, rejected)
├── is_active, is_locked
├── failed_login_attempts
├── last_login_at
├── created_at, updated_at

transactions
├── id (PK, UUID)
├── reference_number (UNIQUE, เช่น "TXN-20240115-ABC123")
├── account_id (FK → accounts)
├── type (deposit, withdrawal, transfer_in, transfer_out, fee, interest)
├── amount (DECIMAL 18,2)
├── balance_before (DECIMAL 18,2) ← snapshot ก่อนทำรายการ
├── balance_after (DECIMAL 18,2) ← snapshot หลังทำรายการ
├── status (pending, processing, completed, failed, reversed)
├── description
├── related_transaction_id (FK → transactions, สำหรับ transfer คู่)
├── metadata (JSONB — ข้อมูลเพิ่มเติม)
├── ip_address
├── created_at

transfers
├── id (PK, UUID)
├── from_account_id (FK)
├── to_account_id (FK)
├── amount
├── fee
├── status
├── debit_transaction_id (FK → transactions)
├── credit_transaction_id (FK → transactions)
├── created_at

audit_logs
├── id (PK, BIGINT)
├── user_id
├── action (login, deposit, withdrawal, transfer, settings_change)
├── entity_type, entity_id
├── old_values (JSONB)
├── new_values (JSONB)
├── ip_address, user_agent
├── created_at
```

### Critical Constraints
- `balance` ต้อง >= 0 (CHECK constraint)
- `transactions.balance_after` = `balance_before` + amount (สำหรับ deposit) หรือ - amount (สำหรับ withdrawal)
- ทุก transaction ต้องอยู่ใน DB Transaction (ACID)
- ใช้ **Row-level locking** (`SELECT ... FOR UPDATE`) เมื่อแก้ไข balance

---

## Phase 2: ASP.NET Core Backend API

### Project Structure (Clean Architecture)

```
BankingApi/
├── src/
│   ├── Banking.Domain/
│   │   ├── Entities/ (User, Account, Transaction, Transfer)
│   │   ├── Enums/ (AccountType, TransactionType, TransactionStatus)
│   │   ├── Interfaces/ (IAccountRepository, ITransactionRepository)
│   │   ├── Exceptions/ (InsufficientFundsException, AccountFrozenException)
│   │   └── Events/ (TransactionCompletedEvent, FraudDetectedEvent)
│   │
│   ├── Banking.Application/
│   │   ├── Accounts/
│   │   │   ├── Commands/ (CreateAccount, FreezeAccount)
│   │   │   └── Queries/ (GetBalance, GetStatement)
│   │   ├── Transactions/
│   │   │   ├── Commands/ (Deposit, Withdraw, Transfer)
│   │   │   └── Queries/ (GetTransaction, GetHistory)
│   │   ├── Auth/
│   │   │   └── Commands/ (Login, Register, RefreshToken)
│   │   └── Common/
│   │       ├── Behaviors/ (ValidationBehavior, LoggingBehavior, TransactionBehavior)
│   │       └── Interfaces/ (ICurrentUser, IDateTimeProvider)
│   │
│   ├── Banking.Infrastructure/
│   │   ├── Data/ (AppDbContext, Configurations/, Migrations/)
│   │   ├── Repositories/
│   │   ├── Services/ (RedisCache, EmailService, SmsService)
│   │   ├── BackgroundJobs/ (DailyInterestJob, StatementGeneratorJob)
│   │   └── ExternalServices/ (BankGateway, FraudDetection)
│   │
│   └── Banking.Api/
│       ├── Controllers/ (AccountsController, TransactionsController, AuthController)
│       ├── Hubs/ (NotificationHub — real-time balance updates)
│       ├── Middleware/ (ExceptionMiddleware, RateLimitMiddleware, AuditMiddleware)
│       └── Program.cs
│
└── tests/
    ├── Banking.Tests.Unit/
    └── Banking.Tests.Integration/
```

### Critical API Endpoints

```
Auth:
POST   /api/auth/register          ← สมัครสมาชิก + KYC
POST   /api/auth/login             ← Login → JWT + Refresh Token
POST   /api/auth/refresh           ← Refresh Token
POST   /api/auth/logout            ← Revoke Token

Accounts:
GET    /api/accounts               ← บัญชีทั้งหมดของ user
GET    /api/accounts/{id}          ← รายละเอียดบัญชี
GET    /api/accounts/{id}/balance  ← ยอดเงิน (real-time จาก Redis)
GET    /api/accounts/{id}/statement?from=&to= ← Statement

Transactions:
POST   /api/transactions/deposit   ← ฝากเงิน
POST   /api/transactions/withdraw  ← ถอนเงิน
POST   /api/transactions/transfer  ← โอนเงิน
GET    /api/transactions/{id}      ← รายละเอียด transaction
GET    /api/transactions?page=&size= ← ประวัติ transactions

Admin:
GET    /api/admin/dashboard        ← สถิติระบบ
POST   /api/admin/accounts/{id}/freeze  ← อายัดบัญชี
GET    /api/admin/transactions/suspicious ← รายการต้องสงสัย
```

### Critical Logic: Withdrawal (ตัวอย่าง Flow ที่ซับซ้อน)

```
1. Validate: amount > 0, account active, KYC verified
2. Rate Limit Check (Redis): ไม่เกิน X ครั้ง/นาที
3. Daily Limit Check: ถอนวันนี้ + amount <= daily_limit
4. Acquire Distributed Lock (Redis): lock:account:{id}
5. DB Transaction:
   a. SELECT balance FROM accounts WHERE id = @id FOR UPDATE (row lock)
   b. Check: balance >= amount
   c. UPDATE accounts SET balance = balance - amount
   d. INSERT INTO transactions (type='withdrawal', ...)
   e. COMMIT
6. Release Lock
7. Update Redis Cache: balance:{account_id}
8. Push Real-time: SignalR → "balance-updated" event
9. Async (RabbitMQ):
   - Send SMS/Email notification
   - Write audit log
   - Fraud detection check
```

---

## Phase 3: Redis Strategy

```
Redis ใช้ทำ 5 อย่าง:

1. Balance Cache
   Key: "balance:{accountId}" → DECIMAL
   TTL: 5 minutes (invalidate on write)
   → อ่าน balance เร็ว ไม่ต้อง hit DB ทุกครั้ง

2. Distributed Lock
   Key: "lock:account:{accountId}"
   TTL: 10 seconds (auto-release)
   → ป้องกัน race condition (2 คนถอนเงินพร้อมกัน)

3. Rate Limiting
   Key: "ratelimit:{userId}:{endpoint}" → Counter
   → จำกัด 10 transactions/minute per user

4. Session / Token Blacklist
   Key: "session:{userId}" → JWT metadata
   Key: "blacklist:{jti}" → revoked tokens
   → Manage auth sessions

5. Real-time Pub/Sub
   Channel: "notifications:{userId}"
   → Push balance updates, transaction alerts via SignalR
```

---

## Phase 4: Next.js Frontend

```
Pages:
├── / (Landing page)
├── /login, /register
├── /dashboard               ← ยอดเงิน real-time, กราฟ, recent transactions
├── /accounts                ← รายการบัญชี
├── /accounts/[id]           ← รายละเอียด + statement
├── /transfer                ← โอนเงิน (form + PIN confirmation)
├── /deposit                 ← ฝากเงิน
├── /withdraw                ← ถอนเงิน
├── /transactions            ← ประวัติ transactions (infinite scroll)
├── /settings                ← ตั้งค่า profile, เปลี่ยน PIN, limits
└── /admin/*                 ← Admin panel (role-based)

Tech:
- Next.js 15 (App Router, Server Components)
- Tailwind CSS + shadcn/ui
- React Query (TanStack Query) → API state management
- SignalR client → real-time balance updates
- Zustand → client state
- React Hook Form + Zod → form validation
```

---

## Phase 5: Load Balancing & Scaling

```
Load Balancer (Nginx):
├── Round-robin → Next.js instances (3+)
├── Least-connections → API instances (3+)
├── Health checks → /health endpoint
├── SSL Termination
├── WebSocket support (SignalR)
└── Rate limiting (first layer)

Scaling Strategy:
├── API: Horizontal scaling (stateless, add more instances)
├── DB: Primary + Read Replicas (write to primary, read from replicas)
├── Redis: Redis Cluster (3+ nodes, sharding)
├── Next.js: PM2 cluster mode or Docker replicas
└── Background Jobs: Separate worker instances
```

---

## ลำดับการพัฒนา (Sprint Plan)

### Sprint 1 (Week 1-2): Foundation
- [ ] ออกแบบ Database schema + Migrations
- [ ] Setup ASP.NET Core project (Clean Architecture)
- [ ] User registration + Login (JWT + Refresh Token)
- [ ] Account CRUD
- [ ] Basic Next.js layout + Auth pages

### Sprint 2 (Week 3-4): Core Banking
- [ ] Deposit logic (with DB transaction + locking)
- [ ] Withdrawal logic (with balance check + daily limit)
- [ ] Transfer logic (atomic debit + credit)
- [ ] Transaction history API
- [ ] Next.js: Dashboard + Transaction pages

### Sprint 3 (Week 5-6): Redis + Real-time
- [ ] Redis cache สำหรับ balance
- [ ] Distributed lock สำหรับ transactions
- [ ] Rate limiting
- [ ] SignalR real-time notifications
- [ ] Next.js: Real-time balance updates

### Sprint 4 (Week 7-8): Security & Production
- [ ] PIN verification สำหรับ transactions
- [ ] Fraud detection (unusual amount, frequency)
- [ ] Audit logging ทุก action
- [ ] Input validation + security hardening
- [ ] Unit + Integration tests

### Sprint 5 (Week 9-10): Scale & Deploy
- [ ] Docker + docker-compose
- [ ] Nginx load balancer config
- [ ] DB read replicas
- [ ] Redis Cluster
- [ ] CI/CD pipeline
- [ ] Monitoring (Prometheus + Grafana)
- [ ] Load testing (k6)

---

## Security Checklist

```
Authentication & Authorization:
☐ JWT + Refresh Token (short-lived access, long-lived refresh)
☐ PIN verification สำหรับทุก financial transaction
☐ 2FA (OTP via SMS/Email) สำหรับ large transactions
☐ Account lockout หลัง 5 failed attempts
☐ IP whitelisting สำหรับ admin

Data Protection:
☐ Encrypt sensitive data at rest (national ID, etc.)
☐ HTTPS everywhere (TLS 1.3)
☐ ไม่เก็บ plain text passwords/PINs
☐ Hash national ID (ค้นหาด้วย hash)
☐ Mask account numbers ใน logs

Transaction Security:
☐ Distributed lock ป้องกัน double-spending
☐ Idempotency key ป้องกัน duplicate transactions
☐ Rate limiting per user per endpoint
☐ Daily transaction limits
☐ Fraud detection (unusual patterns)
☐ Transaction reversal workflow

Infrastructure:
☐ WAF (Web Application Firewall)
☐ DDoS protection
☐ Database encryption at rest
☐ Audit log ทุก action (immutable)
☐ Regular security audits
```

---

## Phase 6: CI/CD Pipeline (GitHub Actions)

### Pipeline Flow

```
Developer Push → GitHub Actions:
┌────────┐   ┌──────┐   ┌──────────┐   ┌─────────┐   ┌───────────┐
│  Push  │ → │Build │ → │  Test    │ → │ Docker  │ → │  Deploy   │
│  Code  │   │.NET  │   │Unit+Int. │   │Build+Push│  │ to Cloud  │
└────────┘   │Next.js│  └──────────┘   └─────────┘   └───────────┘
              └──────┘
```

### CI: Build + Test

```yaml
# .github/workflows/ci.yml
name: CI

on:
  push:
    branches: [main, develop]
  pull_request:
    branches: [main]

jobs:
  # ===== Backend (.NET) =====
  backend:
    runs-on: ubuntu-latest
    services:
      postgres:
        image: postgres:16
        env:
          POSTGRES_DB: banking_test
          POSTGRES_USER: test
          POSTGRES_PASSWORD: test123
        ports: ["5432:5432"]
      redis:
        image: redis:7-alpine
        ports: ["6379:6379"]

    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with: { dotnet-version: '9.0.x' }

      - run: dotnet restore
      - run: dotnet build -c Release --no-restore
      - run: dotnet test -c Release --no-restore --logger trx
        env:
          ConnectionStrings__DefaultConnection: "Host=localhost;Database=banking_test;Username=test;Password=test123"
          ConnectionStrings__Redis: "localhost:6379"

      - run: dotnet publish src/Banking.Api -c Release -o ./publish-api

      - uses: actions/upload-artifact@v4
        with: { name: api-build, path: ./publish-api }

  # ===== Frontend (Next.js) =====
  frontend:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-node@v4
        with: { node-version: '20', cache: 'npm', cache-dependency-path: 'frontend/package-lock.json' }

      - run: cd frontend && npm ci
      - run: cd frontend && npm run lint
      - run: cd frontend && npm run build
      - run: cd frontend && npm test -- --coverage

      - uses: actions/upload-artifact@v4
        with: { name: frontend-build, path: frontend/.next }
```

### CD: Docker Build + Push + Deploy

```yaml
# .github/workflows/cd.yml
name: CD - Deploy

on:
  push:
    branches: [main]

jobs:
  deploy:
    runs-on: ubuntu-latest
    needs: [backend, frontend]  # ต้อง CI ผ่านก่อน

    steps:
      - uses: actions/checkout@v4

      # Build & Push Docker images
      - uses: docker/login-action@v3
        with:
          registry: ghcr.io
          username: ${{ github.actor }}
          password: ${{ secrets.GITHUB_TOKEN }}

      - uses: docker/build-push-action@v5
        with:
          context: .
          file: ./src/Banking.Api/Dockerfile
          push: true
          tags: ghcr.io/${{ github.repository }}/api:${{ github.sha }}

      - uses: docker/build-push-action@v5
        with:
          context: ./frontend
          file: ./frontend/Dockerfile
          push: true
          tags: ghcr.io/${{ github.repository }}/frontend:${{ github.sha }}

      # Deploy to Railway / Render / Fly.io
      - name: Deploy API to Railway
        run: |
          railway up --service api --detach
        env:
          RAILWAY_TOKEN: ${{ secrets.RAILWAY_TOKEN }}

      - name: Deploy Frontend to Vercel
        uses: amondnet/vercel-action@v25
        with:
          vercel-token: ${{ secrets.VERCEL_TOKEN }}
          vercel-org-id: ${{ secrets.VERCEL_ORG_ID }}
          vercel-project-id: ${{ secrets.VERCEL_PROJECT_ID }}
          vercel-args: '--prod'
          working-directory: ./frontend
```

---

## Phase 7: Deploy ฟรีบน Cloud (Portfolio)

### แผน Deploy ฟรี 100%

```
┌─────────────────────────────────────────────────────────────────┐
│                    FREE CLOUD DEPLOYMENT                        │
│                                                                 │
│  Frontend (Next.js)     → Vercel (Free tier)                   │
│                           ✅ ฟรี 100GB bandwidth/เดือน         │
│                           ✅ Auto SSL, CDN ทั่วโลก              │
│                           ✅ Preview deployments per PR         │
│                                                                 │
│  Backend (ASP.NET Core) → Railway (Free $5/เดือน)              │
│                           หรือ Render (Free 750 hrs/เดือน)      │
│                           หรือ Fly.io (Free 3 shared VMs)      │
│                                                                 │
│  Database (PostgreSQL)  → Neon (Free 0.5GB)                    │
│                           หรือ Supabase (Free 500MB)            │
│                           หรือ Railway PostgreSQL               │
│                                                                 │
│  Redis                  → Upstash Redis (Free 10,000 cmd/day)  │
│                           หรือ Railway Redis                    │
│                                                                 │
│  Message Broker         → CloudAMQP (Free — Little Lemur plan) │
│                           RabbitMQ managed                      │
│                                                                 │
│  Monitoring             → Better Stack (Free — logs + uptime)  │
│                           หรือ Grafana Cloud (Free tier)        │
│                                                                 │
│  Docker Registry        → GitHub Container Registry (GHCR)     │
│                           ✅ ฟรีสำหรับ public repos             │
│                                                                 │
│  CI/CD                  → GitHub Actions                        │
│                           ✅ ฟรี 2,000 minutes/เดือน            │
└─────────────────────────────────────────────────────────────────┘
```

### Option A: Vercel + Railway (แนะนำ! — ง่ายที่สุด)

```
Setup:
1. Frontend → Vercel
   - เชื่อม GitHub repo → auto deploy ทุก push
   - ตั้ง Environment Variables: NEXT_PUBLIC_API_URL

2. Backend → Railway
   - สร้าง project → Add service from GitHub
   - Railway จะ detect Dockerfile อัตโนมัติ
   - ตั้ง Environment Variables: ConnectionStrings, JWT keys, etc.

3. Database → Railway PostgreSQL
   - Add PostgreSQL plugin → ได้ connection string ทันที
   - Railway จัดการ backups ให้

4. Redis → Upstash
   - สร้าง Redis database ที่ upstash.com
   - ได้ REST API + connection string
   - Free: 10,000 commands/day (พอสำหรับ demo)

ค่าใช้จ่าย: $0/เดือน (within free tiers)
```

### Option B: Vercel + Render (ฟรีมากกว่า)

```
1. Frontend → Vercel (เหมือน Option A)
2. Backend → Render (Free Web Service)
   - ⚠️ Free tier: sleep หลัง 15 นาทีไม่มี traffic
   - ⚠️ Cold start ~30 วินาที (ใช้ demo ได้ แต่ไม่เหมาะ production)
3. Database → Neon PostgreSQL (Free 0.5GB)
   - Serverless PostgreSQL — auto sleep
4. Redis → Upstash Redis (Free)
```

### Option C: Fly.io (ควบคุมได้มากสุด)

```
1. Frontend → Vercel
2. Backend → Fly.io
   - fly launch → auto detect Dockerfile
   - Free: 3 shared VMs, 256MB RAM each
   - ได้ custom domain + SSL ฟรี
3. Database → Fly.io PostgreSQL (Free single node)
4. Redis → Fly.io Redis (Free single node)

ข้อดี: ทุกอย่างอยู่ที่เดียว, ใกล้ Singapore region
```

### Dockerfile สำหรับ Deploy

```dockerfile
# src/Banking.Api/Dockerfile
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["src/Banking.Api/Banking.Api.csproj", "Banking.Api/"]
COPY ["src/Banking.Domain/Banking.Domain.csproj", "Banking.Domain/"]
COPY ["src/Banking.Application/Banking.Application.csproj", "Banking.Application/"]
COPY ["src/Banking.Infrastructure/Banking.Infrastructure.csproj", "Banking.Infrastructure/"]
RUN dotnet restore "Banking.Api/Banking.Api.csproj"
COPY src/ .
RUN dotnet publish "Banking.Api/Banking.Api.csproj" -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
RUN adduser --disabled-password appuser && chown -R appuser /app
USER appuser
COPY --from=build /app .
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "Banking.Api.dll"]
```

```dockerfile
# frontend/Dockerfile
FROM node:20-alpine AS build
WORKDIR /app
COPY package*.json ./
RUN npm ci
COPY . .
RUN npm run build

FROM node:20-alpine
WORKDIR /app
COPY --from=build /app/.next/standalone ./
COPY --from=build /app/.next/static ./.next/static
COPY --from=build /app/public ./public
EXPOSE 3000
ENV PORT=3000
CMD ["node", "server.js"]
```

### Railway Deploy Config

```toml
# railway.toml (ใน root ของ backend project)
[build]
builder = "dockerfile"
dockerfilePath = "src/Banking.Api/Dockerfile"

[deploy]
healthcheckPath = "/health"
healthcheckTimeout = 30
restartPolicyType = "on_failure"
restartPolicyMaxRetries = 3
```

### Environment Variables (ต้องตั้งบน Cloud)

```
# Backend (Railway/Render/Fly.io)
ConnectionStrings__DefaultConnection=Host=xxx;Database=banking;Username=xxx;Password=xxx;SSL Mode=Require
ConnectionStrings__Redis=rediss://default:xxx@xxx.upstash.io:6379
Jwt__Key=your-super-secret-key-at-least-32-chars
Jwt__Issuer=banking-api
Jwt__Audience=banking-frontend
ASPNETCORE_ENVIRONMENT=Production

# Frontend (Vercel)
NEXT_PUBLIC_API_URL=https://banking-api.railway.app
NEXT_PUBLIC_SIGNALR_URL=https://banking-api.railway.app/hubs/notifications
```

### ขั้นตอน Deploy ทีละ Step

```
Step 1: Push code ขึ้น GitHub (public repo — สำหรับ portfolio)

Step 2: Deploy Database
  → สมัคร Neon (neon.tech) → สร้าง PostgreSQL → copy connection string
  → หรือ Railway → Add PostgreSQL plugin

Step 3: Deploy Redis
  → สมัคร Upstash (upstash.com) → สร้าง Redis → copy connection string

Step 4: Deploy Backend
  → สมัคร Railway (railway.app) → New Project → Deploy from GitHub
  → ตั้ง environment variables → Railway build + deploy อัตโนมัติ
  → ได้ URL: https://banking-api-xxx.railway.app

Step 5: Deploy Frontend
  → สมัคร Vercel (vercel.com) → Import GitHub repo
  → ตั้ง NEXT_PUBLIC_API_URL = URL จาก Step 4
  → Vercel build + deploy อัตโนมัติ
  → ได้ URL: https://banking-app.vercel.app

Step 6: ตั้ง Custom Domain (Optional)
  → ซื้อ domain (Cloudflare, Namecheap)
  → Point DNS → Vercel (frontend) + Railway (API)
  → banking.yourdomain.com → Vercel
  → api.yourdomain.com → Railway

Step 7: Setup CI/CD
  → GitHub Actions ทำงานอัตโนมัติเมื่อ push
  → Test → Build → Deploy
```

### Portfolio README Template

```markdown
# 🏦 Banking System — Full-Stack Web Application

## Live Demo
- 🌐 Frontend: https://banking-app.vercel.app
- 🔗 API: https://banking-api.railway.app/swagger
- 📊 Monitoring: https://banking-grafana.xxx

## Tech Stack
- **Frontend:** Next.js 15, React, Tailwind CSS, SignalR
- **Backend:** ASP.NET Core 9, Clean Architecture, CQRS + MediatR
- **Database:** PostgreSQL + Redis
- **Real-time:** SignalR WebSocket
- **CI/CD:** GitHub Actions → Docker → Railway + Vercel
- **Security:** JWT, PIN verification, Rate Limiting, Distributed Lock

## Features
- ✅ User Registration + KYC verification
- ✅ Multi-account management (Savings, Checking)
- ✅ Deposit / Withdrawal / Transfer
- ✅ Real-time balance updates (SignalR)
- ✅ Transaction history + Statement generation
- ✅ Fraud detection & Rate limiting
- ✅ Admin dashboard
- ✅ Comprehensive audit logging

## Architecture
[แปะ architecture diagram]

## How to Run Locally
[docker-compose up instructions]
```

---

## Verification / Testing Strategy

```
1. Unit Tests: ทุก business logic (Deposit, Withdraw, Transfer, Validators)
2. Integration Tests: API endpoints + real DB (TestContainers)
3. Load Test: k6 — 10,000 concurrent users, 1,000 TPS
4. Security Test: OWASP ZAP scan
5. Chaos Test: Kill API instance → verify load balancer failover
6. Concurrency Test: 100 users ถอนเงินจากบัญชีเดียวกันพร้อมกัน → balance ถูกต้อง
```
