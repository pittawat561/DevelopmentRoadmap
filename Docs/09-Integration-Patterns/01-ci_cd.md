# CI/CD — Continuous Integration & Continuous Delivery

---

## 1. CI/CD คืออะไร

```
CI (Continuous Integration):
- รวมโค้ดจากหลายคนเข้าด้วยกันบ่อยๆ (ทุกวัน/ทุก commit)
- รัน tests อัตโนมัติทุกครั้ง
- ตรวจจับ bug เร็ว

CD (Continuous Delivery):
- ทำให้โค้ดพร้อม deploy ได้เสมอ
- ผ่าน tests → พร้อม deploy (manual approve)

CD (Continuous Deployment):
- deploy อัตโนมัติทุกครั้งที่ผ่าน tests
- ไม่ต้อง approve (สำหรับทีมที่มั่นใจ)

Pipeline:
Code → Build → Test → (Approve) → Deploy

┌─────────┐    ┌─────────┐    ┌─────────┐    ┌─────────┐
│  Push   │ →  │  Build  │ →  │  Test   │ →  │ Deploy  │
│  Code   │    │ Compile │    │ Unit    │    │ Staging │
│         │    │ Docker  │    │ Integ.  │    │ Prod    │
└─────────┘    └─────────┘    └─────────┘    └─────────┘
      CI ──────────────────────┘     CD ─────────────────┘
```

## 2. CI/CD Tools

```
| เครื่องมือ          | ประเภท              | เด่นเรื่อง                 |
|--------------------|--------------------|---------------------------|
| GitHub Actions     | Cloud (GitHub)     | ง่าย, ฟรีสำหรับ public repo |
| GitLab CI/CD       | Built-in GitLab    | ครบในตัว, self-hosted ได้   |
| Jenkins            | Self-hosted        | ปรับแต่งได้มาก, plugin เยอะ |
| Azure DevOps       | Cloud (Microsoft)  | ดีกับ .NET / Azure         |
| CircleCI           | Cloud              | เร็ว, config ง่าย           |
| Travis CI          | Cloud              | เก่าแก่, open source        |
```

## 3. GitHub Actions — ตัวอย่างเต็ม

```yaml
# .github/workflows/ci-cd.yml
name: CI/CD Pipeline

on:
  push:
    branches: [main, develop]
  pull_request:
    branches: [main]

jobs:
  # ===== JOB 1: Build & Test =====
  test:
    runs-on: ubuntu-latest

    services:
      postgres:                         # Test database
        image: postgres:16
        env:
          POSTGRES_DB: testdb
          POSTGRES_USER: testuser
          POSTGRES_PASSWORD: testpass
        ports:
          - 5432:5432

    steps:
      - uses: actions/checkout@v4

      - name: Setup Node.js
        uses: actions/setup-node@v4
        with:
          node-version: '20'
          cache: 'npm'

      - name: Install dependencies
        run: npm ci                     # ci = clean install (เร็วกว่า npm install)

      - name: Run linting
        run: npm run lint

      - name: Run unit tests
        run: npm test

      - name: Run integration tests
        run: npm run test:integration
        env:
          DATABASE_URL: postgresql://testuser:testpass@localhost:5432/testdb

      - name: Build
        run: npm run build

  # ===== JOB 2: Deploy to Staging =====
  deploy-staging:
    needs: test                          # รอ test เสร็จก่อน
    if: github.ref == 'refs/heads/develop'
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - name: Deploy to Staging
        run: |
          echo "Deploying to staging..."
          # deploy script ของคุณ

  # ===== JOB 3: Deploy to Production =====
  deploy-prod:
    needs: test
    if: github.ref == 'refs/heads/main'
    runs-on: ubuntu-latest
    environment: production              # ต้อง approve ก่อน deploy
    steps:
      - uses: actions/checkout@v4
      - name: Deploy to Production
        run: |
          echo "Deploying to production..."
```

## 4. .NET CI/CD Example

```yaml
# .github/workflows/dotnet.yml
name: .NET CI/CD

on:
  push:
    branches: [main]
  pull_request:
    branches: [main]

jobs:
  build-and-test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0.x'

      - name: Restore dependencies
        run: dotnet restore

      - name: Build
        run: dotnet build --no-restore --configuration Release

      - name: Test
        run: dotnet test --no-build --configuration Release --verbosity normal

      - name: Publish
        run: dotnet publish -c Release -o ./publish

      - name: Upload artifact
        uses: actions/upload-artifact@v4
        with:
          name: app
          path: ./publish
```

## 5. Best Practices

```
✅ ควรทำ:
- รัน CI ทุก push และทุก Pull Request
- Tests ต้องเร็ว (< 10 นาที ถ้าเป็นไปได้)
- ใช้ caching (npm cache, NuGet cache)
- Environment variables สำหรับ secrets
- แยก staging / production environment
- Rollback plan — ถ้า deploy ผิดพลาด ย้อนกลับได้

❌ ไม่ควรทำ:
- ใส่ secrets ใน code/config files
- Skip tests เพื่อ deploy เร็วขึ้น
- Deploy ตรงไป production โดยไม่ผ่าน staging
```
