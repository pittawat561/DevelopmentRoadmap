# Design & Architecture — Architectural Patterns & System Design

---

## 1. Architectural Patterns

### Monolith

```
ทุกอย่างอยู่ใน application เดียว

┌──────────────────────────┐
│       Monolith App       │
│ ┌──────┬──────┬────────┐ │
│ │ Auth │Orders│Products│ │
│ ├──────┴──────┴────────┤ │
│ │     Shared Database   │ │
│ └───────────────────────┘ │
└──────────────────────────┘

✅ ข้อดี: deploy ง่าย, debug ง่าย, เริ่มต้นเร็ว
❌ ข้อเสีย: scale ยาก, แก้ส่วนนึงอาจกระทบส่วนอื่น, tech stack เดียว
เหมาะกับ: startup, ทีมเล็ก, MVP
```

### Microservices

```
แยกแต่ละ feature เป็น service อิสระ

┌────────┐ ┌────────┐ ┌──────────┐
│  Auth  │ │ Orders │ │ Products │  ← แต่ละตัวมี DB ของตัวเอง
│Service │ │Service │ │ Service  │
│  [DB]  │ │  [DB]  │ │  [DB]    │
└───┬────┘ └───┬────┘ └────┬─────┘
    └──────────┼───────────┘
         [API Gateway]
              │
           [Client]

✅ ข้อดี: scale แยกได้, deploy อิสระ, เลือก tech stack ต่างกันได้
❌ ข้อเสีย: ซับซ้อน, network latency, debugging ยาก
เหมาะกับ: ทีมใหญ่, ระบบที่ต้อง scale
```

### SOA (Service-Oriented Architecture)

```
คล้าย Microservices แต่ services ใหญ่กว่า + share ข้อมูลผ่าน ESB

Services → [Enterprise Service Bus] → Services

SOA vs Microservices:
- SOA: services ใหญ่กว่า, share data มากกว่า
- Microservices: services เล็กกว่า, แยก DB, lightweight communication
```

### Serverless

```
ไม่ต้องจัดการ server เลย — cloud จัดการให้

┌─────────────┐
│ AWS Lambda  │  ← รัน function เมื่อมี request
│ Azure Func  │  ← จ่ายเฉพาะตอนใช้
│ Cloud Func  │  ← auto-scale
└─────────────┘

// AWS Lambda Example
exports.handler = async (event) => {
  const { name } = JSON.parse(event.body)
  const user = await createUser(name)
  return {
    statusCode: 201,
    body: JSON.stringify(user)
  }
}

✅ ข้อดี: ไม่ต้องจัดการ infra, จ่ายตามใช้, auto-scale
❌ ข้อเสีย: cold start, vendor lock-in, จำกัดเวลารัน (15 นาที)
เหมาะกับ: API ง่ายๆ, scheduled tasks, event processing
```

---

## 2. Design Patterns สำหรับ Backend

### Service Mesh

```
จัดการ communication ระหว่าง microservices

ทำอะไร:
- Service discovery (หา service ที่ต้องการ)
- Load balancing ระหว่าง services
- mTLS (เข้ารหัสระหว่าง services)
- Circuit breaker
- Observability (metrics, tracing)

เครื่องมือ: Istio, Linkerd, Consul Connect
```

### Twelve-Factor App

```
12 หลักการสำหรับสร้าง cloud-native app:

1.  Codebase      — 1 repo = 1 app
2.  Dependencies   — ประกาศ dependencies ชัดเจน (package.json)
3.  Config         — config อยู่ใน environment variables
4.  Backing Services — DB, cache เป็น attached resources
5.  Build, Release, Run — แยก 3 ขั้นตอนชัดเจน
6.  Processes      — app เป็น stateless processes
7.  Port Binding   — export service ผ่าน port
8.  Concurrency    — scale ด้วยการเพิ่ม processes
9.  Disposability  — start เร็ว, stop gracefully
10. Dev/Prod Parity — dev กับ production เหมือนกันที่สุด
11. Logs           — เขียน logs เป็น event streams
12. Admin Processes — รัน admin tasks เป็น one-off processes
```

---

## 3. System Design Basics

### Load Balancer

```
กระจาย traffic ไปหลาย servers

Client → [Load Balancer] → Server 1
                         → Server 2
                         → Server 3

Algorithms:
- Round Robin: วนรอบ 1→2→3→1→2→3
- Least Connections: ส่งไป server ที่ว่างที่สุด
- IP Hash: client เดิมไป server เดิม

เครื่องมือ: Nginx, HAProxy, AWS ALB, Cloudflare
```

### API Gateway

```
จุดเข้าเดียวสำหรับทุก API requests

Client → [API Gateway] → Auth Service
                       → Order Service
                       → Product Service

ทำอะไร:
- Authentication/Authorization
- Rate Limiting
- Request routing
- Response caching
- Logging/Monitoring
- Request/Response transformation

เครื่องมือ: Kong, AWS API Gateway, Azure API Management
```

### Horizontal vs Vertical Scaling

```
Vertical Scaling (Scale Up):
เพิ่ม CPU/RAM ให้เครื่องเดิม
4 CPU → 16 CPU
8 GB RAM → 64 GB RAM
✅ ง่าย  ❌ มีขีดจำกัด, แพง

Horizontal Scaling (Scale Out):
เพิ่มจำนวนเครื่อง
1 server → 5 servers → 20 servers
✅ ไม่มีขีดจำกัด  ❌ ซับซ้อนกว่า (load balancer, stateless)

แนะนำ: Scale vertically ก่อน → ถ้าไม่พอ → Scale horizontally
```

---

## 4. สรุป

```
เริ่มต้น:
1. Monolith + ออกแบบ modular ดีๆ
2. เมื่อโตขึ้น → แยกเป็น Microservices ทีละส่วน
3. ใช้ API Gateway จัดการ routing
4. เพิ่ม Message Broker สำหรับ async tasks
5. Scale horizontally + Load Balancer

"Start with a monolith, evolve to microservices when needed."
```
