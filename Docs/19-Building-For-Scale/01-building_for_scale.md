# Building For Scale — Observability & Mitigation Strategies

---

## 1. Observability — การมองเห็นระบบ

### 3 Pillars of Observability

```
1. Logs      — บันทึกเหตุการณ์ (อะไรเกิดขึ้น)
2. Metrics   — ตัวเลขวัดผล (ระบบทำงานดีแค่ไหน)
3. Traces    — ติดตาม request ข้าม services (ช้าตรงไหน)

┌─────────────────────────────────────────┐
│             Observability               │
│                                         │
│  ┌────────┐  ┌─────────┐  ┌─────────┐  │
│  │  Logs  │  │ Metrics │  │ Traces  │  │
│  │        │  │         │  │         │  │
│  │ What   │  │ How     │  │ Where   │  │
│  │ happened│ │ is it   │  │ is it   │  │
│  │        │  │ doing?  │  │ slow?   │  │
│  └────────┘  └─────────┘  └─────────┘  │
└─────────────────────────────────────────┘
```

### Logging

```javascript
// ✅ Structured Logging (JSON) — ง่ายต่อการค้นหาและวิเคราะห์
const logger = require('pino')()

logger.info({
  event: 'user_registered',
  userId: 123,
  email: 'john@test.com',
  duration: 45
}, 'User registered successfully')

// Output:
// {"level":30,"time":1705300000,"event":"user_registered",
//  "userId":123,"email":"john@test.com","duration":45,
//  "msg":"User registered successfully"}

// ❌ ไม่ดี:
console.log('User john@test.com registered')
// → ค้นหายาก, parse ยาก, ไม่มี context

// Log Levels:
// FATAL  → ระบบพัง ต้องแก้ทันที
// ERROR  → เกิด error แต่ระบบยังทำงาน
// WARN   → มีปัญหา แต่ไม่ร้ายแรง
// INFO   → เหตุการณ์ปกติ (user login, order created)
// DEBUG  → ข้อมูลสำหรับ debugging
// TRACE  → ข้อมูลละเอียดมาก
```

### Metrics & Monitoring

```
Metrics ที่สำคัญ:

RED Method (สำหรับ services):
├── Rate      — requests per second
├── Errors    — error rate (%)
└── Duration  — response time (p50, p95, p99)

USE Method (สำหรับ resources):
├── Utilization — CPU, Memory ใช้ไปเท่าไหร่ (%)
├── Saturation  — งานรอคิวเท่าไหร่
└── Errors      — error count

เครื่องมือ:
├── Prometheus + Grafana  ← open source, นิยมมาก
├── Datadog               ← cloud, ครบในตัว
├── New Relic             ← APM
├── Azure Monitor         ← สำหรับ Azure
└── AWS CloudWatch        ← สำหรับ AWS
```

```javascript
// Prometheus metrics ด้วย Node.js
const promClient = require('prom-client')

// Counter — นับจำนวน
const httpRequests = new promClient.Counter({
  name: 'http_requests_total',
  help: 'Total HTTP requests',
  labelNames: ['method', 'path', 'status']
})

// Histogram — วัดเวลา
const httpDuration = new promClient.Histogram({
  name: 'http_request_duration_seconds',
  help: 'HTTP request duration',
  labelNames: ['method', 'path'],
  buckets: [0.01, 0.05, 0.1, 0.5, 1, 5]
})

// Middleware
app.use((req, res, next) => {
  const end = httpDuration.startTimer({ method: req.method, path: req.path })

  res.on('finish', () => {
    httpRequests.inc({ method: req.method, path: req.path, status: res.statusCode })
    end()
  })

  next()
})

// Expose metrics endpoint
app.get('/metrics', async (req, res) => {
  res.set('Content-Type', promClient.register.contentType)
  res.send(await promClient.register.metrics())
})
```

### Distributed Tracing (Telemetry)

```
ติดตาม request ข้าม services ว่าแต่ละจุดใช้เวลาเท่าไหร่

Request → API Gateway (5ms) → Auth Service (20ms) → Order Service (50ms)
                                                          ↓
                              Payment Service (200ms) ← ← ←
                                     ↓
                              Email Service (100ms)

Trace แสดงว่า:
- ทั้งหมดใช้เวลา 375ms
- Payment Service ช้าที่สุด (200ms) → ต้องปรับปรุง!

เครื่องมือ:
├── OpenTelemetry  ← มาตรฐาน open source
├── Jaeger         ← distributed tracing
├── Zipkin         ← distributed tracing
└── Datadog APM    ← commercial
```

---

## 2. Mitigation Strategies — กลยุทธ์รับมือปัญหา

### Graceful Degradation

```
เมื่อบางส่วนพัง → ลดคุณภาพแทนที่จะล่มทั้งหมด

ตัวอย่าง:
- Recommendation service ล่ม → แสดง "สินค้ายอดนิยม" แทน
- Image service ช้า → แสดง placeholder image
- Payment gateway timeout → แจ้งผู้ใช้ "กำลังดำเนินการ"

async function getRecommendations(userId) {
  try {
    return await recommendationService.getForUser(userId)
  } catch (error) {
    logger.warn({ userId, error }, 'Recommendation service failed')
    return await getPopularProducts()  // fallback
  }
}
```

### Circuit Breaker

```
เหมือนเบรกเกอร์ไฟฟ้า — ตัดวงจรเมื่อมีปัญหา

States:
CLOSED → ทำงานปกติ
  ↓ (error เยอะเกิน threshold)
OPEN → หยุดส่ง request ทันที (return error/fallback)
  ↓ (รอ timeout)
HALF-OPEN → ส่ง request ทดสอบ
  ↓ success → CLOSED
  ↓ fail → OPEN

const CircuitBreaker = require('opossum')

const breaker = new CircuitBreaker(callPaymentService, {
  timeout: 3000,        // timeout 3 วินาที
  errorThresholdPercentage: 50,  // error 50% → open
  resetTimeout: 30000   // รอ 30 วินาที ก่อน half-open
})

breaker.fallback(() => ({
  status: 'pending',
  message: 'Payment is being processed'
}))

breaker.on('open', () => logger.warn('Circuit breaker OPENED'))
breaker.on('close', () => logger.info('Circuit breaker CLOSED'))

const result = await breaker.fire(orderData)
```

### Rate Limiting / Throttling

```
จำกัดจำนวน request เพื่อป้องกัน overload

Algorithms:
1. Fixed Window:   100 requests / นาที
2. Sliding Window: ยืดหยุ่นกว่า fixed
3. Token Bucket:   เติม token เป็นอัตราคงที่
4. Leaky Bucket:   process request อัตราคงที่

const rateLimit = require('express-rate-limit')

const limiter = rateLimit({
  windowMs: 60 * 1000,  // 1 นาที
  max: 100,              // 100 requests/นาที
  message: { error: 'Too many requests, please try again later' },
  standardHeaders: true,
  legacyHeaders: false
})

app.use('/api/', limiter)
```

### Backpressure

```
เมื่อ consumer ทำงานไม่ทัน → บอก producer ให้ชะลอ

ไม่มี Backpressure:
Producer (1000 msg/s) → Consumer (100 msg/s) → ❌ Queue เต็ม → ข้อมูลหาย!

มี Backpressure:
Producer → "Queue เกือบเต็ม! ชะลอด้วย" → Producer ลด rate
หรือ: Producer → Queue (bounded) → reject เมื่อเต็ม → Producer retry ทีหลัง
```

### Loadshifting

```
เลื่อนงานไปทำตอนที่ traffic น้อย

ตัวอย่าง:
- สร้าง report → ไม่ต้องทำตอน peak hour → queue ไว้ทำตอนกลางคืน
- Send bulk email → queue แล้วค่อยส่งทีละ batch
- Image processing → ทำ background ไม่ต้องรอ
```

### Circuit Breaker + Retry + Timeout (รวมกัน)

```javascript
async function callServiceWithResilience(fn, options = {}) {
  const {
    maxRetries = 3,
    timeout = 5000,
    retryDelay = 1000
  } = options

  for (let attempt = 1; attempt <= maxRetries; attempt++) {
    try {
      const controller = new AbortController()
      const timer = setTimeout(() => controller.abort(), timeout)

      const result = await fn({ signal: controller.signal })
      clearTimeout(timer)
      return result

    } catch (error) {
      if (attempt === maxRetries) throw error

      logger.warn({
        attempt,
        maxRetries,
        error: error.message
      }, 'Retrying...')

      await new Promise(r => setTimeout(r, retryDelay * attempt))  // exponential backoff
    }
  }
}

// ใช้งาน
const result = await callServiceWithResilience(
  () => paymentService.charge(order),
  { maxRetries: 3, timeout: 5000 }
)
```

---

## 3. สรุป

```
Observability:
├── Logs (structured, JSON) → ELK / Loki
├── Metrics (RED, USE) → Prometheus + Grafana
└── Traces (distributed) → OpenTelemetry + Jaeger

Mitigation:
├── Graceful Degradation → fallback เมื่อ service ล่ม
├── Circuit Breaker → หยุดเรียก service ที่มีปัญหา
├── Rate Limiting → จำกัด request/วินาที
├── Backpressure → บอก producer ให้ชะลอ
├── Loadshifting → เลื่อนงานไปตอน off-peak
├── Retry + Timeout → ลองใหม่ + ตั้งเวลา
└── Throttling → จำกัด throughput

สิ่งที่ต้องทำเสมอ:
1. Monitor ทุก service (logs, metrics, traces)
2. ตั้ง alerts สำหรับ anomalies
3. ใช้ circuit breaker สำหรับ external calls
4. Rate limit ทุก public API
5. ทดสอบ failure scenarios (chaos engineering)
```
