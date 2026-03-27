# Web Security & API Security Best Practices

> ครอบคลุม Hashing, HTTPS, SSL/TLS, OWASP, CORS, CSP, Server Security และ Best Practices

---

## 1. Hashing Algorithms (อัลกอริทึมแฮช)

### Hashing คืออะไร

การแปลงข้อมูลให้เป็น **string ความยาวคงที่** แบบ **ทางเดียว** (ย้อนกลับไม่ได้)

```
input  →  hash function  →  output (fixed length)
"hello" →     MD5        →  "5d41402abc4b2a76b9719d911017c592"
"hello" →     SHA256     →  "2cf24dba5fb0a30e26e83b2ac5b9e29e..."

คุณสมบัติ:
1. ทางเดียว — ย้อน hash กลับเป็น input ไม่ได้
2. ผลคงที่ — input เดิม → hash เดิมเสมอ
3. Avalanche Effect — input เปลี่ยนนิดเดียว hash เปลี่ยนทั้งหมด
4. ป้องกัน Collision — input ต่างกันไม่ควรได้ hash เดียวกัน
```

### MD5 (Message Digest 5)

```
Output: 128-bit (32 hex characters)

"hello"    → 5d41402abc4b2a76b9719d911017c592
"hello!"   → f572d396fae9206628714fb2ce00f72e  (ต่างสิ้นเชิง)

⚠️ สถานะ: ไม่ปลอดภัยแล้ว
- พบ collision ได้ง่าย (2 input ต่างกันแต่ hash เหมือนกัน)
- เร็วเกินไป → brute force ง่าย

❌ ห้ามใช้กับ: password, digital signatures, security
✅ ใช้ได้กับ: checksum ตรวจสอบไฟล์ (non-security), deduplication
```

### SHA (Secure Hash Algorithm)

```
SHA Family:
| อัลกอริทึม | Output    | สถานะ         |
|------------|-----------|---------------|
| SHA-1      | 160-bit   | ❌ ไม่ปลอดภัย  |
| SHA-256    | 256-bit   | ✅ ใช้ได้       |
| SHA-384    | 384-bit   | ✅ ใช้ได้       |
| SHA-512    | 512-bit   | ✅ ใช้ได้       |
| SHA-3      | หลายขนาด  | ✅ ใหม่ล่าสุด   |

// ตัวอย่าง SHA-256
"hello" → 2cf24dba5fb0a30e26e83b2ac5b9e29e1b161e5c1fa7425e73043362938b9824

ใช้งาน:
- Digital signatures (SSL/TLS certificates)
- Data integrity (ตรวจสอบว่าไฟล์ไม่ถูกแก้ไข)
- Blockchain (Bitcoin ใช้ SHA-256)
- HMAC (Hash-based Message Authentication Code)

⚠️ ไม่เหมาะกับ password — เร็วเกินไป (ต้องใช้ bcrypt/scrypt แทน)
```

### bcrypt

```
อัลกอริทึมออกแบบมาเฉพาะสำหรับ hash password

คุณสมบัติ:
- ช้าโดยเจตนา (ป้องกัน brute force)
- มี salt ในตัว (ป้องกัน rainbow table)
- ปรับความช้าได้ด้วย cost factor

// bcrypt output format
$2b$12$LJ3m4ys3Lg7E/cOqGkB.4OZtGh8X.oJKR2C.sY1JZ4FUfEiQHyC6
│   │  │
│   │  └── salt + hash (combined)
│   └── cost factor (2^12 = 4096 rounds)
└── algorithm version (2b)

// ใช้งาน (Node.js)
const bcrypt = require('bcrypt')

// Hash password
const saltRounds = 12  // cost factor (10-12 เหมาะสม)
const hash = await bcrypt.hash("myPassword123", saltRounds)
// → "$2b$12$LJ3m4ys3Lg7E/cOqGkB.4OZtGh8X.oJKR2C.sY1JZ4FUfEiQHyC6"

// ตรวจสอบ password
const match = await bcrypt.compare("myPassword123", hash)
// → true

// Cost factor กับเวลา (โดยประมาณ):
// cost 10: ~100ms
// cost 12: ~300ms  (แนะนำ)
// cost 14: ~1s
// cost 16: ~4s

✅ ใช้สำหรับ: hash password
✅ ข้อดี: salt อัตโนมัติ, ช้าพอดี, ใช้ง่าย
```

### scrypt

```
อัลกอริทึม hash password ที่ใช้ memory เยอะ (memory-hard)
ยากต่อการ brute force ด้วย GPU/ASIC

คุณสมบัติ:
- ใช้ CPU + Memory สูง
- ปรับ parameters ได้: N (CPU/memory cost), r (block size), p (parallelism)
- ปลอดภัยกว่า bcrypt สำหรับ hardware attack

// ใช้งาน (Node.js)
const crypto = require('crypto')

const password = "myPassword123"
const salt = crypto.randomBytes(16)

crypto.scrypt(password, salt, 64, {
  N: 16384,    // CPU/memory cost (power of 2)
  r: 8,        // block size
  p: 1         // parallelism
}, (err, derivedKey) => {
  const hash = derivedKey.toString('hex')
})

เปรียบเทียบ bcrypt vs scrypt:
| หัวข้อ         | bcrypt       | scrypt          |
|---------------|-------------|-----------------|
| ป้องกัน GPU    | ปานกลาง     | ดีมาก (memory-hard) |
| ความง่าย       | ง่ายมาก     | ต้องตั้ง params   |
| ใช้แพร่หลาย    | มากที่สุด   | น้อยกว่า          |
| แนะนำสำหรับ    | ทั่วไป      | security สูง     |
```

### Argon2 (เพิ่มเติม — ไม่อยู่ใน roadmap แต่ควรรู้)

```
ชนะ Password Hashing Competition 2015
ถือเป็นอัลกอริทึม hash password ที่ดีที่สุดในปัจจุบัน

Variants:
- Argon2d  — ป้องกัน GPU attacks (data-depending)
- Argon2i  — ป้องกัน side-channel attacks
- Argon2id — รวมทั้งสอง (แนะนำ)

ถ้าเลือกได้ → ใช้ Argon2id
ถ้าใช้ไม่ได้ → ใช้ bcrypt (ดีเพียงพอ)
```

### สรุป Hashing

```
| ใช้สำหรับ          | ใช้               | ห้ามใช้        |
|-------------------|-------------------|---------------|
| Hash password     | bcrypt, scrypt    | MD5, SHA      |
|                   | Argon2id          |               |
| Data integrity    | SHA-256, SHA-3    | MD5           |
| Digital signature | SHA-256, SHA-512  | MD5, SHA-1    |
| Checksum ไฟล์     | SHA-256, MD5 (ok) |               |
| HMAC             | SHA-256           |               |
```

---

## 2. HTTPS

### HTTPS คืออะไร

HTTP + TLS (Transport Layer Security) = การสื่อสาร HTTP ที่ถูกเข้ารหัส

```
HTTP:  ข้อมูลส่งแบบ plain text → ดักจับอ่านได้
HTTPS: ข้อมูลถูกเข้ารหัส → ดักจับแล้วอ่านไม่ได้

http://api.example.com   → ❌ ไม่ปลอดภัย
https://api.example.com  → ✅ ปลอดภัย

HTTPS ป้องกัน:
1. การดักฟัง (Eavesdropping) — ข้อมูลถูกเข้ารหัส
2. การแก้ไข (Tampering) — ตรวจจับได้ว่าข้อมูลถูกแก้
3. การปลอมตัว (Impersonation) — ยืนยัน server ด้วย certificate
```

---

## 3. SSL/TLS (Secure Sockets Layer / Transport Layer Security)

### SSL vs TLS

```
SSL → เวอร์ชันเก่า (SSL 1.0, 2.0, 3.0) — ❌ ล้าสมัย ไม่ปลอดภัย
TLS → เวอร์ชันใหม่ที่แทนที่ SSL

| เวอร์ชัน   | ปี   | สถานะ              |
|-----------|------|-------------------|
| SSL 2.0   | 1995 | ❌ ล้าสมัย          |
| SSL 3.0   | 1996 | ❌ ล้าสมัย          |
| TLS 1.0   | 1999 | ❌ ล้าสมัย          |
| TLS 1.1   | 2006 | ❌ ล้าสมัย          |
| TLS 1.2   | 2008 | ✅ ใช้ได้           |
| TLS 1.3   | 2018 | ✅ แนะนำ (เร็วที่สุด)|

หมายเหตุ: คนยังเรียก "SSL" แต่จริงๆ ใช้ TLS แล้ว
```

### TLS Handshake (TLS 1.2)

```
Client                                Server
  │                                      │
  │ 1. ClientHello                       │
  │    (TLS version, cipher suites,      │
  │     random number)              ──→  │
  │                                      │
  │ 2. ServerHello                       │
  │    (เลือก cipher suite,              │
  │     random number)              ←──  │
  │                                      │
  │ 3. Server Certificate                │
  │    (SSL/TLS certificate)        ←──  │
  │                                      │
  │ 4. Client ตรวจสอบ certificate        │
  │    สร้าง pre-master secret           │
  │    เข้ารหัสด้วย public key ของ server │
  │                                 ──→  │
  │                                      │
  │ 5. ทั้งสองฝ่ายสร้าง session key      │
  │    จาก pre-master secret             │
  │                                      │
  │ 6. เริ่มส่งข้อมูลที่เข้ารหัสด้วย      │
  │    symmetric encryption (AES)         │
  │ ←════════ encrypted data ═══════→    │

TLS 1.3: เร็วกว่า — handshake 1 round trip (แทนที่จะ 2)
```

### SSL/TLS Certificate

```
Certificate มีข้อมูล:
- ชื่อ domain (เช่น api.example.com)
- Public key ของ server
- ผู้ออก certificate (Certificate Authority - CA)
- วันหมดอายุ
- Digital signature ของ CA

ประเภท Certificate:
| ประเภท    | ตรวจสอบ              | ราคา   |
|-----------|---------------------|--------|
| DV (Domain)  | เป็นเจ้าของ domain    | ฟรี-ถูก |
| OV (Organization) | ตรวจสอบองค์กร   | ปานกลาง |
| EV (Extended)     | ตรวจสอบเข้มงวด  | แพง    |

Certificate Authority (CA) ที่ใช้บ่อย:
- Let's Encrypt (ฟรี ✅)
- DigiCert
- Cloudflare (ฟรี ✅)
- AWS Certificate Manager (ฟรีถ้าใช้ AWS)
```

---

## 4. OWASP Top 10 API Security Risks

OWASP (Open Web Application Security Project) จัดอันดับช่องโหว่ API ที่พบบ่อยที่สุด

### 1. Broken Object Level Authorization (BOLA)

```
ปัญหา: เข้าถึง resource ของคนอื่นได้

❌ ช่องโหว่:
GET /api/users/123/orders → เห็นออเดอร์ของตัวเอง
GET /api/users/456/orders → เห็นออเดอร์ของคนอื่น!

✅ ป้องกัน:
// ตรวจสอบ ownership ทุก request
app.get('/api/users/:id/orders', (req, res) => {
  if (req.params.id !== req.user.id) {
    return res.status(403).json({ error: "Forbidden" })
  }
  // ดึงข้อมูล...
})
```

### 2. Broken Authentication

```
ปัญหา: ระบบ auth มีช่องโหว่

❌ ช่องโหว่:
- ไม่จำกัดจำนวนครั้ง login ผิด
- JWT ไม่ validate signature
- Token ไม่หมดอายุ
- Password ง่ายเกินไป

✅ ป้องกัน:
- Rate limit login attempts (เช่น 5 ครั้ง/5 นาที)
- ใช้ strong password policy
- ตั้ง token expiration
- ใช้ MFA (Multi-Factor Authentication)
- Lock account หลัง login ผิดหลายครั้ง
```

### 3. Broken Object Property Level Authorization

```
ปัญหา: แก้ไข field ที่ไม่ควรแก้ได้

❌ ช่องโหว่ (Mass Assignment):
PUT /api/users/123
{ "name": "John", "role": "admin" }  ← เปลี่ยน role ตัวเองเป็น admin!

✅ ป้องกัน:
// Whitelist fields ที่อนุญาตให้แก้ไข
const allowedFields = ['name', 'email', 'avatar']
const updates = pick(req.body, allowedFields)  // เอาเฉพาะ field ที่อนุญาต
```

### 4. Unrestricted Resource Consumption

```
ปัญหา: ไม่จำกัดทรัพยากร

❌ ช่องโหว่:
GET /api/users?limit=1000000    ← ดึงทีเดียวล้านรายการ
POST /api/upload (ไฟล์ 10GB)    ← upload ไฟล์ใหญ่เกินไป

✅ ป้องกัน:
- ตั้ง max limit สำหรับ pagination
- จำกัดขนาด request body
- Rate limiting
- จำกัดขนาด file upload
```

### 5. Broken Function Level Authorization

```
ปัญหา: เข้าถึง admin function ได้โดยไม่มีสิทธิ์

❌ ช่องโหว่:
// ผู้ใช้ทั่วไปเดา URL admin
DELETE /api/admin/users/456  ← ลบผู้ใช้คนอื่นได้!

✅ ป้องกัน:
// ตรวจสอบ role ทุก endpoint
app.delete('/api/admin/users/:id', requireRole('admin'), (req, res) => {
  // ...
})
```

### 6. Server Side Request Forgery (SSRF)

```
ปัญหา: หลอก server ให้ request ไปที่ internal resource

❌ ช่องโหว่:
POST /api/fetch-url
{ "url": "http://internal-server:8080/admin" }
← server ดึง internal resource แทน attacker!

✅ ป้องกัน:
- Whitelist URL ที่อนุญาต
- ห้าม request ไป private IP (127.0.0.1, 10.x.x.x, 192.168.x.x)
- ใช้ DNS resolution validation
```

### 7-10. อื่นๆ

```
7. Security Misconfiguration
   - ลืมปิด debug mode
   - ใช้ default credentials
   - เปิด CORS allow all (*)
   → ตรวจสอบ config ก่อน deploy

8. Lack of Protection from Automated Threats
   - Bot scraping ข้อมูล
   - Credential stuffing
   → ใช้ CAPTCHA, rate limiting, bot detection

9. Improper Asset Management
   - API version เก่ายังเปิดอยู่
   - Endpoint ที่ไม่ใช้แล้วยังทำงาน
   → ทำ API inventory, ปิด version เก่า

10. Unsafe Consumption of APIs
    - ไว้ใจ third-party API มากเกินไป
    - ไม่ validate response จาก external API
    → Validate ทุก input แม้จาก trusted source
```

---

## 5. CORS (Cross-Origin Resource Sharing)

(ดูรายละเอียดเพิ่มเติมใน api_design.md หัวข้อ 2.7)

```
// สรุปสั้นๆ
// ป้องกัน: เว็บ A เรียก API ของเว็บ B โดยไม่ได้รับอนุญาต

// Server ตั้งค่า CORS Headers:
Access-Control-Allow-Origin: https://myapp.com    ← อนุญาต origin ไหน
Access-Control-Allow-Methods: GET, POST, PUT       ← อนุญาต method ไหน
Access-Control-Allow-Headers: Authorization        ← อนุญาต header ไหน
Access-Control-Allow-Credentials: true             ← อนุญาตส่ง cookie ไหม

// ❌ อย่าใช้ใน production:
Access-Control-Allow-Origin: *    ← อนุญาตทุก origin (ไม่ปลอดภัย)
```

---

## 6. CSP (Content Security Policy)

### CSP คืออะไร

HTTP Header ที่บอก browser ว่าอนุญาตให้โหลด resource จากที่ไหนบ้าง — ป้องกัน **XSS** และ **data injection**

```
// ตั้ง CSP ผ่าน HTTP Header
Content-Security-Policy: default-src 'self'; script-src 'self' https://cdn.example.com

// หรือผ่าน meta tag
<meta http-equiv="Content-Security-Policy"
      content="default-src 'self'; script-src 'self'">
```

### Directives ที่สำคัญ

```
Content-Security-Policy:
  default-src 'self';                     ← ค่าเริ่มต้น: โหลดจาก origin เดียวกัน
  script-src 'self' https://cdn.js.com;   ← JavaScript จาก self + cdn
  style-src 'self' 'unsafe-inline';       ← CSS จาก self + inline styles
  img-src 'self' https: data:;            ← รูปจาก self + HTTPS + data URI
  font-src 'self' https://fonts.google.com;← ฟอนต์
  connect-src 'self' https://api.example.com;← API calls (fetch, XHR)
  frame-src 'none';                       ← ห้าม iframe
  object-src 'none';                      ← ห้าม Flash/plugins
  base-uri 'self';                        ← จำกัด <base> tag
  form-action 'self';                     ← form ส่งได้เฉพาะ self
  upgrade-insecure-requests;              ← อัปเกรด HTTP → HTTPS อัตโนมัติ

Directive Values:
'self'          → origin เดียวกัน
'none'          → ไม่อนุญาตเลย
'unsafe-inline' → อนุญาต inline script/style (พยายามหลีกเลี่ยง)
'unsafe-eval'   → อนุญาต eval() (หลีกเลี่ยง)
https:          → อนุญาตทุก HTTPS source
data:           → อนุญาต data: URI
'nonce-abc123'  → อนุญาต script ที่มี nonce ตรงกัน
```

### ตัวอย่าง CSP สำหรับ API

```
// API Server — เข้มงวดมาก (ไม่ serve HTML)
Content-Security-Policy: default-src 'none'; frame-ancestors 'none'

// Web App — ผ่อนปรนกว่า
Content-Security-Policy:
  default-src 'self';
  script-src 'self' https://cdn.jsdelivr.net;
  style-src 'self' https://fonts.googleapis.com;
  img-src 'self' https: data:;
  connect-src 'self' https://api.myapp.com;
  font-src 'self' https://fonts.gstatic.com;
  frame-ancestors 'none';
  base-uri 'self';
  form-action 'self'
```

### Report-Only Mode (ทดสอบก่อน)

```
// ไม่ block แค่รายงาน — ใช้ทดสอบก่อน enforce
Content-Security-Policy-Report-Only:
  default-src 'self';
  report-uri /api/csp-report;
  report-to csp-endpoint;

// Browser ส่ง report เมื่อมีการละเมิด
POST /api/csp-report
{
  "csp-report": {
    "document-uri": "https://myapp.com/page",
    "violated-directive": "script-src 'self'",
    "blocked-uri": "https://evil.com/script.js"
  }
}
```

---

## 7. Server Security

### Security Headers ที่ต้องตั้ง

```
// 1. Strict-Transport-Security (HSTS)
// บังคับ HTTPS ตลอด
Strict-Transport-Security: max-age=31536000; includeSubDomains; preload

// 2. X-Content-Type-Options
// ป้องกัน MIME sniffing
X-Content-Type-Options: nosniff

// 3. X-Frame-Options
// ป้องกัน clickjacking (ซ้อน iframe)
X-Frame-Options: DENY

// 4. X-XSS-Protection (legacy แต่ยังมีประโยชน์)
X-XSS-Protection: 1; mode=block

// 5. Referrer-Policy
// ควบคุมข้อมูล referrer ที่ส่งไปกับ request
Referrer-Policy: strict-origin-when-cross-origin

// 6. Permissions-Policy (เดิมคือ Feature-Policy)
// จำกัดการใช้ browser features
Permissions-Policy: camera=(), microphone=(), geolocation=()

// 7. Content-Security-Policy (ดูหัวข้อ CSP)
```

### Input Validation

```
// ✅ Validate ทุก input ที่มาจาก client

// 1. Type checking
if (typeof userId !== 'number') return error

// 2. Length limits
if (name.length > 100) return error

// 3. Pattern matching
if (!email.match(/^[^\s@]+@[^\s@]+\.[^\s@]+$/)) return error

// 4. Whitelist values
if (!['admin', 'user'].includes(role)) return error

// 5. Sanitize HTML (ป้องกัน XSS)
const clean = sanitizeHtml(userInput)

// 6. Parameterized queries (ป้องกัน SQL Injection)
// ❌ อย่าทำ:
db.query(`SELECT * FROM users WHERE id = ${userId}`)

// ✅ ทำแบบนี้:
db.query('SELECT * FROM users WHERE id = $1', [userId])
```

### Rate Limiting & DDoS Protection

```
// Rate Limiting ตามระดับ:

1. Global Rate Limit
   → ทั้ง API: 10,000 requests/นาที

2. Per-IP Rate Limit
   → ต่อ IP: 100 requests/นาที

3. Per-User Rate Limit
   → ต่อ user: 1,000 requests/นาที

4. Per-Endpoint Rate Limit
   → POST /login: 5 requests/5 นาที (ป้องกัน brute force)
   → POST /register: 3 requests/ชั่วโมง

// DDoS Protection
- ใช้ CDN (Cloudflare, AWS CloudFront)
- WAF (Web Application Firewall)
- Auto-scaling
- Geo-blocking (ถ้าจำเป็น)
```

### Logging & Monitoring

```
// Log ที่ต้องเก็บ:
- ทุก authentication attempt (สำเร็จ + ล้มเหลว)
- Access ข้อมูลสำคัญ
- การเปลี่ยนแปลง permission
- Error 4xx, 5xx
- Rate limit exceeded

// ❌ อย่า log:
- Password
- Token / API Key
- Credit card number
- SSN / เลขบัตรประชาชน

// Format ที่ดี (structured logging)
{
  "timestamp": "2024-01-15T10:30:00Z",
  "level": "warn",
  "event": "login_failed",
  "ip": "1.2.3.4",
  "user_agent": "Mozilla/5.0...",
  "username": "john@example.com",
  "reason": "invalid_password",
  "attempt_count": 3
}
```

### Environment & Dependencies

```
// 1. Environment Variables
❌ อย่าเก็บ secret ใน code:
const dbPassword = "secretPassword123"

✅ ใช้ environment variables:
const dbPassword = process.env.DB_PASSWORD

// 2. .env file (development only)
DB_PASSWORD=secretPassword123
JWT_SECRET=my-super-secret-key
// ❌ อย่า commit .env เข้า git!

// 3. Dependencies
- อัปเดต dependencies สม่ำเสมอ
- ใช้ npm audit / yarn audit ตรวจช่องโหว่
- Lock version ด้วย package-lock.json
- ตรวจสอบ license

// 4. Docker Security
- ใช้ non-root user
- ใช้ official base image
- Scan image for vulnerabilities
- อย่า hardcode secrets ใน Dockerfile
```

---

## 8. API Security Best Practices (สรุปรวม)

### Checklist ก่อน Deploy

```
Authentication & Authorization:
☐ ใช้ HTTPS เสมอ (ห้าม HTTP)
☐ ใช้ authentication ที่เหมาะสม (JWT, OAuth)
☐ ตรวจสอบ authorization ทุก endpoint
☐ ตรวจสอบ ownership (BOLA prevention)
☐ Token มี expiration
☐ ใช้ refresh token pattern
☐ Password hash ด้วย bcrypt (cost ≥ 12)

Input & Output:
☐ Validate ทุก input
☐ Sanitize HTML input
☐ ใช้ parameterized queries (ป้องกัน SQL injection)
☐ ไม่ส่ง sensitive data ใน error response
☐ ไม่ expose stack trace
☐ Whitelist allowed fields (ป้องกัน mass assignment)
☐ จำกัดขนาด request body

Rate Limiting & Protection:
☐ Rate limit ทุก endpoint
☐ Rate limit login attempts พิเศษ
☐ จำกัดขนาด pagination (max limit)
☐ ป้องกัน SSRF

Headers & Transport:
☐ HTTPS only
☐ HSTS header
☐ CORS ตั้งค่าเฉพาะ origin ที่อนุญาต
☐ CSP header
☐ Security headers ครบ (X-Content-Type-Options, etc.)

Monitoring & Maintenance:
☐ Log authentication events
☐ Monitor error rates
☐ อัปเดต dependencies สม่ำเสมอ
☐ ไม่เก็บ secrets ใน code
☐ API versioning + deprecation plan
☐ Penetration testing เป็นระยะ
```

### ตัวอย่าง Secure Express.js API

```javascript
const express = require('express')
const helmet = require('helmet')          // Security headers
const cors = require('cors')
const rateLimit = require('express-rate-limit')
const hpp = require('hpp')                // ป้องกัน HTTP Parameter Pollution

const app = express()

// 1. Security Headers
app.use(helmet())

// 2. CORS
app.use(cors({
  origin: 'https://myapp.com',           // เฉพาะ origin ที่อนุญาต
  methods: ['GET', 'POST', 'PUT', 'DELETE'],
  credentials: true
}))

// 3. Rate Limiting
app.use(rateLimit({
  windowMs: 15 * 60 * 1000,              // 15 นาที
  max: 100                                // 100 requests ต่อ window
}))

// Login rate limit (เข้มงวดกว่า)
app.use('/api/auth/login', rateLimit({
  windowMs: 15 * 60 * 1000,
  max: 5
}))

// 4. Body parsing limits
app.use(express.json({ limit: '10kb' })) // จำกัด body size

// 5. HPP (ป้องกัน parameter pollution)
app.use(hpp())

// 6. ซ่อนข้อมูล server
app.disable('x-powered-by')
```
