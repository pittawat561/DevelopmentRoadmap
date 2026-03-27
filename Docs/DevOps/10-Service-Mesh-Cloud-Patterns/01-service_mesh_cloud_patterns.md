# Service Mesh & Cloud Design Patterns

---

## 1. Service Mesh

### คืออะไร

Service Mesh = **infrastructure layer ที่จัดการ communication ระหว่าง microservices** โดยไม่ต้องแก้โค้ด application

```
ไม่มี Service Mesh:
Service A → Service B  (ต้องจัดการ retry, timeout, TLS เอง)

มี Service Mesh:
Service A → [Sidecar Proxy] → [Sidecar Proxy] → Service B
                ↕                    ↕
            [Control Plane]
            จัดการ: mTLS, retry, circuit breaker, observability

Sidecar Pattern:
┌──────────────────────────┐
│         Pod              │
│ ┌──────────┐ ┌─────────┐│
│ │  App     │ │ Envoy   ││  ← Sidecar proxy
│ │ Container│ │ Proxy   ││  ← ดักจับ traffic ทั้งหมด
│ └──────────┘ └─────────┘│
└──────────────────────────┘
```

### ทำอะไรได้

```
1. mTLS (Mutual TLS)     — เข้ารหัสทุก service-to-service communication
2. Traffic Management     — canary deploy, A/B testing, traffic splitting
3. Retry & Timeout        — retry อัตโนมัติเมื่อ request ล้มเหลว
4. Circuit Breaker        — หยุดส่ง request ไป service ที่มีปัญหา
5. Load Balancing         — กระจาย traffic ระหว่าง instances
6. Observability          — metrics, traces, logs อัตโนมัติ
7. Rate Limiting          — จำกัด request rate
8. Access Control         — กำหนดว่า service ไหนคุยกับ service ไหนได้
```

### เครื่องมือ

```
| เครื่องมือ  | Data Plane | เด่นเรื่อง                          |
|------------|-----------|--------------------------------------|
| Istio ⭐   | Envoy     | ครบที่สุด, ซับซ้อน, ใช้มากที่สุด       |
| Linkerd    | linkerd2  | เบา, ง่าย, CNCF graduated            |
| Consul     | Envoy     | Service discovery + mesh (HashiCorp) |
| Envoy      | (เป็น proxy)| ใช้เป็น standalone proxy ได้          |

เลือกอะไร:
├── ต้องการ features ครบ?     → Istio
├── ต้องการง่าย, เบา?         → Linkerd
├── ใช้ HashiCorp stack อยู่? → Consul
└── เริ่มต้น?                 → Linkerd (เรียนง่ายกว่า)

⚠️ Service Mesh เพิ่มความซับซ้อน — ใช้เมื่อจำเป็นจริงๆ เท่านั้น
ถ้ามี < 10 services → อาจยังไม่ต้องใช้
```

### Istio ตัวอย่าง

```yaml
# Traffic Splitting — Canary Deployment
apiVersion: networking.istio.io/v1alpha3
kind: VirtualService
metadata:
  name: myapp
spec:
  hosts: [myapp]
  http:
    - route:
        - destination:
            host: myapp
            subset: v1
          weight: 90        # 90% ไป v1
        - destination:
            host: myapp
            subset: v2
          weight: 10        # 10% ไป v2 (canary)

---
# Circuit Breaker
apiVersion: networking.istio.io/v1alpha3
kind: DestinationRule
metadata:
  name: myapp
spec:
  host: myapp
  trafficPolicy:
    connectionPool:
      http:
        h2UpgradePolicy: DEFAULT
        http1MaxPendingRequests: 100
        http2MaxRequests: 1000
    outlierDetection:
      consecutive5xxErrors: 5
      interval: 30s
      baseEjectionTime: 30s    # ถ้า error 5 ครั้ง → หยุดส่ง 30 วินาที
```

---

## 2. Cloud Design Patterns

### Availability Patterns

```
1. Health Endpoint Monitoring
   /health → ตรวจสอบว่า service ทำงานปกติ
   Load balancer เรียกทุก 10 วินาที → ถ้าไม่ตอบ → หยุดส่ง traffic

2. Queue-Based Load Leveling
   Client → [Queue] → Worker
   ป้องกัน backend overload เมื่อ traffic spike

3. Throttling
   จำกัด requests → ป้องกันระบบล่ม
```

### Data Management Patterns

```
1. CQRS (Command Query Responsibility Segregation)
   Write → Write Database (optimized for writes)
   Read  → Read Database (optimized for reads)

2. Event Sourcing
   เก็บทุก event แทนที่จะเก็บ current state
   AccountCreated → Deposited(100) → Withdrawn(30) → Balance = 70

3. Sharding
   แบ่งข้อมูลข้ามหลาย databases ตาม key
```

### Design & Implementation Patterns

```
1. Sidecar Pattern
   แยก cross-cutting concerns (logging, proxy) เป็น container ข้างๆ

2. Ambassador Pattern
   Proxy container จัดการ outbound connections

3. Strangler Fig Pattern
   ค่อยๆ ย้ายจาก monolith → microservices ทีละส่วน
   Old system ← [Router] → New service
   เมื่อย้ายครบ → ลบ old system

4. Backends for Frontends (BFF)
   API Gateway แยกตาม client type
   Mobile → [Mobile BFF] → Services
   Web    → [Web BFF]    → Services
```

### Management & Monitoring Patterns

```
1. Circuit Breaker        → หยุดเรียก service ที่มีปัญหา
2. Retry with Backoff     → ลองใหม่ รอนานขึ้นเรื่อยๆ
3. Leader Election        → เลือก 1 instance เป็นผู้นำ
4. Bulkhead               → แยก resource pools ป้องกัน cascade failure
5. Compensating Transaction → ยกเลิก distributed transaction
```

---

## 3. สรุป DevOps Roadmap ทั้งหมด

```
DevOps Learning Path:

Phase 1 — พื้นฐาน:
├── 01. Programming Language (Python) + Linux + Bash
├── 02. Git + GitHub/GitLab
└── 03. Networking (SSH, DNS, HTTP/HTTPS, Firewall)

Phase 2 — Core DevOps:
├── 04. Docker (containers, images, compose)
├── 05. Cloud Provider (AWS/Azure/GCP — เลือก 1)
├── 06. IaC (Terraform) + Config Management (Ansible)
└── 07. CI/CD (GitHub Actions or Jenkins)

Phase 3 — Production:
├── 08. Secret Management + Centralized Logging + Monitoring
├── 09. Kubernetes + GitOps (ArgoCD)
└── 10. Observability + Service Mesh (เมื่อจำเป็น)

"DevOps is a journey, not a destination."
```
