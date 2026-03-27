# API Design - สรุปเนื้อหาทั้งหมด

> สรุปจาก roadmap.sh/api-design ครบทุกหัวข้อ

---

## 1. API คืออะไร (What are APIs)

**API (Application Programming Interface)** คือตัวกลางที่ช่วยให้แอปพลิเคชันสื่อสารกันได้ โดยซ่อนความซับซ้อนภายในไว้ และเปิดเผยเฉพาะส่วนที่จำเป็นให้นักพัฒนาใช้งาน

### แนวคิดหลัก
- API กำหนด **วิธีการ (methods)** และ **รูปแบบข้อมูล (data formats)** ที่แอปพลิเคชันใช้ในการส่ง รับ หรือแก้ไขข้อมูล
- ทำให้แอปพลิเคชันต่างๆ สามารถแลกเปลี่ยนข้อมูลและฟังก์ชันการทำงานได้อย่างง่ายดาย
- เป็นพื้นฐานสำคัญของการพัฒนาซอฟต์แวร์สมัยใหม่

### ตัวอย่างในชีวิตจริง
```
แอปสั่งอาหาร  →  API ร้านอาหาร  →  ระบบร้านอาหาร
(ลูกค้าสั่ง)     (ตัวกลาง)         (ประมวลผลออเดอร์)
```

---

## 2. HTTP (Hypertext Transfer Protocol)

HTTP เป็นโปรโตคอลพื้นฐานสำหรับการสื่อสารบนเว็บ และเป็นหัวใจของ API Design

### 2.1 HTTP Versions (เวอร์ชันของ HTTP)

| เวอร์ชัน | ปีที่ออก | คุณสมบัติหลัก |
|----------|---------|---------------|
| **HTTP/1.0** | 1996 | 1 request ต่อ 1 connection |
| **HTTP/1.1** | 1997 | Keep-alive connection, Pipeline, ใช้แพร่หลายที่สุด |
| **HTTP/2** | 2015 | Multiplexing (หลาย request พร้อมกัน), Header compression, Server push |
| **HTTP/3** | 2022 | ใช้ QUIC แทน TCP, เร็วขึ้น, ลด latency |

```
HTTP/1.1: ส่งทีละ request → รอ response → ส่ง request ต่อไป
           [req1]→[res1]→[req2]→[res2]→[req3]→[res3]

HTTP/2:   ส่งหลาย request พร้อมกันใน connection เดียว
           [req1]→
           [req2]→  ← Multiplexing
           [req3]→

HTTP/3:   ใช้ QUIC (UDP-based) แทน TCP
           เร็วกว่า HTTP/2 โดยเฉพาะเครือข่ายไม่เสถียร
```

### 2.2 HTTP Methods (วิธีการร้องขอ)

| Method | ความหมาย | ใช้เมื่อ | ตัวอย่าง |
|--------|----------|---------|----------|
| `GET` | ดึงข้อมูล | อ่านข้อมูล | ดึงรายการสินค้า |
| `POST` | สร้างข้อมูลใหม่ | เพิ่มข้อมูล | สร้างผู้ใช้ใหม่ |
| `PUT` | อัปเดตข้อมูลทั้งหมด | แก้ไขข้อมูลแบบเต็ม | อัปเดตโปรไฟล์ทั้งหมด |
| `PATCH` | อัปเดตข้อมูลบางส่วน | แก้ไขเฉพาะบางฟิลด์ | เปลี่ยนชื่อผู้ใช้ |
| `DELETE` | ลบข้อมูล | ลบข้อมูล | ลบบัญชีผู้ใช้ |
| `HEAD` | เหมือน GET แต่ไม่มี body | ตรวจสอบว่า resource มีอยู่ไหม | เช็คว่าไฟล์มีอยู่ |
| `OPTIONS` | ดู methods ที่รองรับ | Preflight request ใน CORS | ตรวจสอบสิทธิ์ก่อนส่ง |

### 2.3 HTTP Status Codes (รหัสสถานะ)

**2xx - สำเร็จ (Success)**
| รหัส | ความหมาย |
|------|----------|
| `200 OK` | คำขอสำเร็จ |
| `201 Created` | สร้างข้อมูลใหม่สำเร็จ |
| `204 No Content` | สำเร็จแต่ไม่มีข้อมูลตอบกลับ (เช่น ลบสำเร็จ) |

**3xx - เปลี่ยนเส้นทาง (Redirection)**
| รหัส | ความหมาย |
|------|----------|
| `301 Moved Permanently` | ย้าย URL ถาวร |
| `302 Found` | ย้าย URL ชั่วคราว |
| `304 Not Modified` | ข้อมูลไม่เปลี่ยนแปลง (ใช้ cache ได้) |

**4xx - ข้อผิดพลาดจากฝั่ง Client**
| รหัส | ความหมาย |
|------|----------|
| `400 Bad Request` | คำขอไม่ถูกต้อง |
| `401 Unauthorized` | ไม่ได้ยืนยันตัวตน |
| `403 Forbidden` | ไม่มีสิทธิ์เข้าถึง |
| `404 Not Found` | ไม่พบข้อมูล |
| `405 Method Not Allowed` | ใช้ HTTP Method ผิด |
| `409 Conflict` | ข้อมูลขัดแย้งกัน |
| `422 Unprocessable Entity` | รูปแบบถูกต้องแต่ข้อมูลไม่ผ่าน validation |
| `429 Too Many Requests` | คำขอมากเกินไป (Rate Limit) |

**5xx - ข้อผิดพลาดจากฝั่ง Server**
| รหัส | ความหมาย |
|------|----------|
| `500 Internal Server Error` | เซิร์ฟเวอร์เกิดข้อผิดพลาด |
| `502 Bad Gateway` | เกตเวย์ได้รับการตอบกลับที่ไม่ถูกต้อง |
| `503 Service Unavailable` | เซิร์ฟเวอร์ไม่พร้อมให้บริการ |
| `504 Gateway Timeout` | เกตเวย์รอ response นานเกินไป |

### 2.4 HTTP Headers (ส่วนหัวของ HTTP)

Headers คือข้อมูลเพิ่มเติมที่แนบไปกับ Request/Response

```
// Request Headers ที่ใช้บ่อย
Content-Type: application/json      → บอกประเภทข้อมูลที่ส่ง
Authorization: Bearer <token>       → ส่ง token ยืนยันตัวตน
Accept: application/json            → บอกประเภทข้อมูลที่ต้องการรับ
Accept-Language: th, en             → ภาษาที่ต้องการ
User-Agent: Mozilla/5.0...          → ข้อมูล client
If-None-Match: "etag-value"         → ใช้กับ caching

// Response Headers ที่ใช้บ่อย
Content-Type: application/json      → ประเภทข้อมูลที่ตอบกลับ
Cache-Control: max-age=3600         → กำหนดการ cache
ETag: "abc123"                      → ค่า hash ของข้อมูลสำหรับ cache
X-RateLimit-Remaining: 99          → จำนวน request ที่เหลือ
Set-Cookie: session=abc             → ตั้ง cookie
```

### 2.5 HTTP Caching (การแคช)

การเก็บสำเนาข้อมูลไว้ใกล้ Client เพื่อลด request ซ้ำ

```
// วิธีที่ 1: Cache-Control Header
Cache-Control: max-age=3600        → cache ได้ 1 ชั่วโมง
Cache-Control: no-cache             → ต้องตรวจสอบกับ server ก่อนใช้ cache
Cache-Control: no-store             → ห้าม cache เลย (ข้อมูลลับ)

// วิธีที่ 2: ETag (Entity Tag)
// Request แรก
GET /api/users/1
// Response: ETag: "v1-abc123"

// Request ถัดไป — ส่ง ETag กลับไปถาม
GET /api/users/1
If-None-Match: "v1-abc123"
// Response: 304 Not Modified (ข้อมูลไม่เปลี่ยน ใช้ cache เดิม)
// หรือ: 200 OK + ข้อมูลใหม่ (ถ้าเปลี่ยนแล้ว)

// วิธีที่ 3: Last-Modified
Last-Modified: Wed, 21 Oct 2024 07:28:00 GMT
If-Modified-Since: Wed, 21 Oct 2024 07:28:00 GMT
```

### 2.6 Cookies

ข้อมูลเล็กๆ ที่ Server ส่งให้ Browser เก็บไว้ และ Browser จะส่งกลับไปในทุก request

```
// Server ตั้ง Cookie
Set-Cookie: session_id=abc123; HttpOnly; Secure; SameSite=Strict; Max-Age=86400

// Browser ส่ง Cookie กลับอัตโนมัติ
Cookie: session_id=abc123
```

**Flags สำคัญ:**
- `HttpOnly` — JavaScript เข้าถึงไม่ได้ (ป้องกัน XSS)
- `Secure` — ส่งผ่าน HTTPS เท่านั้น
- `SameSite=Strict` — ป้องกัน CSRF attack
- `Max-Age=86400` — หมดอายุใน 24 ชั่วโมง
- `Domain=.example.com` — กำหนดขอบเขต domain
- `Path=/api` — ส่งเฉพาะ path ที่กำหนด

### 2.7 CORS (Cross-Origin Resource Sharing)

กลไกที่ช่วยให้เว็บหนึ่งสามารถเรียก API จากเว็บอื่นได้

```
// ปัญหา: เว็บ frontend (localhost:3000) เรียก API (localhost:8000)
// Browser จะ block ถ้าไม่ตั้งค่า CORS

// Preflight Request (Browser ส่งอัตโนมัติ)
OPTIONS /api/users
Origin: http://localhost:3000

// Server ตอบกลับด้วย CORS Headers:
Access-Control-Allow-Origin: http://localhost:3000
Access-Control-Allow-Methods: GET, POST, PUT, DELETE
Access-Control-Allow-Headers: Content-Type, Authorization
Access-Control-Allow-Credentials: true
Access-Control-Max-Age: 86400    → cache preflight 24 ชม.
```

**ประเภทของ CORS Request:**
- **Simple Request** — GET/POST ธรรมดา ไม่มี preflight
- **Preflight Request** — Browser ส่ง OPTIONS ก่อนเพื่อถาม server ว่ายอมรับไหม
- **Credentialed Request** — ส่ง cookies/auth ข้าม origin

---

## 3. พื้นฐานที่ต้องรู้ (Learn the Basics)

### 3.1 URL, Query & Path Parameters

```
https://api.example.com/users/123?sort=name&limit=10
│         │              │    │    │
│         │              │    │    └── Query Parameters (ตัวกรอง)
│         │              │    └── Path Parameter (ระบุตัว resource)
│         │              └── Resource (ทรัพยากร)
│         └── Base URL
└── Protocol
```

- **Path Parameters** (`/users/123`) — ระบุ resource เฉพาะเจาะจง (บังคับ)
- **Query Parameters** (`?sort=name&limit=10`) — กรอง เรียงลำดับ แบ่งหน้า (ไม่บังคับ)

```
// Path Parameters — ระบุ resource
GET /users/123           → ผู้ใช้ ID 123
GET /users/123/orders/5  → ออเดอร์ ID 5 ของผู้ใช้ 123

// Query Parameters — กรอง/เรียง/แบ่งหน้า
GET /users?role=admin&sort=name&page=2&limit=20
GET /products?category=electronics&min_price=100&max_price=500
```

### 3.2 Content Negotiation

กระบวนการที่ Client และ Server ตกลงกันว่าจะใช้รูปแบบข้อมูลอะไร

```
// Client บอกว่าต้องการ JSON
Accept: application/json

// Client บอกว่าต้องการ XML
Accept: application/xml

// Client บอกว่ารับได้หลายแบบ (JSON สำคัญกว่า)
Accept: application/json;q=0.9, application/xml;q=0.5

// Server ตอบกลับด้วยรูปแบบที่ตกลงกัน
Content-Type: application/json

// ถ้า Server ไม่รองรับรูปแบบที่ขอ
HTTP/1.1 406 Not Acceptable
```

### 3.3 Understand TCP/IP

TCP/IP เป็นชุดโปรโตคอลพื้นฐานที่ทำให้อุปกรณ์สื่อสารกันผ่านเครือข่ายได้

```
4 ชั้นของ TCP/IP:

┌─────────────────────────┐
│  Application Layer       │  ← HTTP, HTTPS, FTP, DNS
│  (ชั้นแอปพลิเคชัน)       │    ข้อมูลที่ผู้ใช้เห็น
├─────────────────────────┤
│  Transport Layer         │  ← TCP (เชื่อถือได้), UDP (เร็ว)
│  (ชั้นขนส่ง)             │    แบ่งข้อมูลเป็น packets
├─────────────────────────┤
│  Internet Layer          │  ← IP (กำหนดที่อยู่), ICMP
│  (ชั้นอินเทอร์เน็ต)      │    หาเส้นทางส่งข้อมูล
├─────────────────────────┤
│  Network Access Layer    │  ← Ethernet, Wi-Fi
│  (ชั้นเครือข่าย)         │    ส่งข้อมูลจริงทางกายภาพ
└─────────────────────────┘
```

**TCP vs UDP:**
| | TCP | UDP |
|---|---|---|
| **ความน่าเชื่อถือ** | รับรองว่าข้อมูลถึง | ไม่รับรอง |
| **ความเร็ว** | ช้ากว่า (มี handshake) | เร็วกว่า |
| **ใช้กับ** | HTTP, API, เว็บ | Video streaming, Game |

**TCP 3-Way Handshake:**
```
Client → SYN → Server          (ขอเชื่อมต่อ)
Client ← SYN-ACK ← Server     (ยอมรับ)
Client → ACK → Server          (ยืนยัน → เริ่มส่งข้อมูล)
```

### 3.4 Basics of DNS (Domain Name System)

DNS แปลงชื่อ domain (เช่น google.com) เป็น IP address (เช่น 142.250.190.78)

```
ขั้นตอนการทำงาน:

1. พิมพ์ api.example.com ใน browser
2. Browser ถาม DNS Resolver: "api.example.com อยู่ที่ IP อะไร?"
3. DNS Resolver ถาม Root Server → .com Server → example.com Server
4. ได้คำตอบ: 93.184.216.34
5. Browser เชื่อมต่อไปที่ IP นั้น

ประเภท DNS Record ที่สำคัญ:
- A Record     → domain → IPv4 (เช่น 93.184.216.34)
- AAAA Record  → domain → IPv6
- CNAME Record → domain → domain อื่น (alias)
- MX Record    → domain → mail server
- TXT Record   → ข้อมูลข้อความ (ใช้ verify domain)
```

---

## 4. Different API Styles (รูปแบบ API ต่างๆ)

### 4.1 RESTful APIs

REST (Representational State Transfer) เป็นรูปแบบสถาปัตยกรรม API ที่ใช้มากที่สุด ใช้ HTTP Methods จัดการ resource

```
GET    /api/users          → ดึงรายการผู้ใช้
POST   /api/users          → สร้างผู้ใช้ใหม่
GET    /api/users/123      → ดึงผู้ใช้ ID 123
PUT    /api/users/123      → อัปเดตผู้ใช้ ID 123
DELETE /api/users/123      → ลบผู้ใช้ ID 123
```

**ข้อดี:** เข้าใจง่าย, ใช้ HTTP มาตรฐาน, cache ได้ง่าย, รองรับทุกภาษา
**ข้อเสีย:** Over-fetching/Under-fetching, หลาย endpoint

### 4.2 Simple JSON APIs

API ง่ายๆ ที่รับ-ส่งข้อมูลเป็น JSON โดยไม่ต้องตามหลัก REST อย่างเคร่งครัด

```json
// ไม่จำเป็นต้องตามหลัก REST ทุกข้อ
POST /api/getUser
{ "userId": 123 }

// Response
{ "name": "John", "email": "john@example.com" }
```

**ข้อดี:** ง่าย เร็ว เหมาะกับโปรเจกต์เล็ก
**ข้อเสีย:** ไม่มีมาตรฐาน, ยากต่อการ scale

### 4.3 SOAP APIs (Simple Object Access Protocol)

โปรโตคอลที่ใช้ XML ในการรับ-ส่งข้อมูล เข้มงวดเรื่องรูปแบบ

```xml
<!-- SOAP Request -->
<?xml version="1.0"?>
<soap:Envelope xmlns:soap="http://www.w3.org/2003/05/soap-envelope">
  <soap:Header>
    <auth:Token>abc123</auth:Token>
  </soap:Header>
  <soap:Body>
    <GetUser>
      <UserId>123</UserId>
    </GetUser>
  </soap:Body>
</soap:Envelope>

<!-- SOAP Response -->
<soap:Envelope>
  <soap:Body>
    <GetUserResponse>
      <Name>John</Name>
      <Email>john@example.com</Email>
    </GetUserResponse>
  </soap:Body>
</soap:Envelope>
```

**ข้อดี:** มีมาตรฐานเข้มงวด (WSDL), รองรับ WS-Security, ใช้ในระบบ enterprise
**ข้อเสีย:** ซับซ้อน, verbose (ข้อมูลเยอะ), ช้ากว่า REST

### 4.4 GraphQL APIs

ภาษา query สำหรับ API ที่ให้ Client เลือกได้ว่าต้องการข้อมูลอะไรบ้าง

```graphql
# Client ระบุเฉพาะฟิลด์ที่ต้องการ (ไม่ Over-fetch)
query {
  user(id: 123) {
    name
    email
    orders {
      id
      total
    }
  }
}

# Response — ได้เฉพาะที่ขอ
{
  "data": {
    "user": {
      "name": "John",
      "email": "john@example.com",
      "orders": [
        { "id": 1, "total": 500 }
      ]
    }
  }
}

# Mutation (แก้ไขข้อมูล)
mutation {
  createUser(name: "John", email: "john@example.com") {
    id
    name
  }
}
```

**ข้อดี:** ไม่มี over/under-fetching, endpoint เดียว, type system, self-documenting
**ข้อเสีย:** ซับซ้อนกว่า REST, caching ยากกว่า, อาจมีปัญหา N+1 query

### 4.5 gRPC APIs

Framework จาก Google ที่ใช้ Protocol Buffers (protobuf) ในการรับ-ส่งข้อมูล เร็วกว่า JSON

```protobuf
// กำหนด service ด้วย .proto file
syntax = "proto3";

service UserService {
  rpc GetUser (GetUserRequest) returns (User);
  rpc ListUsers (ListUsersRequest) returns (stream User);  // streaming
}

message GetUserRequest {
  int32 id = 1;
}

message User {
  int32 id = 1;
  string name = 2;
  string email = 3;
}
```

**ข้อดี:** เร็วมาก (binary format), รองรับ streaming, type-safe, code generation
**ข้อเสีย:** อ่านไม่ได้ด้วยตา (binary), ไม่รองรับ browser โดยตรง, ต้องใช้ HTTP/2

**เมื่อไหร่ใช้อะไร:**
| รูปแบบ | เหมาะกับ |
|--------|---------|
| REST | Web API ทั่วไป, CRUD operations |
| GraphQL | Frontend ที่ต้องการข้อมูลหลากหลาย, Mobile app |
| gRPC | Microservices คุยกัน, ต้องการความเร็วสูง |
| SOAP | ระบบ Enterprise, ธนาคาร, ต้องการมาตรฐานเข้มงวด |

---

## 5. Building JSON / RESTful APIs

### 5.1 REST Principles (หลักการ REST)

1. **Client-Server** — แยก Client กับ Server ออกจากกัน
2. **Stateless** — แต่ละ request ต้องมีข้อมูลครบในตัวเอง server ไม่เก็บ state ของ client
3. **Cacheable** — Response ควรบอกได้ว่า cache ได้หรือไม่
4. **Uniform Interface** — ใช้ interface ที่เป็นมาตรฐานเดียวกัน
5. **Layered System** — สามารถมี layer ตัวกลาง (proxy, gateway) ได้
6. **Code on Demand** (ไม่บังคับ) — Server ส่ง code ให้ Client รันได้

### 5.2 URI Design (การออกแบบ URI)

```
✅ ถูกต้อง:
GET    /users              → ดึงรายการผู้ใช้ทั้งหมด
GET    /users/123          → ดึงผู้ใช้ ID 123
POST   /users              → สร้างผู้ใช้ใหม่
PUT    /users/123          → อัปเดตผู้ใช้ ID 123
DELETE /users/123          → ลบผู้ใช้ ID 123
GET    /users/123/orders   → ดึงออเดอร์ของผู้ใช้ ID 123

❌ ผิดหลัก:
GET    /getUsers           → อย่าใส่ verb ใน URI
GET    /user               → ใช้พหูพจน์ (users)
POST   /users/create       → อย่าใส่ action ใน URI
GET    /Users              → ใช้ตัวพิมพ์เล็ก
```

**กฎสำคัญ:**
- ใช้คำนาม ไม่ใช้คำกริยา (`/users` ไม่ใช่ `/getUsers`)
- ใช้พหูพจน์ (`/users` ไม่ใช่ `/user`)
- ใช้ตัวพิมพ์เล็กและ kebab-case (`/order-items`)
- ใช้ HTTP Method บอก action แทน
- ซ้อน resource ได้ไม่เกิน 2-3 ชั้น (`/users/123/orders`)

### 5.3 Versioning Strategies (กลยุทธ์การกำหนดเวอร์ชัน)

```
// 1. URI Path Versioning (นิยมที่สุด)
GET /api/v1/users
GET /api/v2/users

// 2. Header Versioning
GET /api/users
Accept: application/vnd.myapi.v1+json

// 3. Query Parameter Versioning
GET /api/users?version=1

// 4. Content Negotiation
Accept: application/vnd.myapi+json;version=2
```

| วิธี | ข้อดี | ข้อเสีย |
|------|------|---------|
| URI Path | เข้าใจง่าย เห็นชัด | URI เปลี่ยน |
| Header | URI สะอาด | ซ่อนอยู่ ทดสอบยาก |
| Query | ง่าย | ถูกมองว่า URI ไม่สะอาด |

### 5.4 Handling CRUD Operations

| การกระทำ | HTTP Method | URI | ตัวอย่าง Body |
|----------|-------------|-----|---------------|
| **C**reate | `POST /users` | `{"name": "John"}` | สร้างผู้ใช้ |
| **R**ead (ทั้งหมด) | `GET /users` | ไม่มี | ดึงรายการ |
| **R**ead (เดี่ยว) | `GET /users/1` | ไม่มี | ดึงข้อมูลผู้ใช้ |
| **U**pdate (ทั้งหมด) | `PUT /users/1` | `{"name": "Jane", "email": "..."}` | แทนที่ทั้ง record |
| **U**pdate (บางส่วน) | `PATCH /users/1` | `{"name": "Jane"}` | แก้เฉพาะชื่อ |
| **D**elete | `DELETE /users/1` | ไม่มี | ลบผู้ใช้ |

### 5.5 Pagination (การแบ่งหน้า)

```json
// Offset-based Pagination (ง่าย)
GET /api/users?page=2&limit=20

{
  "data": [...],
  "pagination": {
    "page": 2,
    "limit": 20,
    "total": 150,
    "totalPages": 8
  }
}

// Cursor-based Pagination (ดีกว่าสำหรับข้อมูลเยอะ)
GET /api/users?cursor=eyJpZCI6MjB9&limit=20

{
  "data": [...],
  "pagination": {
    "next_cursor": "eyJpZCI6NDB9",
    "has_more": true
  }
}
```

| วิธี | ข้อดี | ข้อเสีย |
|------|------|---------|
| Offset-based | ง่าย กระโดดไปหน้าไหนก็ได้ | ช้าเมื่อ offset ใหญ่ |
| Cursor-based | เร็วสม่ำเสมอ | กระโดดข้ามหน้าไม่ได้ |

### 5.6 Rate Limiting (การจำกัดจำนวนคำขอ)

```
// Response Headers
X-RateLimit-Limit: 100         → จำนวน request สูงสุดต่อช่วงเวลา
X-RateLimit-Remaining: 45      → จำนวนที่เหลือ
X-RateLimit-Reset: 1620000000  → เวลาที่จะ reset

// เมื่อเกินลิมิต
HTTP/1.1 429 Too Many Requests
Retry-After: 60                → ลองใหม่หลังจาก 60 วินาที
```

**อัลกอริทึมที่ใช้บ่อย:**
- **Fixed Window** — นับ request ต่อช่วงเวลาตายตัว (เช่น 100/นาที)
- **Sliding Window** — นับ request ย้อนหลัง (แม่นยำกว่า)
- **Token Bucket** — เติม token ตามเวลา ใช้ 1 token ต่อ request
- **Leaky Bucket** — request เข้าคิว ปล่อยออกคงที่

### 5.7 Idempotency (ความเป็นไอเด็มโพเทนซ์)

การส่ง request เดิมซ้ำหลายครั้ง ผลลัพธ์ต้องเหมือนเดิม

```
✅ Idempotent Methods:
GET    /users/123          → ดึงข้อมูลเดิมเสมอ
PUT    /users/123          → อัปเดตเป็นค่าเดิมเสมอ
DELETE /users/123          → ลบแล้วก็ลบแล้ว (ครั้งที่ 2 ได้ 404)

❌ ไม่ Idempotent:
POST   /users              → สร้างผู้ใช้ใหม่ทุกครั้ง!

// ป้องกัน POST ซ้ำด้วย Idempotency Key
POST /api/payments
Idempotency-Key: abc-123-xyz
{ "amount": 1000 }
// ส่งซ้ำด้วย key เดิม → server return ผลเดิม ไม่ทำรายการซ้ำ
```

### 5.8 HATEOAS (Hypermedia as the Engine of Application State)

API ตอบกลับพร้อม link บอก action ที่ทำได้ต่อไป ให้ Client ไม่ต้อง hardcode URL

```json
// GET /api/users/123
{
  "id": 123,
  "name": "John",
  "email": "john@example.com",
  "_links": {
    "self": { "href": "/api/users/123" },
    "orders": { "href": "/api/users/123/orders" },
    "update": { "href": "/api/users/123", "method": "PUT" },
    "delete": { "href": "/api/users/123", "method": "DELETE" }
  }
}
```

**ข้อดี:** Client ค้นพบ API ได้เอง, ลดการ hardcode URL
**ข้อเสีย:** Response ใหญ่ขึ้น, ซับซ้อนขึ้น, ในทางปฏิบัติใช้น้อย

### 5.9 Error Handling (การจัดการข้อผิดพลาด)

```json
// ✅ Error Response ที่ดี
{
  "error": {
    "code": "VALIDATION_ERROR",
    "message": "ข้อมูลไม่ถูกต้อง",
    "details": [
      {
        "field": "email",
        "message": "รูปแบบอีเมลไม่ถูกต้อง"
      },
      {
        "field": "age",
        "message": "ต้องมากกว่า 0"
      }
    ]
  }
}
```

**หลักปฏิบัติ:**
- ใช้ HTTP Status Code ที่เหมาะสม
- มี error code ที่เฉพาะเจาะจง
- ให้รายละเอียดที่เพียงพอแก่นักพัฒนา
- อย่าเปิดเผยข้อมูลระบบภายใน (เช่น stack trace)
- ใช้รูปแบบเดียวกันทั้ง API

### 5.10 RFC 7807 - Problem Details for APIs

มาตรฐานรูปแบบ error response ที่เป็นสากล

```json
// รูปแบบตาม RFC 7807
{
  "type": "https://api.example.com/errors/validation",
  "title": "Validation Error",
  "status": 422,
  "detail": "ฟิลด์อีเมลไม่ถูกต้อง",
  "instance": "/api/users/123",
  "errors": [
    { "field": "email", "message": "รูปแบบไม่ถูกต้อง" }
  ]
}
```

| ฟิลด์ | ความหมาย |
|-------|----------|
| `type` | URI อ้างอิงประเภทข้อผิดพลาด |
| `title` | ชื่อข้อผิดพลาดที่อ่านเข้าใจง่าย |
| `status` | HTTP status code |
| `detail` | คำอธิบายเฉพาะเจาะจง |
| `instance` | URI ของ resource ที่เกิดปัญหา |

---

## 6. Authentication & Authorization (การยืนยันตัวตนและการอนุญาต)

### 6.1 Authentication vs Authorization

| | Authentication (AuthN) | Authorization (AuthZ) |
|---|---|---|
| **คำถาม** | คุณเป็นใคร? | คุณมีสิทธิ์ทำอะไรบ้าง? |
| **ทำเมื่อ** | ก่อน | หลัง Authentication |
| **ตัวอย่าง** | Login ด้วย username/password | ผู้ใช้ทั่วไป vs Admin |

### 6.2 Authentication Methods (วิธียืนยันตัวตน)

#### Basic Auth
วิธีง่ายที่สุด — ส่ง username:password แบบ Base64 encode

```
Authorization: Basic YWRtaW46MTIzNA==
// ถอดรหัส Base64: admin:1234
```
> ⚠️ ไม่ปลอดภัยถ้าไม่ใช้ HTTPS เพราะ Base64 ถอดรหัสได้ง่าย

#### Token Based Auth
ใช้ token แทน username/password ในทุก request

```
// 1. Login → ได้ token
POST /api/login
{ "username": "admin", "password": "1234" }
// Response: { "token": "abc123xyz" }

// 2. ใช้ token ในทุก request ถัดไป
GET /api/users
Authorization: Bearer abc123xyz
```

#### JWT (JSON Web Token)
Token ที่มีข้อมูลในตัวเอง server ไม่ต้องเก็บ session

```
// JWT มี 3 ส่วน คั่นด้วยจุด
xxxxx.yyyyy.zzzzz
│       │       │
│       │       └── Signature (ลายเซ็นรับรอง)
│       └── Payload (ข้อมูลผู้ใช้)
└── Header (ประเภท token และ algorithm)

// ตัวอย่าง Payload
{
  "sub": "123",        // user id
  "name": "John",
  "role": "admin",
  "exp": 1700000000    // หมดอายุ
}

// ส่ง JWT ผ่าน Header
Authorization: Bearer eyJhbGciOiJIUzI1NiIs...
```

**ข้อดี:** Stateless, server ไม่ต้องเก็บ session, ใช้ข้าม service ได้
**ข้อเสีย:** เพิกถอน (revoke) ยาก, payload ขนาดใหญ่

#### OAuth 2.0
ให้สิทธิ์แอปอื่นเข้าถึงข้อมูลโดยไม่เปิดเผยรหัสผ่าน

```
ตัวอย่าง: "Login with Google"

1. ผู้ใช้กด "Login with Google" ในแอปของเรา
2. แอปของเรา → redirect ไป Google
3. ผู้ใช้อนุญาตให้ Google แชร์ข้อมูล
4. Google → ส่ง Authorization Code กลับมาที่แอป
5. แอปใช้ Code แลก Access Token กับ Google
6. แอปใช้ Access Token ดึงข้อมูลผู้ใช้จาก Google
```

**Grant Types:**
| Grant Type | ใช้เมื่อ |
|-----------|---------|
| Authorization Code | Web app มี server (ปลอดภัยที่สุด) |
| PKCE | Mobile/SPA app (ไม่มี server เก็บ secret) |
| Client Credentials | Server-to-server (ไม่มีผู้ใช้) |
| Implicit | ❌ เลิกใช้แล้ว (ไม่ปลอดภัย) |

#### Session Based Auth
Server เก็บ session ไว้ฝั่ง server และส่ง session ID ผ่าน cookie

```
// 1. Login
POST /api/login → Server สร้าง session เก็บไว้ในหน่วยความจำ/database
Set-Cookie: session_id=abc123

// 2. ทุก request ถัดไป Browser ส่ง cookie อัตโนมัติ
Cookie: session_id=abc123
→ Server ดึง session จาก memory/DB → ยืนยันตัวตน
```

**เปรียบเทียบ JWT vs Session:**
| | JWT | Session |
|---|---|---|
| **เก็บที่** | Client (token) | Server (memory/DB) |
| **Stateless** | ใช่ | ไม่ |
| **Scale** | ง่าย | ต้อง share session |
| **เพิกถอน** | ยาก | ง่าย |
| **เหมาะกับ** | API, Microservices | Web app แบบดั้งเดิม |

### 6.3 Authorization Methods (วิธีอนุญาต)

#### RBAC (Role-Based Access Control)
กำหนดสิทธิ์ตามบทบาท (Role)

```
Roles:
├── admin  → CRUD ทุกอย่าง
├── editor → Create, Read, Update
└── viewer → Read เท่านั้น

// ตรวจสอบ
if (user.role === "admin") {
  // อนุญาตทุกอย่าง
}
```

#### ABAC (Attribute-Based Access Control)
กำหนดสิทธิ์ตามคุณสมบัติ (Attributes) ของผู้ใช้ ทรัพยากร และสภาพแวดล้อม

```
Rules:
- ถ้า user.department === "HR" AND resource.type === "salary"
  → อนุญาต
- ถ้า user.location === "TH" AND time.hour >= 9 AND time.hour <= 17
  → อนุญาต
```

#### อื่นๆ
| วิธี | คำอธิบาย |
|------|---------|
| **DAC** (Discretionary) | เจ้าของ resource กำหนดสิทธิ์เอง (เช่น share ไฟล์) |
| **MAC** (Mandatory) | ระบบกำหนดสิทธิ์ตาม security level (เช่น Top Secret) |
| **PBAC** (Policy-Based) | ใช้ policy engine ตัดสินใจ |
| **ReBAC** (Relationship-Based) | สิทธิ์ตามความสัมพันธ์ (เช่น เพื่อนเห็นโพสต์) |

### 6.4 API Keys & Management

API Key คือ string ที่ใช้ระบุตัว client application (ไม่ใช่ผู้ใช้)

```
// ส่ง API Key ผ่าน Header
GET /api/weather?city=Bangkok
X-API-Key: sk_live_abc123xyz

// หรือผ่าน Query Parameter (ไม่แนะนำ เพราะติด log)
GET /api/weather?city=Bangkok&api_key=sk_live_abc123xyz
```

**Best Practices:**
- แยก key สำหรับ production กับ development
- ตั้งค่า rate limit ต่อ key
- สามารถเพิกถอน (revoke) key ได้
- อย่าเก็บ key ใน source code (ใช้ environment variables)
- หมุนเวียน (rotate) key เป็นระยะ

---

## 7. API Documentation Tools (เครื่องมือเอกสาร API)

### 7.1 Swagger / OpenAPI

มาตรฐานสำหรับอธิบาย RESTful API ในรูปแบบ YAML/JSON

```yaml
# openapi.yaml
openapi: 3.0.0
info:
  title: User API
  version: 1.0.0

paths:
  /users:
    get:
      summary: ดึงรายการผู้ใช้
      parameters:
        - name: page
          in: query
          schema:
            type: integer
      responses:
        '200':
          description: สำเร็จ
          content:
            application/json:
              schema:
                type: array
                items:
                  $ref: '#/components/schemas/User'

components:
  schemas:
    User:
      type: object
      properties:
        id:
          type: integer
        name:
          type: string
        email:
          type: string
```

### 7.2 เครื่องมืออื่นๆ

| เครื่องมือ | จุดเด่น |
|-----------|---------|
| **Swagger UI** | สร้างหน้าเอกสารจาก OpenAPI spec, ทดสอบ API ได้ |
| **Postman** | ทดสอบ API, สร้าง collection, mock server |
| **Readme.com** | สร้างเอกสาร API สวยงาม, interactive |
| **Stoplight** | ออกแบบ API-first, validate spec |

---

## 8. API Security (ความปลอดภัย API)

### 8.1 Common Vulnerabilities (ช่องโหว่ที่พบบ่อย)

| ช่องโหว่ | คำอธิบาย | ป้องกัน |
|---------|---------|---------|
| **Injection** | ส่ง SQL/NoSQL ผ่าน input | ใช้ parameterized queries |
| **Broken Auth** | ระบบยืนยันตัวตนมีช่องโหว่ | ใช้ OAuth, limit login attempts |
| **Excessive Data** | API ส่งข้อมูลมากเกินจำเป็น | ส่งเฉพาะข้อมูลที่ต้องการ |
| **Rate Limiting** | ไม่จำกัด request | ใช้ rate limiter |
| **BOLA** | เข้าถึง resource ของคนอื่น | ตรวจสอบ ownership ทุก request |
| **Mass Assignment** | ส่ง field ที่ไม่ควรแก้ไข | whitelist fields ที่อนุญาต |

### 8.2 Security Best Practices

```
1. ใช้ HTTPS เสมอ (ห้าม HTTP)
2. ใช้ Authentication ที่เหมาะสม (JWT, OAuth)
3. Validate input ทุกตัว
4. Rate Limiting
5. อย่า expose ข้อมูลละเอียดใน error messages
6. ใช้ CORS อย่างเข้มงวด
7. เข้ารหัสข้อมูลลับ (passwords, tokens)
8. Log และ monitor ทุก request
9. ใช้ API Keys สำหรับ tracking
10. อัปเดต dependencies สม่ำเสมอ
```

---

## 9. API Performance (ประสิทธิภาพ API)

### 9.1 Performance Metrics (ตัวชี้วัด)

| Metric | คำอธิบาย | ค่าที่ดี |
|--------|---------|---------|
| **Latency** | เวลาตอบกลับ | < 200ms |
| **Throughput** | จำนวน requests/วินาที | ขึ้นกับระบบ |
| **Error Rate** | เปอร์เซ็นต์ error | < 1% |
| **Availability** | เวลาที่ระบบพร้อมใช้งาน | 99.9%+ |
| **P95/P99 Latency** | latency ที่ 95%/99% ของ request | ไม่ควรสูงมาก |

### 9.2 Caching Strategies (กลยุทธ์ Cache)

```
// 1. Client-side Cache (Browser)
Cache-Control: max-age=3600

// 2. CDN Cache (เช่น Cloudflare)
ใส่ resource ที่ไม่เปลี่ยนบ่อยไว้ที่ CDN

// 3. Server-side Cache (เช่น Redis)
// ตรวจ cache ก่อนดึง database
cache_key = "user:123"
result = redis.get(cache_key)
if (!result) {
  result = db.query("SELECT * FROM users WHERE id = 123")
  redis.set(cache_key, result, ttl=3600)
}
return result

// 4. Database Query Cache
// database เก็บ cache ของ query ที่เรียกบ่อย
```

### 9.3 Load Balancing (กระจายภาระ)

```
                    ┌→ Server 1
Client → Load  ───→├→ Server 2
         Balancer  └→ Server 3

อัลกอริทึม:
- Round Robin      → วนรอบไปทีละ server
- Least Connection → ส่งไป server ที่งานน้อยที่สุด
- IP Hash          → client เดิมไป server เดิม
- Weighted         → server แรงกว่ารับมากกว่า
```

### 9.4 Rate Limiting / Throttling

(ดูรายละเอียดในหัวข้อ 5.6)

### 9.5 Profiling and Monitoring (การตรวจสอบและติดตาม)

```
เครื่องมือที่ใช้บ่อย:
- APM: New Relic, Datadog, Dynatrace
- Logging: ELK Stack (Elasticsearch, Logstash, Kibana)
- Metrics: Prometheus + Grafana
- Tracing: Jaeger, Zipkin (ติดตาม request ข้าม services)

สิ่งที่ต้อง monitor:
- Response time (เฉลี่ย, P95, P99)
- Error rate
- Request rate
- CPU / Memory usage
- Database query time
```

### 9.6 Performance Testing (ทดสอบประสิทธิภาพ)

| ประเภท | เป้าหมาย | เครื่องมือ |
|--------|---------|-----------|
| **Load Testing** | ทดสอบภาระปกติ | k6, JMeter, Artillery |
| **Stress Testing** | หาจุดแตกหัก | k6, Locust |
| **Spike Testing** | ทดสอบ traffic พุ่งฉับพลัน | k6, Gatling |
| **Soak Testing** | ทดสอบรันนาน memory leak | k6, JMeter |

### 9.7 Error Handling / Retries

```
// Retry with Exponential Backoff
attempt 1: รอ 1 วินาที
attempt 2: รอ 2 วินาที
attempt 3: รอ 4 วินาที
attempt 4: รอ 8 วินาที (+ jitter สุ่มเวลาเล็กน้อย)

// Circuit Breaker Pattern
สถานะ:
- CLOSED  → ทำงานปกติ
- OPEN    → หยุดส่ง request (service ล่ม)
- HALF-OPEN → ลองส่งบางส่วน ถ้าสำเร็จกลับ CLOSED

[CLOSED] → error เกิน threshold → [OPEN]
[OPEN]   → รอ timeout → [HALF-OPEN]
[HALF-OPEN] → สำเร็จ → [CLOSED]
[HALF-OPEN] → ล้มเหลว → [OPEN]
```

---

## 10. API Integration Patterns (รูปแบบการเชื่อมต่อ API)

### 10.1 Synchronous vs Asynchronous APIs

```
// Synchronous — Client รอ response
Client → Request → Server
Client ← Response ← Server (รอจนเสร็จ)

// Asynchronous — Client ไม่ต้องรอ
Client → Request → Server
Client ← 202 Accepted ← Server (รับงานแล้ว)
Client → GET /status/123 → Server (ถามสถานะทีหลัง)
Client ← { "status": "completed" } ← Server
```

| | Synchronous | Asynchronous |
|---|---|---|
| **เหมาะกับ** | งานเร็ว, ต้องการผลทันที | งานนาน, ไม่ต้องรอ |
| **ตัวอย่าง** | ดึงข้อมูลผู้ใช้ | ประมวลผลวิดีโอ, ส่งอีเมล |

### 10.2 Event Driven Architecture

ระบบที่สื่อสารกันผ่าน event แทนการเรียกตรง

```
// แทนที่จะ:
Order Service → เรียก Inventory Service
              → เรียก Payment Service
              → เรียก Email Service

// ใช้ Event:
Order Service → publish "OrderCreated" event
                ↓
    ┌──────────────────────────────┐
    ↓              ↓               ↓
Inventory      Payment         Email
Service        Service         Service
(ลดสต็อก)      (เรียกเก็บเงิน)   (ส่งยืนยัน)
```

**ข้อดี:** Loose coupling, scale แยกกันได้, ทนทาน
**ข้อเสีย:** ซับซ้อน, debug ยาก, eventual consistency

### 10.3 API Gateways

จุดเข้าเดียว (single entry point) สำหรับทุก API request

```
Client → API Gateway → User Service
                     → Order Service
                     → Payment Service

หน้าที่ของ API Gateway:
- Routing (ส่ง request ไปถูก service)
- Authentication & Authorization
- Rate Limiting
- Load Balancing
- Caching
- Request/Response transformation
- Logging & Monitoring

เครื่องมือ: Kong, AWS API Gateway, Nginx, Traefik
```

### 10.4 Microservices Architecture

แบ่งแอปพลิเคชันออกเป็น service เล็กๆ แต่ละตัวทำงานอิสระ

```
Monolith:                    Microservices:
┌──────────────┐            ┌──────────┐  ┌──────────┐
│ User Module  │            │ User     │  │ Order    │
│ Order Module │     →      │ Service  │  │ Service  │
│ Payment Mod  │            └──────────┘  └──────────┘
│ Email Module │            ┌──────────┐  ┌──────────┐
└──────────────┘            │ Payment  │  │ Email    │
                            │ Service  │  │ Service  │
                            └──────────┘  └──────────┘
```

**ข้อดี:** deploy แยกกัน, scale แยกกัน, ใช้เทคโนโลยีต่างกันได้
**ข้อเสีย:** ซับซ้อนมาก, network latency, distributed transactions

### 10.5 Webhooks vs Polling

```
// Polling — Client ถามซ้ำเรื่อยๆ
while (true) {
  response = GET /api/order/123/status
  if (response.status === "completed") break
  sleep(5 seconds)  // ถามทุก 5 วินาที
}
// ❌ สิ้นเปลือง request ส่วนใหญ่ได้คำตอบเดิม

// Webhook — Server แจ้ง Client เมื่อมีเหตุการณ์
// Client ลงทะเบียน webhook URL
POST /api/webhooks
{ "url": "https://myapp.com/webhook", "events": ["order.completed"] }

// เมื่อ order เสร็จ Server ส่ง POST ไปที่ URL
POST https://myapp.com/webhook
{ "event": "order.completed", "data": { "order_id": 123 } }
// ✅ ส่งเฉพาะเมื่อมีเหตุการณ์จริง
```

### 10.6 Batch Processing

ประมวลผลหลาย request รวมกันในครั้งเดียว

```json
// แทนที่จะส่ง 100 requests แยกกัน
// ส่ง batch request ครั้งเดียว
POST /api/batch
{
  "requests": [
    { "method": "GET", "url": "/api/users/1" },
    { "method": "GET", "url": "/api/users/2" },
    { "method": "PUT", "url": "/api/users/3", "body": {"name": "Jane"} }
  ]
}

// Response
{
  "responses": [
    { "status": 200, "body": {"id": 1, "name": "John"} },
    { "status": 200, "body": {"id": 2, "name": "Jane"} },
    { "status": 200, "body": {"id": 3, "name": "Jane"} }
  ]
}
```

### 10.7 Messaging Queues (คิวข้อความ)

ตัวกลางรับ-ส่งข้อความระหว่าง service แบบ asynchronous

```
Producer → [Message Queue] → Consumer

ตัวอย่าง: สั่งซื้อสินค้า
Order Service → Queue → Email Service  (ส่งอีเมลยืนยัน)
                     → Inventory Service (ลดสต็อก)
```

#### RabbitMQ
Message broker ที่ใช้โปรโตคอล AMQP

```
- รองรับหลาย pattern: Direct, Topic, Fanout, Headers
- มี acknowledgment (รับรองว่า message ถูกประมวลผล)
- เหมาะกับ task queue, request/reply pattern
- ข้อมูลหายถ้าไม่ตั้งค่า persistence
```

#### Apache Kafka
Distributed streaming platform สำหรับข้อมูลปริมาณมาก

```
- เก็บข้อมูลเป็น log เรียงตามเวลา
- Consumer อ่านข้อมูลย้อนหลังได้
- ทนทานมาก (replication)
- เหมาะกับ event streaming, log aggregation, real-time analytics
- Throughput สูงมาก (ล้าน messages/วินาที)
```

| | RabbitMQ | Kafka |
|---|---|---|
| **รูปแบบ** | Message Broker | Event Streaming |
| **ข้อมูล** | ลบหลังอ่าน | เก็บไว้ตาม retention |
| **ความเร็ว** | เร็ว | เร็วมาก |
| **เหมาะกับ** | Task queues | Event streams, Big data |

---

## 11. API Testing (การทดสอบ API)

### 11.1 Unit Testing

ทดสอบแต่ละฟังก์ชัน/method แยกกัน mock dependencies

```javascript
// ทดสอบ function คำนวณราคา
test('calculateTotal returns correct total', () => {
  const items = [{ price: 100, qty: 2 }, { price: 50, qty: 1 }]
  expect(calculateTotal(items)).toBe(250)
})
```

### 11.2 Integration Testing

ทดสอบว่า components ทำงานร่วมกันได้ (เช่น API + Database)

```javascript
// ทดสอบ API endpoint จริงกับ database จริง
test('POST /users creates a user', async () => {
  const res = await request(app)
    .post('/api/users')
    .send({ name: 'John', email: 'john@example.com' })

  expect(res.status).toBe(201)
  expect(res.body.name).toBe('John')

  // ตรวจสอบว่าข้อมูลอยู่ใน database จริง
  const user = await db.users.findById(res.body.id)
  expect(user).toBeTruthy()
})
```

### 11.3 Functional Testing

ทดสอบ business logic ตั้งแต่ต้นจนจบ (end-to-end)

```
ทดสอบ flow สั่งซื้อสินค้า:
1. POST /api/cart/items → เพิ่มสินค้าลงตะกร้า
2. POST /api/orders → สร้าง order
3. POST /api/payments → ชำระเงิน
4. GET /api/orders/123 → ตรวจสอบสถานะ = "paid"
```

### 11.4 Load Testing

ทดสอบว่า API รับภาระได้มากแค่ไหน

```javascript
// k6 load test script
import http from 'k6/http'

export const options = {
  vus: 100,           // 100 virtual users
  duration: '30s',    // ทดสอบ 30 วินาที
}

export default function () {
  http.get('http://api.example.com/users')
}
```

### 11.5 Mocking APIs

สร้าง API ปลอมสำหรับทดสอบ ไม่ต้องพึ่ง service จริง

```javascript
// Mock API ด้วย MSW (Mock Service Worker)
import { rest } from 'msw'

const handlers = [
  rest.get('/api/users/:id', (req, res, ctx) => {
    return res(
      ctx.json({ id: req.params.id, name: 'Mock User' })
    )
  })
]
```

**เครื่องมือ:** Postman Mock Server, MSW, WireMock, json-server

### 11.6 Contract Testing

ตรวจสอบว่า Provider กับ Consumer ตกลงกันถูกต้องตาม "สัญญา"

```
Consumer (Frontend) กำหนดว่าต้องการ:
- GET /api/users/1 → { id: number, name: string, email: string }

Provider (Backend) ต้องตอบตาม contract:
- ถ้าเปลี่ยน field name → contract test จะ fail

เครื่องมือ: Pact, Spring Cloud Contract
```

---

## 12. Real-time APIs (API แบบเรียลไทม์)

### 12.1 WebSockets

การเชื่อมต่อแบบ 2 ทาง (bidirectional) ระหว่าง Client กับ Server

```
// HTTP ปกติ: Client ถาม → Server ตอบ (ทุกครั้ง)
Client → Request → Server
Client ← Response ← Server

// WebSocket: เชื่อมต่อครั้งเดียว คุยกันได้ตลอด
Client ←→ Server (เปิดค้างไว้)

// ตัวอย่าง JavaScript
const ws = new WebSocket('wss://api.example.com/chat')

ws.onopen = () => {
  ws.send(JSON.stringify({ type: 'join', room: 'general' }))
}

ws.onmessage = (event) => {
  const data = JSON.parse(event.data)
  console.log('ข้อความใหม่:', data.message)
}
```

**เหมาะกับ:** Chat, Game, Trading, Collaborative editing
**ข้อเสีย:** ใช้ resource มากกว่า HTTP, ต้องจัดการ connection

### 12.2 Server-Sent Events (SSE)

Server ส่งข้อมูลไปยัง Client ทางเดียว (one-way) ผ่าน HTTP

```
// Server ส่ง event stream
GET /api/notifications
Content-Type: text/event-stream

data: {"type": "order", "message": "สั่งซื้อสำเร็จ"}

data: {"type": "shipping", "message": "จัดส่งแล้ว"}

// Client (JavaScript)
const source = new EventSource('/api/notifications')
source.onmessage = (event) => {
  console.log(JSON.parse(event.data))
}
```

| | WebSocket | SSE |
|---|---|---|
| **ทิศทาง** | 2 ทาง | Server → Client เท่านั้น |
| **Protocol** | ws:// / wss:// | HTTP ปกติ |
| **Reconnect** | ต้องจัดการเอง | อัตโนมัติ |
| **เหมาะกับ** | Chat, Game | Notifications, Feed |
| **ผ่าน Firewall** | อาจมีปัญหา | ง่าย (HTTP ปกติ) |

---

## 13. Standards and Compliance (มาตรฐานและการปฏิบัติตามกฎ)

### 13.1 GDPR (General Data Protection Regulation)

กฎหมายคุ้มครองข้อมูลส่วนบุคคลของสหภาพยุโรป (EU)

```
สิ่งที่ API ต้องทำ:
- ขอความยินยอมก่อนเก็บข้อมูลส่วนบุคคล
- ให้ผู้ใช้ดูข้อมูลของตัวเอง (Right to Access)
  GET /api/users/me/data
- ให้ผู้ใช้ลบข้อมูล (Right to be Forgotten)
  DELETE /api/users/me/data
- แจ้งเมื่อข้อมูลรั่วไหลภายใน 72 ชั่วโมง
- เข้ารหัสข้อมูลส่วนบุคคล
```

### 13.2 CCPA (California Consumer Privacy Act)

กฎหมายคุ้มครองข้อมูลของรัฐแคลิฟอร์เนีย สหรัฐฯ

```
คล้าย GDPR แต่เฉพาะผู้อยู่อาศัยในแคลิฟอร์เนีย:
- สิทธิ์รู้ว่าข้อมูลอะไรถูกเก็บ
- สิทธิ์ลบข้อมูล
- สิทธิ์ปฏิเสธการขายข้อมูล
- ต้องมี "Do Not Sell My Personal Information" link
```

### 13.3 PCI DSS (Payment Card Industry Data Security Standard)

มาตรฐานความปลอดภัยสำหรับระบบที่รับบัตรเครดิต

```
สิ่งที่ต้องทำ:
- เข้ารหัสข้อมูลบัตรเครดิต (encryption)
- ไม่เก็บ CVV, PIN หลังจาก authorization
- จำกัดการเข้าถึงข้อมูลบัตร
- ทดสอบความปลอดภัยเป็นระยะ
- ใช้ tokenization แทนเก็บหมายเลขบัตรจริง

// ใช้ Payment Gateway (เช่น Stripe) แทนเก็บข้อมูลบัตรเอง
POST /api/payments
{ "token": "tok_visa_abc123", "amount": 1000 }
// ไม่ส่งหมายเลขบัตรผ่าน API ของเรา
```

### 13.4 HIPAA (Health Insurance Portability and Accountability Act)

กฎหมายคุ้มครองข้อมูลสุขภาพของสหรัฐฯ

```
ข้อมูลสุขภาพ (PHI) ที่ต้องปกป้อง:
- ชื่อผู้ป่วย, ที่อยู่, วันเกิด
- ผลการรักษา, การวินิจฉัย
- หมายเลขประกันสุขภาพ

สิ่งที่ API ต้องทำ:
- เข้ารหัสข้อมูลทั้ง transit (HTTPS) และ at rest
- Access control เข้มงวด
- Audit log ทุกการเข้าถึงข้อมูล
- Business Associate Agreement (BAA)
```

### 13.5 PII (Personally Identifiable Information)

ข้อมูลที่ระบุตัวบุคคลได้

```
ตัวอย่าง PII:
- ชื่อ-นามสกุล, อีเมล, เบอร์โทร
- เลขบัตรประชาชน, passport
- ที่อยู่, วันเกิด
- IP address, ข้อมูล biometric

วิธีจัดการใน API:
- เข้ารหัสเมื่อเก็บ (encryption at rest)
- ส่งผ่าน HTTPS เท่านั้น
- Masking: แสดง "***-***-1234" แทนเบอร์เต็ม
- ตั้ง data retention policy (ลบเมื่อไม่ต้องการ)
- ใช้ tokenization แทนข้อมูลจริง
```

---

## 14. API Lifecycle Management (การจัดการวงจรชีวิต API)

```
วงจรชีวิตของ API:

1. Design (ออกแบบ)
   └── กำหนด endpoints, data models, auth
       ใช้ API-first approach + OpenAPI spec

2. Develop (พัฒนา)
   └── เขียน code, unit tests
       ตาม design ที่กำหนดไว้

3. Test (ทดสอบ)
   └── Integration test, Load test
       Contract test

4. Deploy (ติดตั้ง)
   └── CI/CD pipeline
       Staging → Production

5. Publish (เผยแพร่)
   └── สร้าง documentation
       Developer portal, API keys

6. Monitor (ตรวจสอบ)
   └── Performance metrics
       Error tracking, Logging

7. Versioning (กำหนดเวอร์ชัน)
   └── เมื่อต้อง breaking change
       v1 → v2 ให้เวลา migrate

8. Deprecate (เลิกใช้)
   └── ประกาศล่วงหน้า
       Sunset header, Migration guide

9. Retire (ปิดใช้งาน)
   └── ปิด API เวอร์ชันเก่า
       หลังจากให้เวลา migrate เพียงพอ
```

**Deprecation Best Practices:**
```
// ใช้ Sunset Header แจ้งวันเลิกใช้
Sunset: Sat, 01 Jan 2025 00:00:00 GMT
Deprecation: true
Link: <https://api.example.com/v2>; rel="successor-version"

// Response body เตือน
{
  "data": {...},
  "_deprecation": {
    "message": "API v1 จะปิดใช้งาน 1 ม.ค. 2025 กรุณาย้ายไป v2",
    "sunset": "2025-01-01",
    "migration_guide": "https://docs.example.com/migrate-v2"
  }
}
```

---

## 15. สรุปลำดับการเรียน

```
ขั้น 1 (สัปดาห์ 1-2): พื้นฐาน
├── API คืออะไร
├── HTTP Methods, Status Codes, Headers
├── HTTP Versions, Caching
├── URL, Query & Path Parameters
├── TCP/IP & DNS พื้นฐาน
└── Content Negotiation

ขั้น 2 (สัปดาห์ 3-4): RESTful APIs & API Styles
├── REST Principles & URI Design
├── CRUD Operations
├── Versioning Strategies
├── Simple JSON APIs
├── SOAP, GraphQL, gRPC (ภาพรวม)
└── CORS & Cookies

ขั้น 3 (สัปดาห์ 5-6): การสร้าง API
├── Pagination & Rate Limiting
├── Error Handling & RFC 7807
├── Idempotency & HATEOAS
└── API Documentation (Swagger/OpenAPI)

ขั้น 4 (สัปดาห์ 7-8): Authentication & Authorization
├── Basic Auth, Token Auth, JWT
├── OAuth 2.0, Session Auth
├── RBAC, ABAC และ Authorization Models อื่นๆ
└── API Keys & Management

ขั้น 5 (สัปดาห์ 9-10): Security & Performance
├── API Security (ช่องโหว่ & Best Practices)
├── Caching Strategies & Load Balancing
├── Performance Metrics & Monitoring
└── Performance Testing

ขั้น 6 (สัปดาห์ 11-12): Integration & Testing
├── Sync vs Async APIs
├── Webhooks vs Polling
├── Event Driven & Messaging (RabbitMQ, Kafka)
├── API Gateways & Microservices
├── Batch Processing
└── Unit, Integration, Functional, Load Testing

ขั้น 7 (สัปดาห์ 13-14): Advanced Topics
├── Real-time APIs (WebSocket, SSE)
├── Contract Testing & Mocking
├── Standards & Compliance (GDPR, CCPA, PCI DSS, HIPAA, PII)
└── API Lifecycle Management
```
