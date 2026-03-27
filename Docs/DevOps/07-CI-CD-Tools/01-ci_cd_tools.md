# CI/CD Tools

> พื้นฐาน CI/CD อยู่ใน 09-Integration-Patterns แล้ว ส่วนนี้เจาะลึกเครื่องมือ

---

## 1. เครื่องมือ CI/CD เปรียบเทียบ

```
| เครื่องมือ       | ประเภท     | ดีกับ                    | ราคา         |
|-----------------|-----------|--------------------------|-------------|
| GitHub Actions ⭐| Cloud     | GitHub repos, ง่าย        | ฟรี (public) |
| GitLab CI/CD    | Cloud/Self| GitLab, DevOps ครบ        | ฟรี tier    |
| Jenkins         | Self-host | ปรับแต่งได้สุด, plugin เยอะ| ฟรี (open source)|
| CircleCI        | Cloud     | เร็ว, Docker-native       | ฟรี tier    |
| TeamCity        | Self-host | .NET, JetBrains ecosystem | ฟรี (เล็ก)  |
| Azure DevOps    | Cloud     | .NET, Azure               | ฟรี tier    |
| Octopus Deploy  | Cloud/Self| Deployment focus          | ฟรี tier    |
```

## 2. Jenkins — Self-hosted CI/CD

```groovy
// Jenkinsfile (Declarative Pipeline)
pipeline {
    agent any

    environment {
        DOCKER_IMAGE = "myapp"
        DOCKER_TAG = "${BUILD_NUMBER}"
    }

    stages {
        stage('Checkout') {
            steps {
                checkout scm
            }
        }

        stage('Build') {
            steps {
                sh 'npm ci'
                sh 'npm run build'
            }
        }

        stage('Test') {
            steps {
                sh 'npm test'
            }
            post {
                always {
                    junit 'test-results/*.xml'
                }
            }
        }

        stage('Docker Build') {
            steps {
                sh "docker build -t ${DOCKER_IMAGE}:${DOCKER_TAG} ."
            }
        }

        stage('Deploy to Staging') {
            when { branch 'develop' }
            steps {
                sh "docker push registry.example.com/${DOCKER_IMAGE}:${DOCKER_TAG}"
                sh "kubectl set image deployment/myapp myapp=${DOCKER_IMAGE}:${DOCKER_TAG} -n staging"
            }
        }

        stage('Deploy to Production') {
            when { branch 'main' }
            input { message "Deploy to production?" }
            steps {
                sh "kubectl set image deployment/myapp myapp=${DOCKER_IMAGE}:${DOCKER_TAG} -n production"
            }
        }
    }

    post {
        failure {
            slackSend channel: '#alerts', message: "Build FAILED: ${env.JOB_NAME} #${env.BUILD_NUMBER}"
        }
        success {
            slackSend channel: '#deploys', message: "Build SUCCESS: ${env.JOB_NAME} #${env.BUILD_NUMBER}"
        }
    }
}
```

## 3. Deployment Strategies

```
1. Rolling Update (default K8s):
   เปลี่ยนทีละ pod → ค่อยๆ เปลี่ยนจนหมด
   ✅ Zero downtime  ❌ ช่วงหนึ่งมี 2 versions

2. Blue-Green:
   Blue (v1 — running) | Green (v2 — ready)
   สลับ traffic จาก Blue → Green ทันที
   ✅ Instant rollback  ❌ ต้องมี 2x resources

3. Canary:
   ส่ง traffic 5% ไป v2 → ถ้า OK → เพิ่มเป็น 25% → 50% → 100%
   ✅ ทดสอบกับ real users  ❌ ซับซ้อนกว่า

4. A/B Testing:
   คล้าย Canary แต่แบ่งตาม user segments
   ✅ ทดสอบ features  ❌ ต้องมี feature flags
```
