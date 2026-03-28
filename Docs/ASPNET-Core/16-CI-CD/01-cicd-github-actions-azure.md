# CI/CD สำหรับ .NET — GitHub Actions & Azure Pipelines

> Build, Test, Deploy อัตโนมัติทุกครั้งที่ push code

---

## 1. CI/CD คืออะไร

```
CI (Continuous Integration):
Push code → Auto build → Auto test → แจ้งผล
ถ้า test fail → แก้ทันที (ไม่ปล่อย bug เข้า main)

CD (Continuous Delivery/Deployment):
Test ผ่าน → Auto deploy ไป staging → (Manual approve) → Deploy production

Push → Build → Test → Deploy Staging → Approve → Deploy Production
```

---

## 2. GitHub Actions (แนะนำ!)

```yaml
# .github/workflows/ci.yml
name: CI/CD Pipeline

on:
  push:
    branches: [main, develop]
  pull_request:
    branches: [main]

env:
  DOTNET_VERSION: '9.0.x'

jobs:
  # ===== Build & Test =====
  build:
    runs-on: ubuntu-latest

    services:
      sqlserver:
        image: mcr.microsoft.com/mssql/server:2022-latest
        env:
          ACCEPT_EULA: Y
          SA_PASSWORD: TestPass123!
        ports:
          - 1433:1433

    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: ${{ env.DOTNET_VERSION }}

      - name: Restore dependencies
        run: dotnet restore

      - name: Build
        run: dotnet build --no-restore -c Release

      - name: Run Unit Tests
        run: dotnet test tests/MyApi.Tests.Unit --no-build -c Release --logger trx

      - name: Run Integration Tests
        run: dotnet test tests/MyApi.Tests.Integration --no-build -c Release --logger trx
        env:
          ConnectionStrings__DefaultConnection: "Server=localhost;Database=TestDb;User Id=sa;Password=TestPass123!;TrustServerCertificate=true;"

      - name: Publish
        run: dotnet publish src/MyApi.Api -c Release -o ./publish

      - name: Upload artifact
        uses: actions/upload-artifact@v4
        with:
          name: app
          path: ./publish

  # ===== Deploy to Staging =====
  deploy-staging:
    needs: build
    runs-on: ubuntu-latest
    if: github.ref == 'refs/heads/develop'
    environment: staging

    steps:
      - uses: actions/download-artifact@v4
        with:
          name: app
          path: ./publish

      - name: Deploy to Azure App Service
        uses: azure/webapps-deploy@v3
        with:
          app-name: myapi-staging
          publish-profile: ${{ secrets.AZURE_STAGING_PUBLISH_PROFILE }}
          package: ./publish

  # ===== Deploy to Production =====
  deploy-production:
    needs: build
    runs-on: ubuntu-latest
    if: github.ref == 'refs/heads/main'
    environment: production    # requires manual approval!

    steps:
      - uses: actions/download-artifact@v4
        with:
          name: app
          path: ./publish

      - name: Deploy to Azure App Service
        uses: azure/webapps-deploy@v3
        with:
          app-name: myapi-production
          publish-profile: ${{ secrets.AZURE_PROD_PUBLISH_PROFILE }}
          package: ./publish
```

### Docker Build + Push

```yaml
# .github/workflows/docker.yml
name: Docker Build & Push

on:
  push:
    branches: [main]

jobs:
  docker:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Login to Docker Hub
        uses: docker/login-action@v3
        with:
          username: ${{ secrets.DOCKER_USERNAME }}
          password: ${{ secrets.DOCKER_PASSWORD }}

      - name: Build and Push
        uses: docker/build-push-action@v5
        with:
          context: .
          push: true
          tags: |
            myorg/myapi:latest
            myorg/myapi:${{ github.sha }}
```

---

## 3. Azure Pipelines (ทางเลือก)

```yaml
# azure-pipelines.yml
trigger:
  branches:
    include: [main, develop]

pool:
  vmImage: 'ubuntu-latest'

variables:
  buildConfiguration: 'Release'
  dotnetVersion: '9.0.x'

stages:
  - stage: Build
    jobs:
      - job: BuildAndTest
        steps:
          - task: UseDotNet@2
            inputs:
              version: $(dotnetVersion)

          - script: dotnet restore
          - script: dotnet build -c $(buildConfiguration)
          - script: dotnet test -c $(buildConfiguration) --logger trx
            displayName: Run Tests

          - task: PublishTestResults@2
            inputs:
              testResultsFormat: VSTest
              testResultsFiles: '**/*.trx'

          - script: dotnet publish src/MyApi.Api -c $(buildConfiguration) -o $(Build.ArtifactStagingDirectory)
          - publish: $(Build.ArtifactStagingDirectory)
            artifact: app

  - stage: DeployStaging
    dependsOn: Build
    condition: and(succeeded(), eq(variables['Build.SourceBranch'], 'refs/heads/develop'))
    jobs:
      - deployment: Deploy
        environment: staging
        strategy:
          runOnce:
            deploy:
              steps:
                - task: AzureWebApp@1
                  inputs:
                    azureSubscription: 'my-azure-connection'
                    appName: 'myapi-staging'
                    package: '$(Pipeline.Workspace)/app'
```

---

## 4. Best Practices

```
✅ ควรทำ:
- Test ทุก PR ก่อน merge
- Build + Test อัตโนมัติทุก push
- แยก environments: staging → production
- ใช้ secrets manager (ไม่ hardcode credentials)
- Require approval ก่อน deploy production
- Tag Docker images ด้วย git SHA

❌ ไม่ควร:
- Deploy โดยไม่รัน tests
- ใส่ secrets ใน code หรือ YAML
- Deploy ตรงไป production (ไม่ผ่าน staging)
```
