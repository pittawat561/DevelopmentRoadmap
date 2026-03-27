# How The Backend Works

> 📺 **แหล่งที่มา:** [YouTube - How The Backend Works](https://www.youtube.com/watch?v=4r6WdaY3SOA)

---

## ภาพรวม

เว็บประกอบด้วย 2 ส่วนหลัก:
- **Frontend** — ส่วนที่ผู้ใช้มองเห็นและโต้ตอบได้ (HTML, CSS, JavaScript)
- **Backend** — ส่วนที่ทำงานอยู่เบื้องหลัง จัดการ Logic, ข้อมูล และ ความปลอดภัย

```
┌─────────────────────────────────────────────────────────┐
│                     The Web                              │
│                                                          │
│   ┌──────────────┐              ┌──────────────────┐    │
│   │   Frontend   │  ◄──────►   │     Backend      │    │
│   │              │              │                  │    │
│   │  • HTML      │   HTTP       │  • Server Logic  │    │
│   │  • CSS       │   Request/   │  • Database      │    │
│   │  • JavaScript│   Response   │  • Authentication│    │
│   │              │              │  • API           │    │
│   └──────────────┘              └──────────────────┘    │
│    (สิ่งที่ผู้ใช้เห็น)            (สิ่งที่ทำงานเบื้องหลัง)    │
└─────────────────────────────────────────────────────────┘
```

---

## 1. URL (Uniform Resource Locator) — ที่อยู่ของ Resource

URL คือที่อยู่ที่บอก Browser ว่าจะหา Resource อะไร อยู่ที่ไหน และเข้าถึงอย่างไร

### โครงสร้างของ URL

```
https://www.example.com:443/products/shoes?color=red&size=10#reviews
──┬──   ───────┬───────  ┬  ──────┬─────  ────────┬───────  ───┬──
  │            │         │        │               │            │
Protocol     Host      Port     Path         Query String   Fragment
```

### แต่ละส่วนของ URL

| ส่วน | ตัวอย่าง | คำอธิบาย |
|------|----------|----------|
| **Protocol** | `https://` | วิธีที่ Browser ใช้สื่อสารกับ Server |
| **Host / Domain** | `www.example.com` | ชื่อของ Server ที่เก็บ Resource |
| **Port** | `:443` | "ประตู" ที่ Server ใช้รับ Request (ปกติซ่อนอยู่) |
| **Path** | `/products/shoes` | ตำแหน่งของ Resource ที่ต้องการ |
| **Query String** | `?color=red&size=10` | ข้อมูลเพิ่มเติมที่ส่งไปกับ Request |
| **Fragment** | `#reviews` | ตำแหน่งเฉพาะในหน้าเว็บ (ไม่ถูกส่งไป Server) |

### Protocol: HTTP vs HTTPS

| | HTTP | HTTPS |
|--|------|-------|
| **ชื่อเต็ม** | HyperText Transfer Protocol | HyperText Transfer Protocol **Secure** |
| **Port เริ่มต้น** | 80 | 443 |
| **การเข้ารหัส** | ❌ ไม่มี | ✅ SSL/TLS |
| **ความปลอดภัย** | ข้อมูลส่งแบบ Plain Text | ข้อมูลถูกเข้ารหัส |
| **ใช้งาน** | เว็บทั่วไป (ไม่แนะนำ) | เว็บที่ต้องการความปลอดภัย (แนะนำ) |

### Query String — ส่งข้อมูลผ่าน URL

```
https://www.google.com/search?q=backend+development&lang=th
                               │                    │
                               └─ Parameter 1       └─ Parameter 2

รูปแบบ: ?key1=value1&key2=value2&key3=value3
         │           │
         └─ เริ่มด้วย ?  └─ คั่นด้วย &
```

**ตัวอย่างการใช้งาน:**
- ค้นหา: `?q=search+term`
- กรองข้อมูล: `?category=electronics&sort=price`
- แบ่งหน้า: `?page=2&limit=20`

---

## 2. HTTP Request — การส่งคำขอไปยัง Server

เมื่อผู้ใช้โต้ตอบกับเว็บ (พิมพ์ URL, คลิกลิงก์, ส่ง Form) Browser จะสร้าง **HTTP Request** ส่งไปยัง Server

### ส่วนประกอบของ HTTP Request

```
┌─────────────────────────────────────────┐
│             HTTP Request                │
├─────────────────────────────────────────┤
│ 1. Request Line                         │
│    GET /products/shoes?color=red HTTP/1.1│
│    ─┬─ ────────────┬────────── ───┬───  │
│     │              │              │     │
│   Method          URL          Version  │
├─────────────────────────────────────────┤
│ 2. Headers                              │
│    Host: www.example.com                │
│    Content-Type: application/json       │
│    Authorization: Bearer <token>        │
│    Cookie: session_id=abc123            │
├─────────────────────────────────────────┤
│ 3. Body (ไม่บังคับ - ใช้กับ POST/PUT)    │
│    {                                    │
│      "username": "john",               │
│      "password": "secret"              │
│    }                                    │
└─────────────────────────────────────────┘
```

---

## 3. HTTP Methods (Actions) — ประเภทของคำขอ

HTTP Method บอก Server ว่าผู้ใช้ต้องการ **ทำอะไร** กับ Resource

### Methods หลักๆ

| Method | คำอธิบาย | ตัวอย่างการใช้งาน | มี Body? |
|--------|----------|-------------------|----------|
| **GET** | ดึง/อ่านข้อมูล | เปิดหน้าเว็บ, ดูรายการสินค้า | ❌ ไม่มี |
| **POST** | สร้างข้อมูลใหม่ | ลงทะเบียน, ส่งคอมเมนต์ | ✅ มี |
| **PUT** | แก้ไขข้อมูลทั้งหมด | อัปเดตโปรไฟล์ทั้งหมด | ✅ มี |
| **PATCH** | แก้ไขข้อมูลบางส่วน | เปลี่ยนรหัสผ่านอย่างเดียว | ✅ มี |
| **DELETE** | ลบข้อมูล | ลบบัญชี, ลบโพสต์ | ❌ ไม่มี |

### เปรียบเทียบ GET vs POST

| | GET | POST |
|--|-----|------|
| **ข้อมูลอยู่ที่** | URL (Query String) | Body ของ Request |
| **ความปลอดภัย** | ⚠️ ข้อมูลเห็นได้ใน URL | ✅ ข้อมูลซ่อนใน Body |
| **Cache ได้** | ✅ ได้ | ❌ ไม่ได้ |
| **Bookmark ได้** | ✅ ได้ | ❌ ไม่ได้ |
| **ขนาดข้อมูล** | จำกัด (~2048 chars) | ไม่จำกัด |
| **Idempotent** | ✅ (ทำซ้ำได้ผลเหมือนเดิม) | ❌ (ทำซ้ำอาจได้ผลต่างกัน) |

### ตัวอย่างจริง

```
# GET — ดึงรายการสินค้า
GET /api/products HTTP/1.1
Host: www.shop.com

# POST — สร้างสินค้าใหม่
POST /api/products HTTP/1.1
Host: www.shop.com
Content-Type: application/json

{
  "name": "รองเท้ากีฬา",
  "price": 2500,
  "category": "shoes"
}

# PUT — แก้ไขสินค้าทั้งหมด
PUT /api/products/42 HTTP/1.1
Host: www.shop.com
Content-Type: application/json

{
  "name": "รองเท้ากีฬา Pro",
  "price": 3500,
  "category": "shoes"
}

# DELETE — ลบสินค้า
DELETE /api/products/42 HTTP/1.1
Host: www.shop.com
```

---

## 4. Server Processing — การประมวลผลบน Server

เมื่อ Server ได้รับ Request จะทำตามขั้นตอน:

```
                    HTTP Request จาก Client
                            │
                            ▼
              ┌─────────────────────────┐
              │     1. รับ Request       │
              │   (Parse URL, Method,   │
              │    Headers, Body)       │
              └───────────┬─────────────┘
                          │
                          ▼
              ┌─────────────────────────┐
              │      2. Routing         │
              │  (จับคู่ URL + Method   │
              │   กับ Handler ที่ถูกต้อง) │
              └───────────┬─────────────┘
                          │
                          ▼
              ┌─────────────────────────┐
              │  3. ประมวลผล Logic      │
              │  • ตรวจสอบ Auth          │
              │  • Validate ข้อมูล       │
              │  • Business Logic       │
              └───────────┬─────────────┘
                          │
                          ▼
              ┌─────────────────────────┐
              │  4. จัดการ Database      │
              │  • Query / Insert       │
              │  • Update / Delete      │
              └───────────┬─────────────┘
                          │
                          ▼
              ┌─────────────────────────┐
              │   5. สร้าง Response      │
              │  • Status Code          │
              │  • Headers              │
              │  • Body (HTML / JSON)   │
              └───────────┬─────────────┘
                          │
                          ▼
                  ส่ง Response กลับ Client
```

### Routing — จับคู่ Request กับ Handler

Server ใช้ **URL Path** + **HTTP Method** เพื่อหาว่าจะรัน Code ส่วนไหน:

```
GET  /             →  แสดงหน้าแรก
GET  /products     →  แสดงรายการสินค้า
GET  /products/42  →  แสดงสินค้า ID 42
POST /products     →  สร้างสินค้าใหม่
PUT  /products/42  →  แก้ไขสินค้า ID 42
DELETE /products/42 → ลบสินค้า ID 42
```

---

## 5. HTTP Response — การตอบกลับจาก Server

### ส่วนประกอบของ HTTP Response

```
┌─────────────────────────────────────────┐
│            HTTP Response                │
├─────────────────────────────────────────┤
│ 1. Status Line                          │
│    HTTP/1.1 200 OK                      │
│    ───┬───  ─┬─ ─┬─                    │
│       │      │   │                      │
│    Version Code Message                 │
├─────────────────────────────────────────┤
│ 2. Headers                              │
│    Content-Type: text/html              │
│    Content-Length: 1234                  │
│    Set-Cookie: session=xyz              │
├─────────────────────────────────────────┤
│ 3. Body                                 │
│    <!DOCTYPE html>                      │
│    <html>                               │
│      <head><title>My Page</title></head>│
│      <body>                             │
│        <h1>Welcome!</h1>                │
│      </body>                            │
│    </html>                              │
└─────────────────────────────────────────┘
```

### HTTP Status Codes ที่สำคัญ

| กลุ่ม | ช่วง | ความหมาย | ตัวอย่าง |
|-------|------|----------|----------|
| ✅ **Success** | 2xx | สำเร็จ | `200 OK`, `201 Created`, `204 No Content` |
| 🔀 **Redirect** | 3xx | เปลี่ยนเส้นทาง | `301 Moved Permanently`, `304 Not Modified` |
| ⚠️ **Client Error** | 4xx | ผู้ใช้ผิดพลาด | `400 Bad Request`, `401 Unauthorized`, `403 Forbidden`, `404 Not Found` |
| ❌ **Server Error** | 5xx | Server ผิดพลาด | `500 Internal Server Error`, `502 Bad Gateway`, `503 Service Unavailable` |

### Status Codes ที่พบบ่อยที่สุด

```
200 OK              → Request สำเร็จ (ใช้บ่อยที่สุด)
201 Created         → สร้าง Resource ใหม่สำเร็จ
400 Bad Request     → Request ไม่ถูกต้อง (ข้อมูลผิด format)
401 Unauthorized    → ยังไม่ได้ Login / Token หมดอายุ
403 Forbidden       → Login แล้ว แต่ไม่มีสิทธิ์เข้าถึง
404 Not Found       → หา Resource ที่ร้องขอไม่เจอ
500 Internal Error  → Server มีปัญหาภายใน (Bug)
```

---

## 6. Dynamic vs Static Content

### Static Content (เนื้อหาคงที่)

- ไฟล์ที่ **ไม่เปลี่ยนแปลง** เช่น HTML, CSS, JS, รูปภาพ
- Server ส่งไฟล์กลับไปตรงๆ โดยไม่ต้องประมวลผล

```
Client: GET /about.html
Server: → ส่งไฟล์ about.html กลับไปเลย (ไม่ต้องทำอะไร)
```

### Dynamic Content (เนื้อหาที่สร้างขึ้นแบบ Real-time)

- เนื้อหาถูก **สร้างขึ้นใหม่** ทุกครั้งที่มี Request
- ขึ้นอยู่กับ: ผู้ใช้, เวลา, ข้อมูลในฐานข้อมูล

```
Client: GET /dashboard
Server: → ตรวจสอบว่าเป็นใคร
       → ดึงข้อมูลจาก Database
       → สร้าง HTML เฉพาะสำหรับผู้ใช้คนนี้
       → ส่ง HTML กลับ
```

### เปรียบเทียบ

```
        Static                          Dynamic
┌───────────────────┐          ┌───────────────────┐
│                   │          │                   │
│   ไฟล์ที่มีอยู่แล้ว  │          │  สร้างขึ้นใหม่ทุกครั้ง  │
│                   │          │                   │
│  about.html       │          │  dashboard → HTML │
│  style.css        │          │  profile → HTML   │
│  logo.png         │          │  search → JSON    │
│                   │          │                   │
│  เร็ว, ง่าย, Cache ได้│          │  ยืดหยุ่น, Personalized│
│                   │          │                   │
└───────────────────┘          └───────────────────┘
```

---

## 7. สรุป Flow ทั้งหมด — The Complete Picture

```
 ผู้ใช้พิมพ์ URL หรือคลิกลิงก์
              │
              ▼
    ┌──────────────────┐
    │  1. Browser สร้าง │
    │     HTTP Request  │
    │                   │
    │  Method: GET      │
    │  URL: /products   │
    │  Headers: ...     │
    └────────┬─────────┘
             │
             │  ──── Internet ────
             │
             ▼
    ┌──────────────────┐
    │  2. Server รับ    │
    │     Request       │
    │                   │
    │  Parse URL        │
    │  → Protocol       │
    │  → Host           │
    │  → Path           │
    │  → Query String   │
    │  → Method         │
    └────────┬─────────┘
             │
             ▼
    ┌──────────────────┐
    │  3. Routing       │
    │                   │
    │  GET /products    │
    │  → productsHandler│
    └────────┬─────────┘
             │
             ▼
    ┌──────────────────┐
    │  4. ประมวลผล      │
    │                   │
    │  Query Database   │
    │  Business Logic   │
    │  Generate HTML    │
    └────────┬─────────┘
             │
             ▼
    ┌──────────────────┐
    │  5. สร้าง Response│
    │                   │
    │  Status: 200 OK   │
    │  Body: <html>...  │
    └────────┬─────────┘
             │
             │  ──── Internet ────
             │
             ▼
    ┌──────────────────┐
    │  6. Browser รับ   │
    │     Response      │
    │                   │
    │  แสดงผล HTML      │
    │  ให้ผู้ใช้เห็น      │
    └──────────────────┘
```

---

## 📝 สรุป Key Concepts

| แนวคิด | คำอธิบายสั้นๆ |
|--------|--------------|
| **Frontend vs Backend** | Frontend = สิ่งที่เห็น, Backend = สิ่งที่ทำงานเบื้องหลัง |
| **URL** | ที่อยู่ของ Resource บนเว็บ ประกอบด้วย Protocol, Host, Path, Query String |
| **HTTP Protocol** | กฎการสื่อสารระหว่าง Client กับ Server |
| **HTTP Methods** | GET (อ่าน), POST (สร้าง), PUT (แก้ไข), DELETE (ลบ) |
| **Request** | ข้อความที่ Client ส่งไป Server ประกอบด้วย Method, URL, Headers, Body |
| **Response** | ข้อความที่ Server ส่งกลับ ประกอบด้วย Status Code, Headers, Body |
| **Status Codes** | รหัสตัวเลขบอกผลลัพธ์: 2xx=สำเร็จ, 4xx=Client ผิด, 5xx=Server ผิด |
| **Routing** | การจับคู่ URL+Method กับ Code ที่จะประมวลผล |
| **Static vs Dynamic** | Static=ไฟล์คงที่, Dynamic=สร้างขึ้นใหม่ตาม Request |

---

> 💡 **สิ่งที่ควรจำ:** Backend ทำหน้าที่เป็น "สมอง" ของเว็บแอปพลิเคชัน — รับ Request จากผู้ใช้ → ประมวลผล → จัดการข้อมูล → สร้าง Response ส่งกลับ ทุกอย่างเกิดขึ้นภายในเสี้ยววินาที!
