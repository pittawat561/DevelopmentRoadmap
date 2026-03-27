# Caching - การแคชข้อมูล

> ครอบคลุม Server Side (Redis, Memcached) และ HTTP/Client Side Caching

---

## 1. Caching คืออะไร

การเก็บสำเนาข้อมูลไว้ในที่ที่เข้าถึงได้เร็วกว่า เพื่อลดเวลาในการดึงข้อมูลซ้ำ

```
ไม่มี Cache:
Client → Server → Database (ช้า ทุกครั้ง)

มี Cache:
Client → Server → Cache (เร็ว!) → ถ้าไม่มี → Database
                    ↑
              เก็บไว้ใน Cache สำหรับครั้งถัดไป
```

### ประเภทของ Cache

```
┌─────────────────────────────────────────────────────────────┐
│                     Caching Layers                          │
├─────────────┬──────────────┬──────────────┬─────────────────┤
│  Browser    │  CDN         │  Server Side │  Database       │
│  Cache      │  Cache       │  Cache       │  Query Cache    │
│             │              │              │                 │
│  - HTTP     │  - Cloudflare│  - Redis     │  - MySQL Cache  │
│    Headers  │  - AWS CF    │  - Memcached │  - Materialized │
│  - Service  │  - Fastly    │  - In-memory │    Views        │
│    Worker   │              │              │                 │
├─────────────┴──────────────┴──────────────┴─────────────────┤
│  ← เร็วที่สุด                              ช้าที่สุด →       │
│  ← ใกล้ Client                            ใกล้ Data →       │
└─────────────────────────────────────────────────────────────┘
```

### Caching Strategies (กลยุทธ์การแคช)

#### Cache-Aside (Lazy Loading) — ใช้บ่อยที่สุด

```
อ่านข้อมูล:
1. ดู Cache ก่อน
2. ถ้ามี (Cache Hit) → คืนค่าเลย
3. ถ้าไม่มี (Cache Miss) → ดึงจาก DB → เก็บใน Cache → คืนค่า

เขียนข้อมูล:
1. เขียนลง DB
2. ลบ Cache ของ key นั้น (invalidate)

// Pseudocode
function getUser(id) {
  // 1. ดู cache
  let user = cache.get(`user:${id}`)

  if (user) {
    return user  // Cache Hit!
  }

  // 2. Cache Miss → ดึงจาก DB
  user = db.query('SELECT * FROM users WHERE id = ?', [id])

  // 3. เก็บใน cache (TTL 1 ชั่วโมง)
  cache.set(`user:${id}`, user, { ttl: 3600 })

  return user
}
```

**ข้อดี:** เก็บเฉพาะข้อมูลที่ใช้จริง, ง่าย
**ข้อเสีย:** Cache Miss ครั้งแรกช้า, อาจได้ข้อมูลเก่า (stale)

#### Write-Through

```
เขียนข้อมูล:
1. เขียนลง Cache
2. Cache เขียนลง DB ทันที

อ่านข้อมูล:
1. อ่านจาก Cache เสมอ (ข้อมูลใหม่สุดเสมอ)

function updateUser(id, data) {
  // เขียนทั้ง cache และ DB พร้อมกัน
  cache.set(`user:${id}`, data)
  db.update('users', id, data)
}
```

**ข้อดี:** ข้อมูลใน cache ใหม่เสมอ
**ข้อเสีย:** เขียนช้าขึ้น (ต้องเขียน 2 ที่), เก็บข้อมูลที่ไม่จำเป็น

#### Write-Behind (Write-Back)

```
เขียนข้อมูล:
1. เขียนลง Cache เท่านั้น (เร็ว!)
2. Cache เขียนลง DB ภายหลัง (async, batch)

function updateUser(id, data) {
  cache.set(`user:${id}`, data)    // เร็ว!
  queue.add('write-db', { id, data }) // เขียน DB ทีหลัง
}
```

**ข้อดี:** เขียนเร็วมาก
**ข้อเสีย:** อาจสูญเสียข้อมูลถ้า cache ล่มก่อนเขียน DB

### Cache Invalidation (การทำให้ Cache หมดอายุ)

```
"There are only two hard things in Computer Science:
 cache invalidation and naming things."

วิธีจัดการ:

1. TTL (Time To Live)
   cache.set("user:123", data, { ttl: 3600 })  // หมดอายุใน 1 ชม.

2. Event-based Invalidation
   เมื่อข้อมูลเปลี่ยน → ลบ cache ทันที
   function updateUser(id, data) {
     db.update(id, data)
     cache.delete(`user:${id}`)
   }

3. Version-based
   cache key: "user:123:v5"  // เปลี่ยน version เมื่อข้อมูลเปลี่ยน

ปัญหาที่พบบ่อย:
- Stale Data:   cache ยังเป็นข้อมูลเก่า → ใช้ TTL สั้น
- Cache Stampede: cache หมดอายุ → request จำนวนมากไปที่ DB พร้อมกัน
                  → ใช้ lock หรือ stale-while-revalidate
- Thundering Herd: คล้าย stampede แต่เกิดกับ key จำนวนมาก
                   → ตั้ง TTL ต่างกัน (jitter)
```

### Eviction Policies (นโยบายลบข้อมูลเมื่อ Cache เต็ม)

```
| Policy   | คำอธิบาย                          | เหมาะกับ              |
|----------|----------------------------------|-----------------------|
| LRU      | ลบที่ไม่ได้ใช้นานที่สุด             | ใช้บ่อยที่สุด ✅       |
| LFU      | ลบที่ใช้น้อยครั้งที่สุด              | ข้อมูลยอดนิยม          |
| FIFO     | ลบอันที่เข้ามาก่อน                  | ง่าย                   |
| TTL      | ลบตามเวลาที่กำหนด                 | ข้อมูลที่มีอายุจำกัด     |
| Random   | ลบสุ่ม                            | เมื่อไม่มี pattern ชัด   |
```

---

## 2. Redis

### Redis คืออะไร

Redis (Remote Dictionary Server) คือ **in-memory data structure store** แบบ open-source รองรับ strings, lists, sets, hashes, sorted sets ใช้ได้ทั้ง caching, session management, message broker, real-time analytics

### คุณสมบัติหลัก

```
✅ เร็วมาก — ทำงานใน RAM (~100,000+ operations/วินาที)
✅ Data Structures หลากหลาย (ไม่ใช่แค่ key-value)
✅ Persistence — เก็บข้อมูลลง disk ได้ (RDB, AOF)
✅ Replication — Master-Replica สำหรับ high availability
✅ Clustering — กระจายข้อมูลข้าม nodes
✅ Pub/Sub — ส่งข้อความ real-time
✅ Lua Scripting — รัน script บน server
✅ TTL — ตั้งเวลาหมดอายุ per key
```

### Data Types

#### Strings (พื้นฐานที่สุด)

```bash
# ตั้งค่า / อ่านค่า
SET user:123:name "John Doe"
GET user:123:name              # → "John Doe"

# ตั้งค่าพร้อม TTL
SET session:abc123 "user_data" EX 3600    # หมดอายุใน 1 ชม.
SETEX session:abc123 3600 "user_data"     # แบบเดียวกัน

# ตั้งเฉพาะเมื่อยังไม่มี (distributed lock)
SETNX lock:order:456 "locked"

# Counter (atomic)
SET page:views 0
INCR page:views               # → 1
INCR page:views               # → 2
INCRBY page:views 10          # → 12

# ใช้กับ: cache ค่าง่ายๆ, session, counter, rate limiter
```

#### Hashes (เหมือน object/dictionary)

```bash
# เก็บข้อมูลผู้ใช้เป็น hash
HSET user:123 name "John" email "john@example.com" age 30

# อ่านทีละ field
HGET user:123 name             # → "John"
HGET user:123 email            # → "john@example.com"

# อ่านทั้งหมด
HGETALL user:123
# → name "John" email "john@example.com" age "30"

# อัปเดต field เดียว
HSET user:123 age 31

# ลบ field
HDEL user:123 email

# เช็คว่ามี field ไหม
HEXISTS user:123 name          # → 1 (true)

# ใช้กับ: เก็บ object (user profile, product, settings)
```

#### Lists (ลำดับ — เหมาะกับ queue)

```bash
# เพิ่มข้อมูล
LPUSH queue:emails "email1"    # เพิ่มหัว
LPUSH queue:emails "email2"
RPUSH queue:emails "email3"    # เพิ่มท้าย

# ดึงข้อมูล (queue — FIFO)
RPOP queue:emails              # → "email1" (ดึงจากท้าย)

# Blocking pop (รอจนมีข้อมูล — เหมาะกับ worker)
BRPOP queue:emails 30          # รอ 30 วินาที

# ดูข้อมูลทั้งหมด
LRANGE queue:emails 0 -1       # ดูทั้ง list

# จำกัดขนาด (เก็บแค่ 100 อันล่าสุด)
LPUSH recent:orders "order:789"
LTRIM recent:orders 0 99       # เก็บแค่ 100 แรก

# ใช้กับ: message queue, recent items, activity feed
```

#### Sets (ไม่ซ้ำ ไม่เรียงลำดับ)

```bash
# เพิ่มสมาชิก
SADD user:123:tags "backend" "nodejs" "redis"
SADD user:456:tags "frontend" "react" "nodejs"

# ดูสมาชิกทั้งหมด
SMEMBERS user:123:tags         # → "backend" "nodejs" "redis"

# เช็คว่ามีสมาชิกไหม
SISMEMBER user:123:tags "redis"  # → 1 (true)

# Intersection (tags ที่ทั้งสองคนมี)
SINTER user:123:tags user:456:tags   # → "nodejs"

# Union (tags ทั้งหมดรวมกัน)
SUNION user:123:tags user:456:tags   # → "backend" "nodejs" "redis" "frontend" "react"

# Difference
SDIFF user:123:tags user:456:tags    # → "backend" "redis"

# ใช้กับ: tags, unique visitors, mutual friends, online users
```

#### Sorted Sets (เรียงลำดับด้วย score)

```bash
# Leaderboard
ZADD leaderboard 1500 "player:1"
ZADD leaderboard 2300 "player:2"
ZADD leaderboard 1800 "player:3"

# Top 3 (score มากไปน้อย)
ZREVRANGE leaderboard 0 2 WITHSCORES
# → "player:2" 2300, "player:3" 1800, "player:1" 1500

# อันดับของผู้เล่น
ZREVRANK leaderboard "player:2"   # → 0 (อันดับ 1)

# เพิ่ม score
ZINCRBY leaderboard 500 "player:1"  # 1500 + 500 = 2000

# ดึงตาม score range
ZRANGEBYSCORE leaderboard 1000 2000

# ใช้กับ: leaderboard, priority queue, rate limiter, timeline
```

### Redis ใช้งานจริง

#### Caching

```javascript
const Redis = require('ioredis')
const redis = new Redis()  // localhost:6379

async function getUser(id) {
  const cacheKey = `user:${id}`

  // 1. ดู cache
  const cached = await redis.get(cacheKey)
  if (cached) {
    return JSON.parse(cached)  // Cache Hit
  }

  // 2. Cache Miss → ดึง DB
  const user = await db.users.findById(id)

  // 3. เก็บ cache (TTL 1 ชม.)
  await redis.setex(cacheKey, 3600, JSON.stringify(user))

  return user
}

// Invalidate เมื่อข้อมูลเปลี่ยน
async function updateUser(id, data) {
  await db.users.update(id, data)
  await redis.del(`user:${id}`)  // ลบ cache
}
```

#### Session Store

```javascript
// Express + Redis Session
const session = require('express-session')
const RedisStore = require('connect-redis').default

app.use(session({
  store: new RedisStore({ client: redis }),
  secret: 'my-secret',
  resave: false,
  saveUninitialized: false,
  cookie: {
    maxAge: 24 * 60 * 60 * 1000,  // 24 ชม.
    httpOnly: true,
    secure: true
  }
}))
```

#### Rate Limiter

```javascript
async function rateLimiter(userId, limit = 100, windowSecs = 60) {
  const key = `ratelimit:${userId}`
  const current = await redis.incr(key)

  if (current === 1) {
    await redis.expire(key, windowSecs)
  }

  if (current > limit) {
    throw new Error('Rate limit exceeded')
  }

  return { remaining: limit - current }
}
```

#### Pub/Sub (Real-time messaging)

```javascript
// Publisher
const pub = new Redis()
pub.publish('notifications', JSON.stringify({
  userId: 123,
  message: 'คุณมีออเดอร์ใหม่'
}))

// Subscriber
const sub = new Redis()
sub.subscribe('notifications')
sub.on('message', (channel, message) => {
  const data = JSON.parse(message)
  console.log(`Channel: ${channel}, Data:`, data)
})
```

### Redis Persistence

```
RDB (Redis Database):
- Snapshot ข้อมูลลง disk เป็นระยะ
- เร็ว, ไฟล์เล็ก
- อาจสูญเสียข้อมูลช่วงสุดท้ายก่อน snapshot
- เหมาะกับ: backup, disaster recovery

AOF (Append Only File):
- บันทึกทุก write operation ลง log
- ปลอดภัยกว่า (สูญเสียข้อมูลน้อยกว่า)
- ไฟล์ใหญ่กว่า, ช้ากว่า RDB
- เหมาะกับ: ข้อมูลสำคัญ

แนะนำ: ใช้ทั้ง RDB + AOF ร่วมกัน
```

### Redis Cluster & Sentinel

```
Redis Sentinel (High Availability):
- ตรวจสอบ master ว่ายังทำงานอยู่ไหม
- ถ้า master ล่ม → promote replica เป็น master ใหม่อัตโนมัติ

┌──────────┐
│  Master  │ ←── Sentinel ตรวจสอบ
└────┬─────┘
     │ Replication
┌────┴─────┐  ┌──────────┐
│ Replica 1│  │ Replica 2│
└──────────┘  └──────────┘

Redis Cluster (Scaling):
- กระจายข้อมูลข้าม nodes (sharding)
- แต่ละ node รับผิดชอบ hash slots
- รองรับข้อมูลมากกว่า RAM ของเครื่องเดียว

┌──────────┐  ┌──────────┐  ┌──────────┐
│ Node 1   │  │ Node 2   │  │ Node 3   │
│ slots    │  │ slots    │  │ slots    │
│ 0-5460   │  │ 5461-10922│ │10923-16383│
└──────────┘  └──────────┘  └──────────┘
```

---

## 3. Memcached

### Memcached คืออะไร

Memcached คือ **distributed memory-caching system** ที่เก็บข้อมูลเป็น key-value ใน RAM เพื่อเร่งความเร็วเว็บไซต์ที่ต้องดึงข้อมูลจาก database บ่อยๆ

### คุณสมบัติ

```
✅ เร็วมาก — ง่ายและเร็ว (pure key-value)
✅ Multi-threaded — ใช้หลาย CPU cores ได้
✅ Distributed — กระจายข้อมูลข้ามหลายเครื่อง
✅ LRU eviction — ลบข้อมูลเก่าอัตโนมัติเมื่อเต็ม
✅ ง่ายมาก — แค่ get/set/delete

❌ ไม่มี persistence (ข้อมูลหายเมื่อ restart)
❌ key-value เท่านั้น (ไม่มี data structures)
❌ ไม่มี replication built-in
❌ value สูงสุด 1MB
```

### การใช้งาน

```javascript
const Memcached = require('memcached')
const cache = new Memcached('localhost:11211')

// Set (key, value, TTL in seconds)
cache.set('user:123', JSON.stringify(userData), 3600, (err) => {
  if (err) console.error(err)
})

// Get
cache.get('user:123', (err, data) => {
  if (data) {
    const user = JSON.parse(data)  // Cache Hit
  } else {
    // Cache Miss → ดึง DB
  }
})

// Delete
cache.del('user:123', (err) => {})

// Increment (atomic)
cache.incr('page:views', 1, (err, newValue) => {})
```

### Distributed Memcached

```
Client ใช้ consistent hashing กระจาย key ไปหลาย server:

Key "user:123" → hash → Server A
Key "user:456" → hash → Server B
Key "user:789" → hash → Server C

// เชื่อมต่อหลาย server
const cache = new Memcached([
  'server1:11211',
  'server2:11211',
  'server3:11211'
])

// Client จัดการ distribution เอง
// Server แต่ละตัวไม่รู้จักกัน
```

---

## 4. Redis vs Memcached

```
| หัวข้อ              | Redis              | Memcached          |
|--------------------|--------------------|--------------------|
| Data Types         | หลากหลาย           | key-value เท่านั้น  |
| Persistence        | ✅ RDB + AOF       | ❌ ไม่มี            |
| Replication        | ✅ Master-Replica   | ❌ ไม่มี            |
| Clustering         | ✅ Redis Cluster    | Client-side only   |
| Threading          | Single-threaded*   | Multi-threaded     |
| Max Value Size     | 512MB              | 1MB                |
| Pub/Sub           | ✅                  | ❌                  |
| Lua Scripting      | ✅                  | ❌                  |
| Memory Efficiency  | น้อยกว่า            | มากกว่า             |
| ความเร็ว (simple)   | เร็วมาก            | เร็วมาก (เท่ากัน)   |

* Redis 6+ มี I/O threading แล้ว

เลือกอะไร:
├── ต้องการ data structures (lists, sets, sorted sets)?  → Redis
├── ต้องการ persistence?                                 → Redis
├── ต้องการ Pub/Sub / message broker?                    → Redis
├── ต้องการ cache ง่ายๆ ไม่ซับซ้อน?                       → Memcached ก็ได้
├── ต้องการ multi-threaded performance?                  → Memcached
└── ไม่แน่ใจ?                                            → Redis ✅
```

---

## 5. HTTP Caching (Client-Side Caching)

### HTTP Caching คืออะไร

การเก็บ response ไว้ที่ browser/CDN/proxy เพื่อไม่ต้องดึงข้อมูลจาก server ซ้ำ

```
Request แรก:
Browser → Server → Response + Cache Headers
Browser เก็บ response ไว้ใน cache

Request ถัดไป:
Browser → ดู cache → ยังไม่หมดอายุ → ใช้จาก cache เลย (ไม่ยิง server!)
หรือ
Browser → ดู cache → หมดอายุแล้ว → ถาม server ว่าเปลี่ยนไหม
```

### Cache-Control Header

```
// Server ตอบกลับด้วย Cache-Control

// 1. max-age — cache ได้กี่วินาที
Cache-Control: max-age=3600          // cache 1 ชม.
Cache-Control: max-age=86400         // cache 1 วัน
Cache-Control: max-age=31536000      // cache 1 ปี (static assets)

// 2. ห้าม cache
Cache-Control: no-store              // ห้าม cache เลย (ข้อมูลลับ)
Cache-Control: no-cache              // cache ได้ แต่ต้องถาม server ก่อนใช้

// 3. public / private
Cache-Control: public, max-age=3600  // CDN + Browser cache ได้
Cache-Control: private, max-age=3600 // Browser เท่านั้น (ข้อมูลส่วนตัว)

// 4. must-revalidate
Cache-Control: max-age=3600, must-revalidate
// หมดอายุแล้วต้องถาม server ก่อน ห้ามใช้ stale

// 5. stale-while-revalidate
Cache-Control: max-age=60, stale-while-revalidate=300
// ใช้ cache เก่าได้อีก 5 นาทีขณะดึงข้อมูลใหม่ (background refresh)

// 6. immutable (static assets ที่ไม่เปลี่ยน)
Cache-Control: public, max-age=31536000, immutable
// ใช้กับ: /assets/app.a1b2c3.js (filename มี hash)
```

### ETag (Entity Tag)

```
// Request แรก
GET /api/users/123
→ Response:
HTTP/1.1 200 OK
ETag: "abc123"
Cache-Control: no-cache
{ "name": "John", "email": "john@example.com" }

// Request ถัดไป — Browser ส่ง ETag กลับ
GET /api/users/123
If-None-Match: "abc123"

// กรณี 1: ข้อมูลไม่เปลี่ยน
→ Response:
HTTP/1.1 304 Not Modified
(ไม่ส่ง body → ใช้ cache เดิม → ประหยัด bandwidth)

// กรณี 2: ข้อมูลเปลี่ยนแล้ว
→ Response:
HTTP/1.1 200 OK
ETag: "def456"
{ "name": "Jane", "email": "jane@example.com" }
```

### Last-Modified

```
// คล้าย ETag แต่ใช้เวลา
GET /api/users/123
→ Response:
Last-Modified: Wed, 15 Jan 2024 10:00:00 GMT

// Request ถัดไป
GET /api/users/123
If-Modified-Since: Wed, 15 Jan 2024 10:00:00 GMT

→ 304 Not Modified (ถ้าไม่เปลี่ยน)
→ 200 OK + ข้อมูลใหม่ (ถ้าเปลี่ยน)

ETag vs Last-Modified:
- ETag แม่นยำกว่า (hash ของข้อมูล)
- Last-Modified ความแม่นยำระดับวินาที
- แนะนำใช้ ETag
```

### Caching Strategy ตามประเภทข้อมูล

```
| ประเภท                | Cache-Control                           |
|-----------------------|----------------------------------------|
| API Response (ส่วนตัว) | private, no-cache + ETag               |
| API Response (สาธารณะ) | public, max-age=300                    |
| HTML Page             | no-cache + ETag                         |
| CSS/JS (hashed name)  | public, max-age=31536000, immutable    |
| รูปภาพ                | public, max-age=86400                   |
| ข้อมูลผู้ใช้ (sensitive) | private, no-store                      |
| Static API (ไม่เปลี่ยน) | public, max-age=86400, stale-while-revalidate=3600 |
```

### Service Worker Cache (Advanced)

```javascript
// Service Worker — cache offline + เลือก strategy ได้
self.addEventListener('fetch', (event) => {
  event.respondWith(
    caches.match(event.request).then((cached) => {
      // Cache First: ใช้ cache ก่อน ถ้าไม่มีค่อยดึง network
      if (cached) return cached

      return fetch(event.request).then((response) => {
        // เก็บ response ใน cache
        const clone = response.clone()
        caches.open('v1').then((cache) => {
          cache.put(event.request, clone)
        })
        return response
      })
    })
  )
})

// Strategies:
// Cache First     → เร็ว แต่อาจได้ข้อมูลเก่า (static assets)
// Network First   → ข้อมูลใหม่เสมอ ช้ากว่า (API data)
// Stale While Revalidate → ใช้ cache เลย + อัปเดต background
```

---

## 6. CDN Caching

```
CDN (Content Delivery Network):
เก็บ cache ไว้ที่ server ใกล้ผู้ใช้ทั่วโลก

ไม่มี CDN:
ผู้ใช้ไทย → Server อเมริกา (200ms latency)

มี CDN:
ผู้ใช้ไทย → CDN Edge สิงคโปร์ (20ms latency!)

CDN Providers:
- Cloudflare (ฟรี tier ดีมาก)
- AWS CloudFront
- Fastly
- Akamai

ตั้งค่า:
// Server บอก CDN ว่า cache ได้
Cache-Control: public, s-maxage=3600, max-age=60
// s-maxage → CDN cache 1 ชม.
// max-age → Browser cache 1 นาที

// Purge CDN cache เมื่อข้อมูลเปลี่ยน
// (ผ่าน API ของ CDN provider)
```

---

## 7. สรุป

```
| เมื่อต้องการ                    | ใช้                        |
|--------------------------------|---------------------------|
| Cache API response ฝั่ง client  | HTTP Caching (Cache-Control, ETag) |
| Cache static assets            | CDN + immutable headers    |
| Cache database queries         | Redis (Cache-Aside)        |
| Session storage                | Redis                      |
| Simple key-value cache         | Redis หรือ Memcached       |
| Message queue                  | Redis (Lists / Pub/Sub)    |
| Leaderboard / ranking          | Redis (Sorted Sets)        |
| Real-time counter              | Redis (INCR)               |
| Offline web app                | Service Worker              |

แผนการเรียน:
1. HTTP Caching ← เข้าใจก่อน (พื้นฐานที่ใช้ทุกวัน)
2. Redis ← สำคัญที่สุดสำหรับ backend
3. Caching Strategies ← Cache-Aside, Write-Through
4. CDN ← เมื่อต้อง serve content ทั่วโลก
5. Memcached ← เรียนเปรียบเทียบกับ Redis
```
