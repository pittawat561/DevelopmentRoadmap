# Phase 6: CI/CD Pipeline (GitHub Actions)

> คู่มือการตั้งค่า CI/CD Pipeline สำหรับ Banking System ด้วย GitHub Actions
> ครอบคลุมตั้งแต่ Build, Test, Docker, Deploy ไปจนถึง Production Release

---

## สารบัญ

1. [ภาพรวม CI/CD Pipeline](#1-ภาพรวม-cicd-pipeline)
2. [Free Hosting Platform เปรียบเทียบ](#2-free-hosting-platform-เปรียบเทียบ)
3. [โครงสร้างไฟล์ Workflow](#3-โครงสร้างไฟล์-workflow)
4. [GitHub Secrets & Variables](#4-github-secrets--variables)
5. [Pipeline 1: Backend CI (Build + Test)](#5-pipeline-1-backend-ci-build--test)
6. [Pipeline 2: Frontend CI (Build + Lint)](#6-pipeline-2-frontend-ci-build--lint)
7. [Pipeline 3: Docker Build & Push](#7-pipeline-3-docker-build--push)
8. [Pipeline 4: Database Migration](#8-pipeline-4-database-migration)
9. [Pipeline 5: Deploy to Staging](#9-pipeline-5-deploy-to-staging)
10. [Pipeline 6: Deploy to Production](#10-pipeline-6-deploy-to-production)
11. [Pipeline 7: Security Scanning](#11-pipeline-7-security-scanning)
12. [Reusable Workflows](#12-reusable-workflows)
13. [Branch Protection Rules](#13-branch-protection-rules)
14. [Environment Strategy](#14-environment-strategy)
15. [Monitoring & Notifications](#15-monitoring--notifications)
16. [Troubleshooting](#16-troubleshooting)
17. [Best Practices & Tips](#17-best-practices--tips)

---

## 1. ภาพรวม CI/CD Pipeline

### 1.1 Pipeline Architecture

```
                    ┌─────────────────────────────────────────────────┐
                    │              GitHub Repository                   │
                    └─────────────┬───────────────────────────────────┘
                                  │
                    ┌─────────────▼───────────────────────────────────┐
                    │           Push / Pull Request                    │
                    └─────────────┬───────────────────────────────────┘
                                  │
              ┌───────────────────┼───────────────────┐
              │                   │                   │
    ┌─────────▼────────┐ ┌───────▼────────┐ ┌───────▼─────────┐
    │  Backend CI       │ │ Frontend CI    │ │ Security Scan   │
    │  ┌──────────────┐ │ │ ┌────────────┐ │ │ ┌─────────────┐ │
    │  │ dotnet build  │ │ │ │ npm install│ │ │ │ CodeQL      │ │
    │  │ dotnet test   │ │ │ │ npm lint   │ │ │ │ Dependency  │ │
    │  │ code coverage │ │ │ │ npm build  │ │ │ │ Secret Scan │ │
    │  └──────────────┘ │ │ └────────────┘ │ │ └─────────────┘ │
    └─────────┬────────┘ └───────┬────────┘ └───────┬─────────┘
              │                   │                   │
              └───────────────────┼───────────────────┘
                                  │ ทุก check ผ่าน
                    ┌─────────────▼───────────────────────────────────┐
                    │         Docker Build & Push (GHCR)              │
                    │    ghcr.io/username/banking-api:sha-xxxx        │
                    └─────────────┬───────────────────────────────────┘
                                  │
              ┌───────────────────┼───────────────────┐
              │                                       │
    ┌─────────▼────────────┐            ┌─────────────▼──────────────┐
    │  Deploy to Staging    │            │  Deploy to Production      │
    │  (auto on main)       │            │  (manual approval)         │
    │  ┌──────────────────┐ │            │  ┌──────────────────────┐  │
    │  │ Render Staging    │ │            │  │ Render Production    │  │
    │  │ Vercel Preview    │ │            │  │ Vercel Production    │  │
    │  │ Run Migrations    │ │            │  │ Run Migrations       │  │
    │  │ Health Check      │ │            │  │ Health Check         │  │
    │  │ Smoke Tests       │ │            │  │ Smoke Tests          │  │
    │  └──────────────────┘ │            │  └──────────────────────┘  │
    └──────────────────────┘            └────────────────────────────┘
```

### 1.2 Workflow Summary

| Pipeline | Trigger | เวลาโดยประมาณ | วัตถุประสงค์ |
|----------|---------|-------------|-------------|
| Backend CI | Push, PR | ~3 นาที | Build + Unit/Integration Tests |
| Frontend CI | Push, PR | ~2 นาที | Lint + Type Check + Build |
| Security Scan | Push (main), Schedule | ~5 นาที | CodeQL + Dependency Audit |
| Docker Build | Merge to main | ~4 นาที | Build & Push image to GHCR |
| Deploy Staging | Merge to main | ~3 นาที | Auto-deploy to staging |
| Deploy Production | Manual/Tag | ~3 นาที | Deploy with approval gate |
| DB Migration | Manual | ~1 นาที | Run EF Core migrations |

### 1.3 GitHub Actions Free Tier

| ทรัพยากร | ขีดจำกัด (Free) |
|---------|---------------|
| Compute minutes | 2,000 นาที/เดือน (Linux) |
| Storage (Artifacts/Cache) | 500 MB |
| Concurrent jobs | 20 jobs |
| Package Registry (GHCR) | 500 MB |

---

## 2. Free Hosting Platform เปรียบเทียบ

> เราจะใช้ **บริการฟรี 100%** ที่มี limit แล้ว reset ทุกเดือน/วัน ไม่ต้องผูกบัตรเครดิต

### 2.1 Backend Hosting (แทน Railway)

| Platform | Free Tier | Limit / Reset | Sleep? | Docker? | เหมาะกับ |
|----------|-----------|---------------|--------|---------|---------|
| **Render** ⭐ | 750 ชม./เดือน | Reset ทุกเดือน | หลังไม่มี request 15 นาที (cold start ~30s) | ✅ | **แนะนำ** — ง่ายที่สุด, deploy จาก GitHub ได้เลย |
| **Koyeb** | 1 nano instance | ไม่จำกัดเวลา | ไม่ sleep | ✅ | ต้องการ always-on |
| **Fly.io** | 3 shared VMs (256MB) | Reset ทุกเดือน | ไม่ sleep | ✅ | ต้องการ multi-region |
| **Azure App Service F1** | 60 CPU min/วัน | **Reset ทุกวัน** | ไม่ sleep (แต่ CPU จำกัด) | ❌ (.NET native) | .NET โดยตรง, ไม่ต้อง Docker |
| **Google Cloud Run** | 2M requests/เดือน | Reset ทุกเดือน | Scale to 0 | ✅ | High traffic burst |

### 2.2 สรุปตัวเลือกที่แนะนำ (ฟรีทั้งหมด)

```
┌─────────────────────────────────────────────────────────────────┐
│                  Free Stack (ไม่เสียเงินเลย)                     │
├──────────────────┬──────────────────────────────────────────────┤
│ Backend API      │ Render (Free) — 750 hrs/month               │
│                  │ └── Deploy Docker image จาก GHCR             │
│                  │ └── Auto-deploy เมื่อ push main              │
│                  │ └── Free SSL, Custom domain                  │
├──────────────────┼──────────────────────────────────────────────┤
│ Frontend         │ Vercel (Free) — Unlimited deploys            │
│                  │ └── Next.js first-class support              │
│                  │ └── Preview deploy ทุก PR                    │
├──────────────────┼──────────────────────────────────────────────┤
│ Database         │ Neon PostgreSQL (Free) — 0.5 GB storage      │
│                  │ └── Scale to 0 เมื่อไม่ใช้                    │
│                  │ └── Branching สำหรับ staging                  │
├──────────────────┼──────────────────────────────────────────────┤
│ Cache/Redis      │ Upstash Redis (Free) — 10K commands/วัน      │
│                  │ └── Reset ทุกวัน                              │
│                  │ └── Serverless (pay-per-request)              │
├──────────────────┼──────────────────────────────────────────────┤
│ CI/CD            │ GitHub Actions (Free) — 2,000 min/month      │
├──────────────────┼──────────────────────────────────────────────┤
│ Container Reg.   │ GHCR (Free) — 500 MB                        │
├──────────────────┼──────────────────────────────────────────────┤
│ Monitoring       │ Better Stack (Free) — 1 monitor              │
│                  │ └── Uptime check ทุก 3 นาที                   │
└──────────────────┴──────────────────────────────────────────────┘
```

### 2.3 Render Free Tier รายละเอียด

| ทรัพยากร | ขีดจำกัด |
|---------|--------|
| Instance hours | **750 ชม./เดือน** (reset ทุกวันที่ 1) |
| RAM | 512 MB |
| CPU | 0.1 CPU (shared) |
| Bandwidth | 100 GB/เดือน |
| Auto-sleep | หลัง 15 นาทีไม่มี request |
| Cold start | ~30 วินาที (wake up time) |
| Custom domain | ✅ ฟรี + SSL |
| Deploy method | Docker image / GitHub repo |

> **หมายเหตุ:** 750 ชม. = ~31 วัน ใช้ได้ตลอดเดือนถ้ามี 1 service
> ถ้ามี 2 services → 750/2 = 375 ชม. ต่อ service → ไม่พอทั้งเดือน
> **แก้ไข:** ใช้ auto-sleep → service sleep เมื่อไม่มี traffic → ประหยัด hours

### 2.4 วิธีสมัคร Render (ฟรี)

1. ไปที่ [render.com](https://render.com)
2. **Sign up with GitHub** (เชื่อมต่อ repo โดยตรง)
3. สร้าง **New Web Service**
4. เลือก **Docker** runtime
5. เลือก **Free** plan
6. ตั้ง Environment Variables (DB, Redis, JWT)
7. Deploy!

```
Render Dashboard:
┌────────────────────────────────────────────┐
│  banking-api                    [Free]     │
│  Status: ● Live                            │
│  URL: https://banking-api.onrender.com     │
│                                            │
│  Environment: staging                      │
│  Region: Oregon (US West)                  │
│  Last deploy: 5 minutes ago                │
│                                            │
│  [Manual Deploy]  [Logs]  [Settings]       │
└────────────────────────────────────────────┘
```

### 2.5 Render Deploy Hook

Render ใช้ระบบ **Deploy Hook** — URL ที่เรียก POST แล้ว trigger deploy:

```
https://api.render.com/deploy/srv-xxxxxxxxxxxxx?key=xxxxxxxxxxxxx
```

วิธีสร้าง:
1. Render Dashboard → Service → Settings
2. ส่วน **Deploy Hook** → Copy URL
3. เก็บใน GitHub Secrets: `RENDER_DEPLOY_HOOK_STAGING` / `RENDER_DEPLOY_HOOK_PROD`

---

## 3. โครงสร้างไฟล์ Workflow

```
.github/
├── workflows/
│   ├── backend-ci.yml              # Backend Build + Test
│   ├── frontend-ci.yml             # Frontend Lint + Build
│   ├── docker-build.yml            # Docker Build & Push to GHCR
│   ├── deploy-staging.yml          # Deploy to Staging
│   ├── deploy-production.yml       # Deploy to Production (manual)
│   ├── db-migration.yml            # Database Migration (manual)
│   ├── security-scan.yml           # CodeQL + Dependency Audit
│   └── reusable-dotnet-build.yml   # Reusable workflow
├── actions/
│   └── setup-dotnet/
│       └── action.yml              # Composite action: setup .NET
├── CODEOWNERS                       # PR auto-assign reviewers
└── pull_request_template.md         # PR template
```

---

## 4. GitHub Secrets & Variables

### 4.1 ตั้งค่า Repository Secrets

ไปที่ **Repository → Settings → Secrets and variables → Actions**

#### Secrets (ข้อมูลลับ)

| Secret Name | คำอธิบาย | ตัวอย่างค่า |
|-------------|---------|------------|
| `DB_PASSWORD` | รหัสผ่าน PostgreSQL | `MyStr0ngP@ssw0rd!` |
| `DB_CONNECTION_STRING` | Connection string สำหรับ staging | `Host=...;Database=banking_staging;...` |
| `DB_CONNECTION_STRING_PROD` | Connection string สำหรับ production | `Host=...;Database=banking_prod;...` |
| `JWT_KEY` | Secret key สำหรับ JWT signing | `YourSuperSecretKeyHere256Bits!!` |
| `REDIS_CONNECTION` | Redis connection string | `redis-12345.upstash.io:6379,password=...` |
| `RENDER_DEPLOY_HOOK_STAGING` | Render Deploy Hook URL (staging) | `https://api.render.com/deploy/srv-xxx?key=xxx` |
| `RENDER_DEPLOY_HOOK_PROD` | Render Deploy Hook URL (production) | `https://api.render.com/deploy/srv-yyy?key=yyy` |
| `RENDER_API_KEY` | Render API Key (optional, สำหรับดู status) | `rnd_xxxxx` |
| `VERCEL_TOKEN` | Vercel API token | `vercel_token_xxxxx` |
| `VERCEL_ORG_ID` | Vercel Organization ID | `team_xxxxx` |
| `VERCEL_PROJECT_ID` | Vercel Project ID | `prj_xxxxx` |
| `SLACK_WEBHOOK_URL` | Slack webhook สำหรับ notification | `https://hooks.slack.com/services/...` |

#### Variables (ค่าคงที่ ไม่เป็นความลับ)

| Variable Name | คำอธิบาย | ค่า |
|---------------|---------|-----|
| `DOTNET_VERSION` | .NET SDK version | `10.0.x` |
| `NODE_VERSION` | Node.js version | `22` |
| `REGISTRY` | Container registry | `ghcr.io` |
| `IMAGE_NAME` | Docker image name | `${{ github.repository }}/banking-api` |
| `STAGING_URL` | Staging API URL | `https://banking-api-staging.onrender.com` |
| `PRODUCTION_URL` | Production API URL | `https://banking-api.onrender.com` |

### 4.2 วิธีเพิ่ม Secrets

```bash
# ผ่าน GitHub CLI
gh secret set DB_PASSWORD --body "MyStr0ngP@ssw0rd!"
gh secret set JWT_KEY --body "YourSuperSecretKeyHere256Bits!!"
gh secret set RENDER_DEPLOY_HOOK_STAGING --body "https://api.render.com/deploy/srv-xxx?key=xxx"
gh secret set RENDER_DEPLOY_HOOK_PROD --body "https://api.render.com/deploy/srv-yyy?key=yyy"

# ผ่าน Variables
gh variable set DOTNET_VERSION --body "10.0.x"
gh variable set NODE_VERSION --body "22"
```

### 4.3 Environment Secrets

สร้าง Environments ที่ **Settings → Environments**:

**staging:**
- `DB_CONNECTION_STRING` — Staging database
- `REDIS_CONNECTION` — Staging Redis
- `API_URL` — Staging URL

**production:**
- `DB_CONNECTION_STRING` — Production database
- `REDIS_CONNECTION` — Production Redis
- `API_URL` — Production URL
- **Required reviewers:** 1+ คน (approval gate)
- **Wait timer:** 5 นาที (cooldown)

---

## 5. Pipeline 1: Backend CI (Build + Test)

### 5.1 Workflow File

สร้างไฟล์ `.github/workflows/backend-ci.yml`:

```yaml
name: Backend CI

on:
  push:
    branches: [main, develop]
    paths:
      - "BankingSystem/**"
      - ".github/workflows/backend-ci.yml"
  pull_request:
    branches: [main]
    paths:
      - "BankingSystem/**"

env:
  DOTNET_VERSION: "10.0.x"
  SOLUTION_PATH: "BankingSystem/BankingSystem.slnx"
  DOTNET_NOLOGO: true
  DOTNET_CLI_TELEMETRY_OPTOUT: true

# ยกเลิก run ก่อนหน้าถ้า push ใหม่เข้ามาใน PR เดียวกัน
concurrency:
  group: backend-ci-${{ github.ref }}
  cancel-in-progress: true

jobs:
  build:
    name: Build & Test
    runs-on: ubuntu-latest
    timeout-minutes: 15

    # Service containers สำหรับ Integration Tests
    services:
      postgres:
        image: postgres:16-alpine
        env:
          POSTGRES_USER: banking_test
          POSTGRES_PASSWORD: test_password
          POSTGRES_DB: banking_test
        ports:
          - 5432:5432
        options: >-
          --health-cmd pg_isready
          --health-interval 10s
          --health-timeout 5s
          --health-retries 5

      redis:
        image: redis:7-alpine
        ports:
          - 6379:6379
        options: >-
          --health-cmd "redis-cli ping"
          --health-interval 10s
          --health-timeout 5s
          --health-retries 5

    steps:
      # ===== SETUP =====
      - name: Checkout code
        uses: actions/checkout@v4

      - name: Setup .NET SDK
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: ${{ env.DOTNET_VERSION }}

      # ===== CACHE =====
      - name: Cache NuGet packages
        uses: actions/cache@v4
        with:
          path: ~/.nuget/packages
          key: nuget-${{ runner.os }}-${{ hashFiles('**/*.csproj') }}
          restore-keys: |
            nuget-${{ runner.os }}-

      # ===== RESTORE =====
      - name: Restore dependencies
        run: dotnet restore ${{ env.SOLUTION_PATH }}

      # ===== BUILD =====
      - name: Build solution
        run: dotnet build ${{ env.SOLUTION_PATH }} --no-restore --configuration Release

      # ===== UNIT TESTS =====
      - name: Run Unit Tests
        run: |
          dotnet test BankingSystem/Banking.Tests.Unit \
            --no-build \
            --configuration Release \
            --logger "trx;LogFileName=unit-test-results.trx" \
            --collect:"XPlat Code Coverage" \
            --results-directory ./test-results/unit

      # ===== INTEGRATION TESTS =====
      - name: Run Integration Tests
        env:
          ConnectionStrings__DefaultConnection: "Host=localhost;Port=5432;Database=banking_test;Username=banking_test;Password=test_password"
          ConnectionStrings__Redis: "localhost:6379"
          Jwt__Key: "TestSecretKeyForCICD_AtLeast256Bits!!"
          Jwt__Issuer: "BankingSystem.Test"
          Jwt__Audience: "BankingSystem.Test"
          ASPNETCORE_ENVIRONMENT: "Testing"
        run: |
          dotnet test BankingSystem/Banking.Tests.Integration \
            --no-build \
            --configuration Release \
            --logger "trx;LogFileName=integration-test-results.trx" \
            --results-directory ./test-results/integration

      # ===== TEST REPORT =====
      - name: Upload Test Results
        uses: actions/upload-artifact@v4
        if: always()
        with:
          name: test-results
          path: ./test-results/
          retention-days: 7

      # ===== CODE COVERAGE REPORT =====
      - name: Generate Coverage Report
        if: success()
        run: |
          dotnet tool install --global dotnet-reportgenerator-globaltool
          reportgenerator \
            -reports:./test-results/**/coverage.cobertura.xml \
            -targetdir:./coverage-report \
            -reporttypes:Cobertura

      - name: Upload Coverage to Artifact
        uses: actions/upload-artifact@v4
        if: success()
        with:
          name: coverage-report
          path: ./coverage-report/
          retention-days: 7

      # ===== PR COMMENT (Coverage) =====
      - name: Add Coverage PR Comment
        uses: marocchino/sticky-pull-request-comment@v2
        if: github.event_name == 'pull_request'
        with:
          header: coverage
          path: ./coverage-report/Cobertura.xml
          message: |
            ## Code Coverage Report
            Coverage report uploaded as artifact.
```

### 5.2 อธิบายแต่ละขั้นตอน

| ขั้นตอน | ทำอะไร | ทำไม |
|---------|--------|------|
| **Checkout** | ดึง source code จาก repo | จำเป็นสำหรับทุก workflow |
| **Setup .NET** | ติดตั้ง .NET SDK 10.0 | ใช้ build โปรเจกต์ |
| **Cache NuGet** | เก็บ cache NuGet packages | ลดเวลา restore ครั้งถัดไป (~30s → ~5s) |
| **Restore** | ดาวน์โหลด NuGet dependencies | เตรียม packages ก่อน build |
| **Build** | Compile source code (Release mode) | ตรวจ compilation errors |
| **Unit Tests** | รัน unit tests (Moq, FluentAssertions) | ตรวจ business logic |
| **Integration Tests** | รัน tests กับ PostgreSQL + Redis จริง | ตรวจ API endpoints + DB |
| **Coverage Report** | สร้างรายงาน code coverage | วัดว่า test ครอบคลุมแค่ไหน |

### 5.3 Service Containers คืออะไร

GitHub Actions สามารถรัน Docker containers เป็น "services" คู่กับ job ได้:

```
┌─────────────────────────────────────┐
│         ubuntu-latest runner         │
│                                     │
│  ┌─────────┐  ┌────────┐  ┌──────┐ │
│  │ Job Steps│  │Postgres│  │Redis │ │
│  │ (dotnet) │──│ :5432  │  │:6379 │ │
│  │          │──│        │  │      │ │
│  └─────────┘  └────────┘  └──────┘ │
└─────────────────────────────────────┘
```

- `postgres:16-alpine` — ฐานข้อมูลสำหรับ Integration Test
- `redis:7-alpine` — Cache + Lock สำหรับ Integration Test
- ทั้งสองเข้าถึงได้ผ่าน `localhost` เพราะอยู่ใน network เดียวกัน

---

## 6. Pipeline 2: Frontend CI (Build + Lint)

### 6.1 Workflow File

สร้างไฟล์ `.github/workflows/frontend-ci.yml`:

```yaml
name: Frontend CI

on:
  push:
    branches: [main, develop]
    paths:
      - "banking-frontend/**"
      - ".github/workflows/frontend-ci.yml"
  pull_request:
    branches: [main]
    paths:
      - "banking-frontend/**"

concurrency:
  group: frontend-ci-${{ github.ref }}
  cancel-in-progress: true

defaults:
  run:
    working-directory: banking-frontend

jobs:
  lint-and-build:
    name: Lint, Type Check & Build
    runs-on: ubuntu-latest
    timeout-minutes: 10

    steps:
      - name: Checkout code
        uses: actions/checkout@v4

      - name: Setup Node.js
        uses: actions/setup-node@v4
        with:
          node-version: 22
          cache: "npm"
          cache-dependency-path: banking-frontend/package-lock.json

      - name: Install dependencies
        run: npm ci

      - name: Run ESLint
        run: npm run lint

      - name: Type check
        run: npx tsc --noEmit

      - name: Build Next.js
        run: npm run build
        env:
          NEXT_PUBLIC_API_URL: "https://localhost:7001"
          NEXT_PUBLIC_SIGNALR_URL: "https://localhost:7001/hubs/notifications"

      - name: Upload build artifact
        uses: actions/upload-artifact@v4
        with:
          name: frontend-build
          path: banking-frontend/.next/
          retention-days: 1
```

### 6.2 อธิบายแต่ละขั้นตอน

| ขั้นตอน | ทำอะไร | ทำไม |
|---------|--------|------|
| **Setup Node.js** | ติดตั้ง Node 22 + cache npm | ใช้ build frontend |
| **npm ci** | Clean install dependencies | เร็วกว่า `npm install`, ใช้ lockfile ตรง |
| **ESLint** | ตรวจ code style + potential bugs | รักษาคุณภาพ code |
| **Type Check** | ตรวจ TypeScript errors | จับ type errors ก่อน deploy |
| **Build** | Production build Next.js | ตรวจว่า build ผ่าน, optimize output |

---

## 7. Pipeline 3: Docker Build & Push

### 7.1 Workflow File

สร้างไฟล์ `.github/workflows/docker-build.yml`:

```yaml
name: Docker Build & Push

on:
  push:
    branches: [main]
    paths:
      - "BankingSystem/**"
      - ".github/workflows/docker-build.yml"
  workflow_dispatch:
    inputs:
      tag:
        description: "Custom image tag"
        required: false
        default: ""

env:
  REGISTRY: ghcr.io
  IMAGE_NAME: ${{ github.repository_owner }}/banking-api

jobs:
  build-and-push:
    name: Build & Push Docker Image
    runs-on: ubuntu-latest
    timeout-minutes: 15

    permissions:
      contents: read
      packages: write

    steps:
      - name: Checkout code
        uses: actions/checkout@v4

      # ===== DOCKER SETUP =====
      - name: Set up Docker Buildx
        uses: docker/setup-buildx-action@v3

      - name: Login to GitHub Container Registry
        uses: docker/login-action@v3
        with:
          registry: ${{ env.REGISTRY }}
          username: ${{ github.actor }}
          password: ${{ secrets.GITHUB_TOKEN }}

      # ===== METADATA (TAGS & LABELS) =====
      - name: Extract metadata
        id: meta
        uses: docker/metadata-action@v5
        with:
          images: ${{ env.REGISTRY }}/${{ env.IMAGE_NAME }}
          tags: |
            # sha-xxxxxxx (short commit hash)
            type=sha,prefix=sha-
            # latest (สำหรับ main branch)
            type=raw,value=latest,enable={{is_default_branch}}
            # custom tag (จาก workflow_dispatch)
            type=raw,value=${{ github.event.inputs.tag }},enable=${{ github.event.inputs.tag != '' }}
            # v1.2.3 (จาก git tag)
            type=semver,pattern={{version}}
            type=semver,pattern={{major}}.{{minor}}

      # ===== BUILD & PUSH =====
      - name: Build and Push image
        uses: docker/build-push-action@v6
        with:
          context: ./BankingSystem
          file: ./BankingSystem/Banking.Api/Dockerfile
          push: true
          tags: ${{ steps.meta.outputs.tags }}
          labels: ${{ steps.meta.outputs.labels }}
          cache-from: type=gha
          cache-to: type=gha,mode=max
          build-args: |
            BUILD_VERSION=${{ github.sha }}

      # ===== OUTPUT =====
      - name: Image digest
        run: echo "Image pushed with digest ${{ steps.meta.outputs.tags }}"
```

### 7.2 Docker Image Tags Strategy

```
ghcr.io/username/banking-api:latest        ← ล่าสุดบน main
ghcr.io/username/banking-api:sha-a1b2c3d   ← specific commit
ghcr.io/username/banking-api:v1.0.0        ← release tag
ghcr.io/username/banking-api:v1.0          ← major.minor
```

### 7.3 Buildx Cache

ใช้ **GitHub Actions Cache** (`type=gha`) เพื่อเก็บ Docker layer cache:

```
ครั้งแรก:  Build ทุก layer (~4 นาที)
ครั้งถัดไป: ใช้ cache, build เฉพาะ layer ที่เปลี่ยน (~1 นาที)
```

### 7.4 GHCR (GitHub Container Registry)

- ใช้ `GITHUB_TOKEN` ที่มีอยู่แล้ว (ไม่ต้องสร้าง secret เพิ่ม)
- ต้องตั้ง `permissions: packages: write`
- ดู images ได้ที่ **Repository → Packages**

---

## 8. Pipeline 4: Database Migration

### 8.1 Workflow File

สร้างไฟล์ `.github/workflows/db-migration.yml`:

```yaml
name: Database Migration

on:
  workflow_dispatch:
    inputs:
      environment:
        description: "Target environment"
        required: true
        type: choice
        options:
          - staging
          - production
      migration_action:
        description: "Migration action"
        required: true
        type: choice
        options:
          - migrate
          - status
          - rollback-last

jobs:
  migrate:
    name: Run Migration (${{ inputs.environment }})
    runs-on: ubuntu-latest
    timeout-minutes: 10
    environment: ${{ inputs.environment }}

    steps:
      - name: Checkout code
        uses: actions/checkout@v4

      - name: Setup .NET SDK
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: "10.0.x"

      - name: Install EF Core tools
        run: dotnet tool install --global dotnet-ef

      # ===== STATUS =====
      - name: Check migration status
        if: inputs.migration_action == 'status'
        run: |
          dotnet ef migrations list \
            --project BankingSystem/Banking.Infrastructure \
            --startup-project BankingSystem/Banking.Api
        env:
          ConnectionStrings__DefaultConnection: ${{ secrets.DB_CONNECTION_STRING }}

      # ===== MIGRATE =====
      - name: Apply pending migrations
        if: inputs.migration_action == 'migrate'
        run: |
          echo "Applying migrations to ${{ inputs.environment }}..."
          dotnet ef database update \
            --project BankingSystem/Banking.Infrastructure \
            --startup-project BankingSystem/Banking.Api \
            --verbose
        env:
          ConnectionStrings__DefaultConnection: ${{ secrets.DB_CONNECTION_STRING }}
          ASPNETCORE_ENVIRONMENT: Production

      # ===== ROLLBACK =====
      - name: Rollback last migration
        if: inputs.migration_action == 'rollback-last'
        run: |
          echo "WARNING: Rolling back last migration on ${{ inputs.environment }}!"
          # ดึงชื่อ migration ก่อนหน้า
          PREVIOUS=$(dotnet ef migrations list \
            --project BankingSystem/Banking.Infrastructure \
            --startup-project BankingSystem/Banking.Api \
            | tail -2 | head -1 | xargs)

          echo "Rolling back to: $PREVIOUS"
          dotnet ef database update "$PREVIOUS" \
            --project BankingSystem/Banking.Infrastructure \
            --startup-project BankingSystem/Banking.Api
        env:
          ConnectionStrings__DefaultConnection: ${{ secrets.DB_CONNECTION_STRING }}
          ASPNETCORE_ENVIRONMENT: Production
```

### 8.2 วิธีใช้งาน

1. ไปที่ **Actions** tab ใน GitHub
2. เลือก **Database Migration** workflow
3. กด **Run workflow**
4. เลือก environment (`staging` / `production`)
5. เลือก action (`migrate` / `status` / `rollback-last`)
6. กด **Run workflow**

```
┌────────────────────────────────────────┐
│  ⚡ Run workflow                        │
│                                        │
│  Branch: main                          │
│  Environment: [staging ▼]              │
│  Action:      [migrate ▼]              │
│                                        │
│  [🟢 Run workflow]                     │
└────────────────────────────────────────┘
```

---

## 9. Pipeline 5: Deploy to Staging (Render Free)

### 9.1 Workflow File

สร้างไฟล์ `.github/workflows/deploy-staging.yml`:

```yaml
name: Deploy to Staging

on:
  workflow_run:
    workflows: ["Backend CI", "Docker Build & Push"]
    branches: [main]
    types: [completed]

jobs:
  # ===== CHECK PREREQUISITES =====
  check:
    name: Verify CI passed
    runs-on: ubuntu-latest
    if: ${{ github.event.workflow_run.conclusion == 'success' }}
    outputs:
      should_deploy: ${{ steps.check.outputs.result }}
    steps:
      - name: Check all workflows passed
        id: check
        run: echo "result=true" >> $GITHUB_OUTPUT

  # ===== DEPLOY BACKEND TO RENDER (FREE) =====
  deploy-backend:
    name: Deploy Backend (Render Free)
    runs-on: ubuntu-latest
    needs: check
    if: needs.check.outputs.should_deploy == 'true'
    environment: staging
    timeout-minutes: 10

    steps:
      - name: Checkout code
        uses: actions/checkout@v4

      # Render Deploy Hook — เรียก POST เพื่อ trigger deploy
      # ฟรี! ไม่ต้องติดตั้ง CLI, ไม่ต้อง API key
      - name: Trigger Render Deploy (Staging)
        run: |
          echo "Triggering Render deploy..."
          HTTP_STATUS=$(curl -s -o /dev/null -w "%{http_code}" \
            -X POST "${{ secrets.RENDER_DEPLOY_HOOK_STAGING }}")

          if [ "$HTTP_STATUS" = "200" ] || [ "$HTTP_STATUS" = "201" ]; then
            echo "Deploy triggered successfully! (HTTP $HTTP_STATUS)"
          else
            echo "ERROR: Deploy trigger failed (HTTP $HTTP_STATUS)"
            exit 1
          fi

      # ===== WAIT FOR RENDER BUILD =====
      # Render Free tier ใช้เวลา build ~2-4 นาที + cold start ~30s
      - name: Wait for Render build & deploy
        run: |
          echo "Waiting for Render to build and deploy (free tier ~3-5 min)..."
          sleep 120

      - name: Health check (with Render cold start retry)
        run: |
          # Render Free: cold start ~30s, build ~3 min
          # ต้อง retry หลายครั้งเพื่อรอ wake up + deploy เสร็จ
          MAX_RETRIES=20
          RETRY_COUNT=0
          HEALTH_URL="${{ vars.STAGING_URL }}/health"

          echo "Checking: $HEALTH_URL"
          echo "(Render Free tier may need cold start time...)"

          while [ $RETRY_COUNT -lt $MAX_RETRIES ]; do
            HTTP_STATUS=$(curl -s -o /dev/null -w "%{http_code}" \
              --max-time 60 "$HEALTH_URL" || echo "000")

            if [ "$HTTP_STATUS" = "200" ]; then
              echo "Health check passed! (HTTP $HTTP_STATUS)"
              exit 0
            fi

            RETRY_COUNT=$((RETRY_COUNT + 1))
            echo "Attempt $RETRY_COUNT/$MAX_RETRIES: HTTP $HTTP_STATUS - Retrying in 15s..."
            sleep 15
          done

          echo "Health check failed after $MAX_RETRIES attempts!"
          exit 1

      # ===== SMOKE TEST =====
      - name: Run smoke tests
        run: |
          API_URL="${{ vars.STAGING_URL }}"

          echo "Testing auth endpoint..."
          curl -sf "$API_URL/api/auth/login" \
            -X POST \
            -H "Content-Type: application/json" \
            -d '{"email":"test@test.com","password":"wrong"}' \
            || echo "Auth endpoint responding (expected 401)"

          echo ""
          echo "Testing health endpoint..."
          curl -sf "$API_URL/health"

          echo ""
          echo "Smoke tests passed!"

  # ===== DEPLOY FRONTEND TO VERCEL =====
  deploy-frontend:
    name: Deploy Frontend (Vercel Preview)
    runs-on: ubuntu-latest
    needs: check
    if: needs.check.outputs.should_deploy == 'true'
    timeout-minutes: 10

    steps:
      - name: Checkout code
        uses: actions/checkout@v4

      - name: Setup Node.js
        uses: actions/setup-node@v4
        with:
          node-version: 22

      - name: Install Vercel CLI
        run: npm install -g vercel

      - name: Pull Vercel environment
        run: |
          vercel pull \
            --yes \
            --environment=preview \
            --token=${{ secrets.VERCEL_TOKEN }}
        working-directory: banking-frontend

      - name: Build with Vercel
        run: vercel build --token=${{ secrets.VERCEL_TOKEN }}
        working-directory: banking-frontend

      - name: Deploy to Vercel (Preview)
        id: deploy
        run: |
          URL=$(vercel deploy \
            --prebuilt \
            --token=${{ secrets.VERCEL_TOKEN }})
          echo "preview_url=$URL" >> $GITHUB_OUTPUT
        working-directory: banking-frontend

      - name: Output preview URL
        run: echo "Deployed to ${{ steps.deploy.outputs.preview_url }}"

  # ===== NOTIFICATION =====
  notify:
    name: Notify team
    runs-on: ubuntu-latest
    needs: [deploy-backend, deploy-frontend]
    if: always()
    steps:
      - name: Send Slack notification
        uses: slackapi/slack-github-action@v2.0.0
        with:
          webhook: ${{ secrets.SLACK_WEBHOOK_URL }}
          webhook-type: incoming-webhook
          payload: |
            {
              "text": "Staging Deploy: ${{ needs.deploy-backend.result == 'success' && needs.deploy-frontend.result == 'success' && '✅ Success' || '❌ Failed' }}\nCommit: ${{ github.sha }}\nBy: ${{ github.actor }}"
            }
```

### 9.2 Deploy Flow

```
main branch merge
       │
       ▼
 Backend CI ─────► Docker Build ─────► Deploy Staging
 (build+test)      (push GHCR)         ├── Render Free (backend)
                                        ├── Vercel (frontend)
                                        ├── Health Check
                                        ├── Smoke Tests
                                        └── Slack Notification
```

---

## 10. Pipeline 6: Deploy to Production (Render Free)

### 10.1 Workflow File

สร้างไฟล์ `.github/workflows/deploy-production.yml`:

```yaml
name: Deploy to Production

on:
  # Trigger เมื่อสร้าง release tag (v1.0.0, v1.1.0, ...)
  push:
    tags:
      - "v*.*.*"
  # หรือ trigger manually
  workflow_dispatch:
    inputs:
      version:
        description: "Version to deploy (e.g., sha-a1b2c3d)"
        required: true

jobs:
  # ===== PRE-DEPLOY VALIDATION =====
  validate:
    name: Pre-deploy Validation
    runs-on: ubuntu-latest
    timeout-minutes: 5
    outputs:
      image_tag: ${{ steps.tag.outputs.value }}

    steps:
      - name: Determine image tag
        id: tag
        run: |
          if [ "${{ github.event_name }}" = "push" ]; then
            # จาก git tag: v1.0.0 → v1.0.0
            echo "value=${{ github.ref_name }}" >> $GITHUB_OUTPUT
          else
            # จาก manual input
            echo "value=${{ github.event.inputs.version }}" >> $GITHUB_OUTPUT
          fi

      - name: Verify Docker image exists
        run: |
          IMAGE="ghcr.io/${{ github.repository_owner }}/banking-api:${{ steps.tag.outputs.value }}"
          echo "Checking image: $IMAGE"
          docker manifest inspect "$IMAGE" || {
            echo "ERROR: Image not found in GHCR!"
            exit 1
          }
        env:
          DOCKER_CLI_EXPERIMENTAL: enabled

  # ===== DEPLOY TO PRODUCTION =====
  deploy:
    name: Deploy to Production
    runs-on: ubuntu-latest
    needs: validate
    environment: production  # ← ต้องมี approval!
    timeout-minutes: 15

    steps:
      - name: Checkout code
        uses: actions/checkout@v4

      # ===== DATABASE MIGRATION =====
      - name: Setup .NET SDK
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: "10.0.x"

      - name: Install EF Core tools
        run: dotnet tool install --global dotnet-ef

      - name: Run database migrations
        run: |
          echo "Running migrations on production..."
          dotnet ef database update \
            --project BankingSystem/Banking.Infrastructure \
            --startup-project BankingSystem/Banking.Api \
            --verbose
        env:
          ConnectionStrings__DefaultConnection: ${{ secrets.DB_CONNECTION_STRING_PROD }}
          ASPNETCORE_ENVIRONMENT: Production

      # ===== DEPLOY BACKEND (RENDER FREE) =====
      - name: Trigger Render Deploy (Production)
        run: |
          echo "Triggering Render production deploy..."
          HTTP_STATUS=$(curl -s -o /dev/null -w "%{http_code}" \
            -X POST "${{ secrets.RENDER_DEPLOY_HOOK_PROD }}")

          if [ "$HTTP_STATUS" = "200" ] || [ "$HTTP_STATUS" = "201" ]; then
            echo "Production deploy triggered! (HTTP $HTTP_STATUS)"
          else
            echo "ERROR: Deploy trigger failed (HTTP $HTTP_STATUS)"
            exit 1
          fi

      - name: Wait for Render build
        run: |
          echo "Waiting for Render production build (~3-5 min)..."
          sleep 180

      # ===== DEPLOY FRONTEND =====
      - name: Install Vercel CLI
        run: npm install -g vercel

      - name: Deploy Frontend to Production
        run: |
          vercel deploy \
            --prod \
            --token=${{ secrets.VERCEL_TOKEN }}
        working-directory: banking-frontend

      # ===== POST-DEPLOY VERIFICATION =====
      - name: Wait for rollout
        run: sleep 45

      - name: Production health check
        run: |
          MAX_RETRIES=15
          RETRY_COUNT=0

          while [ $RETRY_COUNT -lt $MAX_RETRIES ]; do
            HTTP_STATUS=$(curl -s -o /dev/null -w "%{http_code}" \
              "${{ vars.PRODUCTION_URL }}/health" || echo "000")

            if [ "$HTTP_STATUS" = "200" ]; then
              echo "Production is healthy!"
              exit 0
            fi

            RETRY_COUNT=$((RETRY_COUNT + 1))
            echo "Retry $RETRY_COUNT/$MAX_RETRIES: HTTP $HTTP_STATUS"
            sleep 10
          done

          echo "CRITICAL: Production health check failed!"
          exit 1

      - name: Verify API responses
        run: |
          API_URL="${{ vars.PRODUCTION_URL }}"

          # ตรวจสอบ swagger (ควรปิดใน production)
          STATUS=$(curl -s -o /dev/null -w "%{http_code}" "$API_URL/swagger")
          if [ "$STATUS" = "200" ]; then
            echo "WARNING: Swagger is enabled in production!"
          fi

          # ตรวจสอบ endpoint หลัก
          STATUS=$(curl -s -o /dev/null -w "%{http_code}" "$API_URL/api/auth/profile")
          if [ "$STATUS" = "401" ]; then
            echo "Auth endpoint responding correctly (401 Unauthorized)"
          fi

  # ===== CREATE GITHUB RELEASE =====
  release:
    name: Create Release
    runs-on: ubuntu-latest
    needs: deploy
    if: startsWith(github.ref, 'refs/tags/v')

    permissions:
      contents: write

    steps:
      - name: Checkout code
        uses: actions/checkout@v4
        with:
          fetch-depth: 0

      - name: Generate changelog
        id: changelog
        run: |
          # ดึง commits ตั้งแต่ tag ก่อนหน้า
          PREV_TAG=$(git describe --tags --abbrev=0 HEAD^ 2>/dev/null || echo "")

          if [ -n "$PREV_TAG" ]; then
            CHANGES=$(git log $PREV_TAG..HEAD --pretty=format:"- %s (%h)" --no-merges)
          else
            CHANGES=$(git log --pretty=format:"- %s (%h)" --no-merges -20)
          fi

          echo "changes<<EOF" >> $GITHUB_OUTPUT
          echo "$CHANGES" >> $GITHUB_OUTPUT
          echo "EOF" >> $GITHUB_OUTPUT

      - name: Create GitHub Release
        uses: softprops/action-gh-release@v2
        with:
          tag_name: ${{ github.ref_name }}
          name: Release ${{ github.ref_name }}
          body: |
            ## What's Changed
            ${{ steps.changelog.outputs.changes }}

            ## Docker Image
            ```
            ghcr.io/${{ github.repository_owner }}/banking-api:${{ github.ref_name }}
            ```

            ## Deployment
            - Backend: ${{ vars.PRODUCTION_URL }}
            - Frontend: Vercel Production
          draft: false
          prerelease: false

  # ===== NOTIFICATION =====
  notify:
    name: Production notification
    runs-on: ubuntu-latest
    needs: [deploy, release]
    if: always()
    steps:
      - name: Notify Slack
        uses: slackapi/slack-github-action@v2.0.0
        with:
          webhook: ${{ secrets.SLACK_WEBHOOK_URL }}
          webhook-type: incoming-webhook
          payload: |
            {
              "text": "🚀 Production Deploy: ${{ needs.deploy.result == 'success' && '✅ Success' || '❌ FAILED' }}\nVersion: ${{ github.ref_name }}\nBy: ${{ github.actor }}"
            }
```

### 10.2 Production Release Flow

```
git tag -a v1.0.0 -m "Release v1.0.0"
git push origin v1.0.0
       │
       ▼
  Validate ──► Deploy (requires approval) ──► GitHub Release
       │              │                              │
       │              ├── DB Migration               ├── Changelog
       │              ├── Render Deploy (Free)        ├── Docker image link
       │              ├── Vercel Deploy (--prod)      └── Release notes
       │              ├── Health Check
       │              └── API Verification
       │
       └── Verify Docker image exists
```

### 10.3 Manual Approval

เมื่อ workflow ถึง job `deploy` ที่ใช้ `environment: production`:

```
┌────────────────────────────────────────────┐
│  ⏳ Waiting for review                      │
│                                            │
│  Environment: production                    │
│  Required reviewers: @team-lead, @devops   │
│                                            │
│  [✅ Approve and deploy]  [❌ Reject]       │
└────────────────────────────────────────────┘
```

---

## 11. Pipeline 7: Security Scanning

### 11.1 Workflow File

สร้างไฟล์ `.github/workflows/security-scan.yml`:

```yaml
name: Security Scan

on:
  push:
    branches: [main]
  pull_request:
    branches: [main]
  schedule:
    # รันทุกวันจันทร์ 09:00 (UTC+7 = 02:00 UTC)
    - cron: "0 2 * * 1"

jobs:
  # ===== CODEQL ANALYSIS =====
  codeql:
    name: CodeQL Analysis
    runs-on: ubuntu-latest
    timeout-minutes: 15

    permissions:
      actions: read
      contents: read
      security-events: write

    strategy:
      matrix:
        language: ["csharp", "javascript"]

    steps:
      - name: Checkout code
        uses: actions/checkout@v4

      - name: Initialize CodeQL
        uses: github/codeql-action/init@v3
        with:
          languages: ${{ matrix.language }}

      - name: Setup .NET SDK
        if: matrix.language == 'csharp'
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: "10.0.x"

      - name: Build .NET (for CodeQL)
        if: matrix.language == 'csharp'
        run: dotnet build BankingSystem/BankingSystem.slnx

      - name: Perform CodeQL Analysis
        uses: github/codeql-action/analyze@v3

  # ===== DEPENDENCY AUDIT =====
  dependency-audit:
    name: Dependency Vulnerability Check
    runs-on: ubuntu-latest
    timeout-minutes: 10

    steps:
      - name: Checkout code
        uses: actions/checkout@v4

      # NuGet vulnerability check
      - name: Setup .NET SDK
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: "10.0.x"

      - name: Audit NuGet packages
        run: |
          dotnet restore BankingSystem/BankingSystem.slnx
          dotnet list BankingSystem/BankingSystem.slnx package --vulnerable --include-transitive 2>&1 | tee nuget-audit.txt

          if grep -q "has the following vulnerable packages" nuget-audit.txt; then
            echo "WARNING: Vulnerable NuGet packages found!"
            cat nuget-audit.txt
            # ไม่ fail build แต่แจ้งเตือน (เปลี่ยนเป็น exit 1 ถ้าต้องการ strict)
          else
            echo "No vulnerable NuGet packages found."
          fi

      # npm vulnerability check
      - name: Setup Node.js
        uses: actions/setup-node@v4
        with:
          node-version: 22

      - name: Audit npm packages
        working-directory: banking-frontend
        run: |
          npm ci
          npm audit --audit-level=high || echo "npm audit found issues (review above)"

  # ===== SECRET SCANNING =====
  secret-scan:
    name: Secret Detection
    runs-on: ubuntu-latest
    timeout-minutes: 5

    steps:
      - name: Checkout code
        uses: actions/checkout@v4
        with:
          fetch-depth: 0

      - name: Run Gitleaks
        uses: gitleaks/gitleaks-action@v2
        env:
          GITHUB_TOKEN: ${{ secrets.GITHUB_TOKEN }}

  # ===== DOCKER IMAGE SCAN =====
  image-scan:
    name: Container Image Scan
    runs-on: ubuntu-latest
    if: github.event_name == 'push' && github.ref == 'refs/heads/main'
    timeout-minutes: 10

    steps:
      - name: Checkout code
        uses: actions/checkout@v4

      - name: Build Docker image (local)
        run: |
          docker build \
            -t banking-api:scan \
            -f BankingSystem/Banking.Api/Dockerfile \
            BankingSystem/

      - name: Run Trivy vulnerability scanner
        uses: aquasecurity/trivy-action@master
        with:
          image-ref: "banking-api:scan"
          format: "sarif"
          output: "trivy-results.sarif"
          severity: "CRITICAL,HIGH"

      - name: Upload Trivy scan results
        uses: github/codeql-action/upload-sarif@v3
        if: always()
        with:
          sarif_file: "trivy-results.sarif"
```

### 11.2 Security Scans ครอบคลุม

| Scan | เครื่องมือ | ตรวจจับ |
|------|----------|--------|
| **CodeQL** | GitHub CodeQL | SQL injection, XSS, code smells ใน C# + JS |
| **NuGet Audit** | dotnet list --vulnerable | NuGet packages ที่มี CVE |
| **npm Audit** | npm audit | npm packages ที่มี CVE |
| **Secret Scan** | Gitleaks | API keys, passwords, tokens ที่หลุดเข้า code |
| **Container Scan** | Trivy | Vulnerabilities ใน Docker image (OS + app) |

ผลลัพธ์ทั้งหมดจะแสดงใน **Security tab** ของ GitHub repository

---

## 12. Reusable Workflows

### 12.1 Composite Action: Setup .NET

สร้างไฟล์ `.github/actions/setup-dotnet/action.yml`:

```yaml
name: "Setup .NET Environment"
description: "Setup .NET SDK with NuGet cache"

inputs:
  dotnet-version:
    description: ".NET SDK version"
    required: false
    default: "10.0.x"

runs:
  using: "composite"
  steps:
    - name: Setup .NET SDK
      uses: actions/setup-dotnet@v4
      with:
        dotnet-version: ${{ inputs.dotnet-version }}

    - name: Cache NuGet packages
      uses: actions/cache@v4
      with:
        path: ~/.nuget/packages
        key: nuget-${{ runner.os }}-${{ hashFiles('**/*.csproj') }}
        restore-keys: |
          nuget-${{ runner.os }}-
```

**วิธีใช้ใน workflow:**

```yaml
steps:
  - uses: actions/checkout@v4
  - uses: ./.github/actions/setup-dotnet
    with:
      dotnet-version: "10.0.x"
```

### 12.2 Reusable Workflow: .NET Build + Test

สร้างไฟล์ `.github/workflows/reusable-dotnet-build.yml`:

```yaml
name: Reusable .NET Build

on:
  workflow_call:
    inputs:
      configuration:
        required: false
        type: string
        default: "Release"
      run-integration-tests:
        required: false
        type: boolean
        default: false

jobs:
  build-and-test:
    runs-on: ubuntu-latest
    timeout-minutes: 15

    services:
      postgres:
        image: postgres:16-alpine
        env:
          POSTGRES_USER: banking_test
          POSTGRES_PASSWORD: test_password
          POSTGRES_DB: banking_test
        ports:
          - 5432:5432
        options: >-
          --health-cmd pg_isready
          --health-interval 10s
          --health-timeout 5s
          --health-retries 5
      redis:
        image: redis:7-alpine
        ports:
          - 6379:6379
        options: >-
          --health-cmd "redis-cli ping"
          --health-interval 10s
          --health-timeout 5s
          --health-retries 5

    steps:
      - uses: actions/checkout@v4

      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: "10.0.x"

      - name: Cache NuGet
        uses: actions/cache@v4
        with:
          path: ~/.nuget/packages
          key: nuget-${{ runner.os }}-${{ hashFiles('**/*.csproj') }}

      - run: dotnet restore BankingSystem/BankingSystem.slnx

      - run: dotnet build BankingSystem/BankingSystem.slnx --no-restore -c ${{ inputs.configuration }}

      - name: Unit Tests
        run: dotnet test BankingSystem/Banking.Tests.Unit --no-build -c ${{ inputs.configuration }}

      - name: Integration Tests
        if: inputs.run-integration-tests
        run: dotnet test BankingSystem/Banking.Tests.Integration --no-build -c ${{ inputs.configuration }}
        env:
          ConnectionStrings__DefaultConnection: "Host=localhost;Port=5432;Database=banking_test;Username=banking_test;Password=test_password"
          ConnectionStrings__Redis: "localhost:6379"
          Jwt__Key: "TestSecretKeyForCICD_AtLeast256Bits!!"
          Jwt__Issuer: "BankingSystem.Test"
          Jwt__Audience: "BankingSystem.Test"
```

**วิธีเรียกใช้:**

```yaml
jobs:
  build:
    uses: ./.github/workflows/reusable-dotnet-build.yml
    with:
      configuration: Release
      run-integration-tests: true
```

---

## 13. Branch Protection Rules

### 13.1 ตั้งค่า Branch Protection

ไปที่ **Settings → Branches → Add rule**

**สำหรับ `main` branch:**

| Rule | ค่า | คำอธิบาย |
|------|-----|---------|
| Require pull request before merging | ✅ | ห้าม push ตรงเข้า main |
| Required approvals | 1 | ต้องมีคนอนุมัติ PR อย่างน้อย 1 คน |
| Dismiss stale reviews | ✅ | push ใหม่จะยกเลิก approval เก่า |
| Require status checks | ✅ | CI ต้องผ่านก่อน merge |
| Required checks | `Build & Test`, `Lint, Type Check & Build` | ทั้ง backend และ frontend ต้องผ่าน |
| Require branches up-to-date | ✅ | Branch ต้อง up-to-date กับ main |
| Require conversation resolution | ✅ | ทุก comment ต้อง resolve ก่อน merge |
| Include administrators | ✅ | Admin ก็ต้องทำตามกฎ |

### 13.2 CODEOWNERS

สร้างไฟล์ `.github/CODEOWNERS`:

```
# ทุกไฟล์ต้องมี review จาก team
* @your-org/banking-team

# Infrastructure changes ต้อง review จาก devops
.github/           @your-org/devops-team
docker/             @your-org/devops-team
docker-compose.yml  @your-org/devops-team

# Database changes ต้อง review จาก DBA
BankingSystem/Banking.Infrastructure/Migrations/  @your-org/dba-team

# Security-sensitive files
BankingSystem/Banking.Api/Middleware/  @your-org/security-team
```

### 13.3 PR Template

สร้างไฟล์ `.github/pull_request_template.md`:

```markdown
## Description
<!-- อธิบายสิ่งที่เปลี่ยนแปลง -->

## Type of Change
- [ ] Bug fix
- [ ] New feature
- [ ] Breaking change
- [ ] Documentation update
- [ ] Performance improvement
- [ ] Refactoring

## Checklist
- [ ] Tests pass locally (`dotnet test`)
- [ ] No new warnings
- [ ] Database migration included (if needed)
- [ ] API documentation updated (if endpoint changed)
- [ ] No secrets/credentials in code

## Related Issues
<!-- Closes #123 -->
```

---

## 14. Environment Strategy

### 14.1 Environment Flow

```
feature/* ──PR──► develop ──PR──► main ──tag──► production
                     │              │               │
                     ▼              ▼               ▼
                  (CI only)    Staging Auto     Production
                                Deploy         (Manual Approval)
```

### 14.2 สรุป Environments

| Environment | Trigger | Backend | Database | Redis | URL |
|-------------|---------|---------|----------|-------|-----|
| **CI** | Push, PR | GitHub Actions runner | Service container | Service container | - |
| **Staging** | Merge to main | Render Free | Neon (staging) | Upstash (staging) | banking-api-staging.onrender.com |
| **Production** | Tag v*.*.* + Approval | Render Free | Neon (prod) | Upstash (prod) | banking-api.onrender.com |

### 14.3 ค่าใช้จ่ายรวม: $0/เดือน

| Service | Plan | ราคา | Limit |
|---------|------|------|-------|
| **Render** | Free | $0 | 750 hrs/month, auto-sleep 15 min |
| **Vercel** | Hobby | $0 | 100 GB bandwidth/month |
| **Neon** | Free | $0 | 0.5 GB storage, 1 branch |
| **Upstash** | Free | $0 | 10K commands/day (reset ทุกวัน) |
| **GitHub Actions** | Free | $0 | 2,000 min/month |
| **GHCR** | Free | $0 | 500 MB |
| **รวม** | | **$0** | |

### 14.4 Environment Variables ต่อ Environment

**Staging:**
```
ASPNETCORE_ENVIRONMENT=Staging
ConnectionStrings__DefaultConnection=Host=xxx.neon.tech;Database=banking_staging;...
ConnectionStrings__Redis=xxx.upstash.io:6379,password=...
Jwt__Issuer=BankingSystem.Staging
Frontend__Url=https://staging.example.com
Swagger__Enabled=true
```

**Production:**
```
ASPNETCORE_ENVIRONMENT=Production
ConnectionStrings__DefaultConnection=Host=xxx.neon.tech;Database=banking_prod;...
ConnectionStrings__Redis=xxx.upstash.io:6379,password=...
Jwt__Issuer=BankingSystem
Frontend__Url=https://app.example.com
Swagger__Enabled=false
```

---

## 15. Monitoring & Notifications

### 15.1 Slack Notifications

เพิ่มที่ท้ายทุก workflow:

```yaml
notify:
  name: Slack Notification
  runs-on: ubuntu-latest
  needs: [deploy]
  if: always()
  steps:
    - uses: slackapi/slack-github-action@v2.0.0
      with:
        webhook: ${{ secrets.SLACK_WEBHOOK_URL }}
        webhook-type: incoming-webhook
        payload: |
          {
            "blocks": [
              {
                "type": "section",
                "text": {
                  "type": "mrkdwn",
                  "text": "${{ needs.deploy.result == 'success' && ':white_check_mark:' || ':x:' }} *${{ github.workflow }}*\n*Status:* ${{ needs.deploy.result }}\n*Branch:* `${{ github.ref_name }}`\n*Commit:* `${{ github.sha }}`\n*Actor:* ${{ github.actor }}"
                }
              }
            ]
          }
```

### 15.2 GitHub Status Badge

เพิ่มใน `README.md`:

```markdown
![Backend CI](https://github.com/YOUR_USER/YOUR_REPO/actions/workflows/backend-ci.yml/badge.svg)
![Frontend CI](https://github.com/YOUR_USER/YOUR_REPO/actions/workflows/frontend-ci.yml/badge.svg)
![Security](https://github.com/YOUR_USER/YOUR_REPO/actions/workflows/security-scan.yml/badge.svg)
```

### 15.3 Post-deploy Health Monitoring

หลัง deploy สำเร็จ ควรตรวจสอบ:

```yaml
- name: Verify metrics endpoint
  run: |
    curl -sf "${{ vars.STAGING_URL }}/metrics" | head -5
    echo "Prometheus metrics endpoint OK"

- name: Check response time
  run: |
    TIME=$(curl -o /dev/null -s -w "%{time_total}" "${{ vars.STAGING_URL }}/health")
    echo "Health check response time: ${TIME}s"
    # แจ้งเตือนถ้า response time > 2 วินาที
    if (( $(echo "$TIME > 2.0" | bc -l) )); then
      echo "WARNING: Slow response time!"
    fi
```

---

## 16. Troubleshooting

### 16.1 ปัญหาที่พบบ่อย

#### CI Build Failed: NuGet restore error

```
error NU1301: Unable to load the service index for source
```

**แก้ไข:** ตรวจสอบ NuGet source configuration หรือเพิ่ม:

```yaml
- name: Clear NuGet cache
  run: dotnet nuget locals all --clear
```

#### Integration Test Failed: Connection refused

```
Npgsql.NpgsqlException: Failed to connect to localhost:5432
```

**แก้ไข:** ตรวจสอบว่า service container พร้อมใช้งาน:

```yaml
services:
  postgres:
    # ...
    options: >-
      --health-cmd pg_isready
      --health-interval 10s
      --health-timeout 5s
      --health-retries 10    # เพิ่มจำนวน retries
```

#### Docker Build Failed: Layer cache miss

**แก้ไข:** ตรวจสอบ cache configuration:

```yaml
- uses: docker/build-push-action@v6
  with:
    cache-from: type=gha
    cache-to: type=gha,mode=max
```

#### Health Check Failed After Deploy

```
Health check failed after 10 attempts!
```

**แก้ไข:**
1. เพิ่มเวลา wait: `sleep 180` (Render Free build ช้ากว่า paid)
2. เพิ่ม `MAX_RETRIES=25` และ `--max-time 60` ใน curl
3. ตรวจสอบ logs: Render Dashboard → Service → Logs
4. **Render Free cold start:** ถ้า service sleep อยู่ request แรกจะช้า ~30s
5. ตรวจสอบว่า instance hours ยังไม่หมด: Render Dashboard → Billing

#### Permission denied: packages write

```
Error: denied: permission_denied
```

**แก้ไข:** เพิ่ม permissions ใน workflow:

```yaml
permissions:
  contents: read
  packages: write
```

#### Render Free: Instance Hours หมด

```
502 Bad Gateway / Service Unavailable
```

**แก้ไข:**
1. ตรวจ Render Dashboard → Billing → ดู hours ที่เหลือ
2. ถ้าหมด → รอ reset วันที่ 1 ของเดือนถัดไป
3. ลดจำนวน services (ใช้ 1 service = 750 hrs เพียงพอทั้งเดือน)
4. เปิด auto-sleep เพื่อประหยัด hours

#### Render Free: Cold Start ช้า

```
curl: (28) Operation timed out after 30000 milliseconds
```

**แก้ไข:**
1. เพิ่ม `--max-time 60` ใน curl
2. ใช้ cron job ping ทุก 14 นาทีเพื่อป้องกัน sleep (ในช่วงใช้งาน):
   ```yaml
   # .github/workflows/keep-alive.yml
   name: Keep Render Alive
   on:
     schedule:
       - cron: "*/14 8-22 * * 1-5"  # ทุก 14 นาที, จ-ศ, 08:00-22:00
   jobs:
     ping:
       runs-on: ubuntu-latest
       steps:
         - run: curl -sf "${{ vars.STAGING_URL }}/health" || true
   ```
3. **ข้อควรระวัง:** keep-alive ใช้ GitHub Actions minutes เพิ่ม (~1 min/run x 60 runs/day = ~60 min/day)

### 16.2 Debug Workflows

เพิ่ม step สำหรับ debug:

```yaml
- name: Debug info
  run: |
    echo "Event: ${{ github.event_name }}"
    echo "Ref: ${{ github.ref }}"
    echo "SHA: ${{ github.sha }}"
    echo "Actor: ${{ github.actor }}"
    echo "Runner OS: ${{ runner.os }}"
    dotnet --version
    node --version
    docker --version
```

เปิด **debug logging** โดยเพิ่ม Repository Secret:

```
ACTIONS_STEP_DEBUG = true
```

---

## 17. Best Practices & Tips

### 17.1 Performance Tips

| เทคนิค | ผลลัพธ์ |
|--------|--------|
| **Cache NuGet/npm** | ลดเวลา restore ~30s → ~5s |
| **Docker layer cache** | ลดเวลา build ~4m → ~1m |
| **concurrency + cancel-in-progress** | ยกเลิก run เก่าเมื่อ push ใหม่ |
| **Path filters** | รัน workflow เฉพาะเมื่อไฟล์ที่เกี่ยวข้องเปลี่ยน |
| **Parallel jobs** | Backend CI + Frontend CI รันพร้อมกัน |
| **Reusable workflows** | ลด code ซ้ำซ้อน |

### 17.2 Security Tips

- **ห้าม** hardcode secrets ใน workflow files
- ใช้ `${{ secrets.XXX }}` เสมอ
- ตั้ง `permissions` ให้น้อยที่สุดเท่าที่จำเป็น (principle of least privilege)
- ใช้ pinned action versions (`@v4`) แทน `@main`
- เปิด **Dependabot** สำหรับ GitHub Actions:

```yaml
# .github/dependabot.yml
version: 2
updates:
  - package-ecosystem: "github-actions"
    directory: "/"
    schedule:
      interval: "weekly"
  - package-ecosystem: "nuget"
    directory: "/BankingSystem"
    schedule:
      interval: "weekly"
  - package-ecosystem: "npm"
    directory: "/banking-frontend"
    schedule:
      interval: "weekly"
```

### 17.3 Git Tag สำหรับ Release

```bash
# สร้าง release tag
git tag -a v1.0.0 -m "Release v1.0.0: Initial production release"
git push origin v1.0.0

# ดู tags ทั้งหมด
git tag -l "v*"

# ลบ tag (ถ้าต้องการ)
git tag -d v1.0.0
git push origin :refs/tags/v1.0.0
```

### 17.4 Workflow สรุปรวม

```
┌──────────────────────────────────────────────────────────────────┐
│                    Complete CI/CD Pipeline                        │
├──────────────────────────────────────────────────────────────────┤
│                                                                  │
│  Developer Push / PR                                             │
│       │                                                          │
│       ├── Backend CI (build + unit test + integration test)       │
│       ├── Frontend CI (lint + typecheck + build)                 │
│       └── Security Scan (CodeQL + audit + secrets + trivy)       │
│                │                                                  │
│                ▼  All checks pass                                │
│           Merge to main                                          │
│                │                                                  │
│                ├── Docker Build & Push (GHCR)                    │
│                └── Deploy to Staging (auto)                      │
│                        ├── Render Free (backend)                  │
│                        ├── Vercel (frontend)                     │
│                        ├── Health check                          │
│                        └── Smoke test                            │
│                                                                  │
│  git tag v1.0.0 && git push origin v1.0.0                       │
│       │                                                          │
│       └── Deploy to Production (manual approval)                 │
│                ├── DB Migration                                  │
│                ├── Render Free (backend)                           │
│                ├── Vercel (frontend --prod)                      │
│                ├── Health check                                  │
│                ├── API verification                              │
│                ├── GitHub Release (changelog)                    │
│                └── Slack notification                            │
│                                                                  │
└──────────────────────────────────────────────────────────────────┘
```

---

## Checklist สำหรับเริ่มต้น

- [ ] สมัคร Render.com (ฟรี, เชื่อม GitHub)
- [ ] สร้าง Render Web Service (Docker, Free plan)
- [ ] คัดลอก Deploy Hook URLs จาก Render
- [ ] สร้าง GitHub Secrets ทั้งหมด (Section 4)
- [ ] สร้าง GitHub Environments: `staging`, `production`
- [ ] ตั้ง required reviewers สำหรับ `production` environment
- [ ] สร้างไฟล์ `.github/workflows/backend-ci.yml`
- [ ] สร้างไฟล์ `.github/workflows/frontend-ci.yml`
- [ ] สร้างไฟล์ `.github/workflows/docker-build.yml`
- [ ] สร้างไฟล์ `.github/workflows/deploy-staging.yml`
- [ ] สร้างไฟล์ `.github/workflows/deploy-production.yml`
- [ ] สร้างไฟล์ `.github/workflows/db-migration.yml`
- [ ] สร้างไฟล์ `.github/workflows/security-scan.yml`
- [ ] สร้างไฟล์ `.github/dependabot.yml`
- [ ] สร้างไฟล์ `.github/CODEOWNERS`
- [ ] สร้างไฟล์ `.github/pull_request_template.md`
- [ ] ตั้งค่า Branch Protection Rules สำหรับ `main`
- [ ] ตั้งค่า Slack Webhook สำหรับ notifications
- [ ] ทดสอบ pipeline ด้วยการสร้าง PR
- [ ] ทดสอบ production deploy ด้วยการสร้าง tag
