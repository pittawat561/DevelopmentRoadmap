# Container Orchestration, Observability, Artifact Management & GitOps

---

## 1. Container Orchestration

### เครื่องมือ

```
| เครื่องมือ         | เด่นเรื่อง                           |
|-------------------|--------------------------------------|
| Kubernetes ⭐      | มาตรฐาน, ครบที่สุด, ซับซ้อน          |
| Docker Swarm      | ง่าย, built-in Docker, เล็ก           |
| OpenShift         | K8s + enterprise features (Red Hat)   |

Managed Kubernetes (ไม่ต้องจัดการ control plane เอง):
├── AWS EKS          — Elastic Kubernetes Service
├── Azure AKS        — Azure Kubernetes Service
├── Google GKE ⭐    — Google Kubernetes Engine (ดีที่สุด)
├── AWS ECS/Fargate  — ง่ายกว่า K8s (AWS proprietary)
└── DigitalOcean K8s — ราคาถูก

เลือกอะไร:
├── ต้องการ standard + portable?          → Kubernetes (EKS/AKS/GKE)
├── ต้องการง่ายกว่า K8s บน AWS?           → ECS Fargate
├── ทีมเล็ก, ไม่ต้องการ K8s complexity?   → Docker Swarm
└── Enterprise + Red Hat?                 → OpenShift
```

### Kubernetes ขั้นสูงสำหรับ DevOps

```yaml
# Horizontal Pod Autoscaler — scale อัตโนมัติ
apiVersion: autoscaling/v2
kind: HorizontalPodAutoscaler
metadata:
  name: myapp-hpa
spec:
  scaleTargetRef:
    apiVersion: apps/v1
    kind: Deployment
    name: myapp
  minReplicas: 2
  maxReplicas: 20
  metrics:
    - type: Resource
      resource:
        name: cpu
        target:
          type: Utilization
          averageUtilization: 70

---
# Ingress — routing ภายนอกเข้า cluster
apiVersion: networking.k8s.io/v1
kind: Ingress
metadata:
  name: myapp-ingress
  annotations:
    cert-manager.io/cluster-issuer: "letsencrypt-prod"
spec:
  tls:
    - hosts: [api.example.com]
      secretName: api-tls
  rules:
    - host: api.example.com
      http:
        paths:
          - path: /
            pathType: Prefix
            backend:
              service:
                name: myapp-service
                port:
                  number: 80
```

---

## 2. Observability (เจาะลึก)

```
เนื้อหาพื้นฐานอยู่ใน 19-Building-For-Scale แล้ว

เครื่องมือเพิ่มเติม:
| เครื่องมือ       | ทำอะไร           | ประเภท       |
|-----------------|------------------|-------------|
| Jaeger          | Distributed tracing| Open source |
| New Relic       | APM ครบวงจร       | Cloud       |
| Datadog         | Metrics+Logs+Traces| Cloud      |
| Prometheus      | Metrics           | Open source |
| OpenTelemetry ⭐| Standard for all  | Open source |
| Dynatrace       | AI-powered APM    | Cloud       |

OpenTelemetry (OTel) — มาตรฐานใหม่:
- รวม metrics, logs, traces ไว้ใน SDK เดียว
- ส่งไป backend ไหนก็ได้ (Jaeger, Datadog, New Relic)
- รองรับทุกภาษา (Node.js, Python, Go, .NET, Java)
```

---

## 3. Artifact Management

```
เก็บ artifacts (Docker images, packages, binaries) อย่างเป็นระบบ

| เครื่องมือ       | เก็บอะไร                        |
|-----------------|--------------------------------|
| Artifactory ⭐  | ทุกอย่าง (Docker, npm, Maven, etc.)|
| Nexus           | ทุกอย่าง (คล้าย Artifactory)    |
| Docker Hub      | Docker images                   |
| GitHub Packages | npm, Docker, Maven, NuGet       |
| AWS ECR         | Docker images (AWS)             |
| Azure ACR       | Docker images (Azure)           |
| Cloud Smith     | Multi-format package hosting    |

CI/CD Pipeline:
Code → Build → Test → Push to Registry → Deploy from Registry
                              ↑
                    Artifactory/ECR/ACR
```

---

## 4. GitOps

### คืออะไร

GitOps = **ใช้ Git เป็น single source of truth สำหรับ infrastructure + application deployment**

```
Traditional Deploy:
Developer → push code → CI build → SSH server → deploy  ← imperative

GitOps:
Developer → push code → CI build → push image
Developer → update K8s manifests in Git
                ↓
         [ArgoCD/FluxCD] ← ดู Git repo ตลอด
                ↓
         ถ้า Git ≠ Cluster → sync ให้ตรงกัน ← declarative
```

### ArgoCD ⭐

```yaml
# ArgoCD Application
apiVersion: argoproj.io/v1alpha1
kind: Application
metadata:
  name: myapp
  namespace: argocd
spec:
  project: default
  source:
    repoURL: https://github.com/myorg/k8s-manifests.git
    targetRevision: main
    path: apps/myapp/production
  destination:
    server: https://kubernetes.default.svc
    namespace: production
  syncPolicy:
    automated:
      prune: true          # ลบ resources ที่ไม่อยู่ใน Git
      selfHeal: true       # ถ้าใครแก้ manual → กลับเป็นตาม Git

# Workflow:
# 1. Developer push K8s manifests ไป Git
# 2. ArgoCD detect การเปลี่ยนแปลง
# 3. ArgoCD sync cluster ให้ตรงกับ Git
# 4. ถ้ามีคนแก้ cluster manual → ArgoCD แก้กลับ (self-heal)
```

```
GitOps ข้อดี:
✅ ทุกอย่างอยู่ใน Git (audit trail, rollback)
✅ PR-based deployment (review ก่อน deploy)
✅ Declarative — cluster state = Git state
✅ Self-healing

GitOps Tools:
├── ArgoCD ⭐    — Web UI ดี, ใช้มากที่สุด
└── FluxCD       — Lightweight, CNCF project
```
