# Different API Styles - รูปแบบ API ทั้งหมด

> เจาะลึกทุกรูปแบบ API จาก roadmap.sh/api-design

---

## 1. RESTful APIs

### REST คืออะไร

REST (Representational State Transfer) เป็น **สถาปัตยกรรม** (ไม่ใช่โปรโตคอล) สำหรับการออกแบบ API ที่ใช้ HTTP เป็นพื้นฐาน คิดค้นโดย Roy Fielding ในปี 2000

แนวคิดหลัก: ทุกอย่างคือ **Resource** (ทรัพยากร) และเราใช้ HTTP Methods จัดการกับ resource เหล่านั้น

### หลักการ 6 ข้อของ REST

#### 1. Client-Server Separation
```
Client (Frontend)  ←→  Server (Backend)
แยกจากกันอิสระ พัฒนาแยกกันได้

✅ Frontend ใช้ React, Mobile, Desktop อะไรก็ได้
✅ Backend เปลี่ยนภาษา/database ได้โดย Client ไม่กระทบ
```

#### 2. Stateless
```
❌ ผิดหลัก (Stateful):
Request 1: POST /login → Server เก็บว่า "user นี้ login แล้ว"
Request 2: GET /profile → Server จำได้ว่า login แล้ว

✅ ถูกหลัก (Stateless):
ทุก request ต้องแนบข้อมูลยืนยันตัวตนมาด้วย
Request 1: GET /profile + Authorization: Bearer <token>
Request 2: GET /orders + Authorization: Bearer <token>
```

#### 3. Cacheable
```
// Server บอก Client ว่า cache ได้นานแค่ไหน
HTTP/1.1 200 OK
Cache-Control: max-age=3600
ETag: "abc123"

// Client ไม่ต้องเรียก API ซ้ำถ้า cache ยังไม่หมดอายุ
// ลด load บน server ได้มาก
```

#### 4. Uniform Interface
```
ใช้รูปแบบเดียวกันทั้ง API:
- Resource ระบุด้วย URI          → /users/123
- ใช้ HTTP Methods จัดการ        → GET, POST, PUT, DELETE
- Response มีข้อมูลเพียงพอ      → JSON + metadata
- Self-descriptive messages     → Content-Type, Status Code
```

#### 5. Layered System
```
Client → CDN → Load Balancer → API Gateway → Server → Database

Client ไม่จำเป็นต้องรู้ว่ามี layer อะไรอยู่ข้างหลัง
แต่ละ layer ทำหน้าที่ของตัวเอง
```

#### 6. Code on Demand (ไม่บังคับ)
```
Server สามารถส่ง executable code (เช่น JavaScript) ให้ Client รันได้
ในทางปฏิบัติใช้น้อยมากกับ API
```

### การออกแบบ RESTful API ที่ดี

#### Resource Naming
```
✅ ถูกต้อง:
/users                    → รายการผู้ใช้ (collection)
/users/123                → ผู้ใช้คนเดียว (resource)
/users/123/orders         → ออเดอร์ของผู้ใช้ (sub-resource)
/users/123/orders/456     → ออเดอร์เฉพาะของผู้ใช้
/order-items              → ใช้ kebab-case
/search?q=john            → ค้นหา (ใช้ query param)

❌ ผิดหลัก:
/getUsers                 → อย่าใส่ verb
/users/create             → อย่าใส่ action
/user                     → ใช้พหูพจน์
/Users                    → ใช้ตัวพิมพ์เล็ก
/users/123/orders/456/items/789/details  → ซ้อนลึกเกินไป (ไม่เกิน 3 ชั้น)
```

#### CRUD Mapping
```
POST   /users              201 Created     สร้างผู้ใช้ใหม่
GET    /users              200 OK          ดึงรายการผู้ใช้
GET    /users/123          200 OK          ดึงผู้ใช้ ID 123
PUT    /users/123          200 OK          อัปเดตผู้ใช้ทั้ง record
PATCH  /users/123          200 OK          อัปเดตบางฟิลด์
DELETE /users/123          204 No Content  ลบผู้ใช้
```

#### Filtering, Sorting, Pagination
```
// Filtering
GET /users?role=admin&status=active

// Sorting
GET /users?sort=name&order=asc
GET /users?sort=-created_at          → เครื่องหมาย - = desc

// Pagination
GET /users?page=2&limit=20

// รวมกัน
GET /users?role=admin&sort=-created_at&page=1&limit=10
```

#### Response Format
```json
// ✅ Response ที่ดี — สร้างผู้ใช้สำเร็จ
// POST /users → 201 Created
{
  "data": {
    "id": 123,
    "name": "John Doe",
    "email": "john@example.com",
    "created_at": "2024-01-15T10:30:00Z"
  },
  "links": {
    "self": "/api/users/123"
  }
}

// ✅ Response ที่ดี — รายการพร้อม pagination
// GET /users → 200 OK
{
  "data": [
    { "id": 1, "name": "John" },
    { "id": 2, "name": "Jane" }
  ],
  "pagination": {
    "page": 1,
    "limit": 20,
    "total": 150,
    "total_pages": 8
  },
  "links": {
    "self": "/api/users?page=1",
    "next": "/api/users?page=2",
    "last": "/api/users?page=8"
  }
}

// ✅ Error Response
// GET /users/999 → 404 Not Found
{
  "error": {
    "code": "USER_NOT_FOUND",
    "message": "ไม่พบผู้ใช้ ID 999"
  }
}
```

### ข้อดี-ข้อเสียของ REST

| ข้อดี | ข้อเสีย |
|-------|---------|
| เข้าใจง่าย ใช้ HTTP มาตรฐาน | Over-fetching (ได้ข้อมูลเกินที่ต้องการ) |
| Cache ได้ง่าย | Under-fetching (ต้องเรียกหลาย endpoint) |
| รองรับทุกภาษา/platform | ไม่เหมาะกับ relationship ซับซ้อน |
| Stateless scale ง่าย | Versioning อาจซับซ้อน |
| เอกสารมากมาย (OpenAPI) | ไม่มี real-time built-in |

---

## 2. Simple JSON APIs

### Simple JSON API คืออะไร

API ง่ายๆ ที่รับ-ส่งข้อมูลเป็น JSON โดยไม่จำเป็นต้องตามหลัก REST อย่างเคร่งครัด มักใช้ในโปรเจกต์เล็กหรือ internal API

### ความแตกต่างจาก REST

```
// REST — ตามหลักเคร่งครัด
GET    /api/users/123
PUT    /api/users/123
DELETE /api/users/123

// Simple JSON API — ยืดหยุ่นกว่า
POST /api/getUser        { "id": 123 }
POST /api/updateUser     { "id": 123, "name": "John" }
POST /api/deleteUser     { "id": 123 }
```

### ตัวอย่างการใช้งาน

```json
// Request — ดึงข้อมูลผู้ใช้
POST /api/user/get
Content-Type: application/json
{
  "user_id": 123
}

// Response
{
  "success": true,
  "data": {
    "id": 123,
    "name": "John",
    "email": "john@example.com"
  }
}

// Request — อัปเดตผู้ใช้
POST /api/user/update
{
  "user_id": 123,
  "name": "Jane"
}

// Response
{
  "success": true,
  "message": "อัปเดตสำเร็จ"
}

// Error Response
{
  "success": false,
  "error": {
    "code": "NOT_FOUND",
    "message": "ไม่พบผู้ใช้"
  }
}
```

### JSON:API Specification

มาตรฐาน JSON API ที่กำหนดรูปแบบ request/response ชัดเจน

```json
// JSON:API format
GET /api/articles/1

{
  "data": {
    "type": "articles",
    "id": "1",
    "attributes": {
      "title": "Hello World",
      "body": "This is my first article"
    },
    "relationships": {
      "author": {
        "data": { "type": "people", "id": "42" }
      },
      "comments": {
        "data": [
          { "type": "comments", "id": "5" },
          { "type": "comments", "id": "12" }
        ]
      }
    },
    "links": {
      "self": "/api/articles/1"
    }
  },
  "included": [
    {
      "type": "people",
      "id": "42",
      "attributes": { "name": "John" }
    }
  ]
}
```

### ข้อดี-ข้อเสีย

| ข้อดี | ข้อเสีย |
|-------|---------|
| ง่ายมาก เริ่มเร็ว | ไม่มีมาตรฐาน |
| ยืดหยุ่น | ทีมใหม่เข้าใจยาก |
| เหมาะกับ prototype/MVP | Scale ยาก |
| ไม่ต้องเรียนหลัก REST | Cache ยาก (POST ทั้งหมด) |

---

## 3. SOAP APIs (Simple Object Access Protocol)

### SOAP คืออะไร

SOAP เป็น **โปรโตคอล** (ไม่ใช่แค่สถาปัตยกรรม) สำหรับแลกเปลี่ยนข้อมูลแบบมีโครงสร้าง ใช้ **XML** เป็นรูปแบบข้อมูล มีมาตรฐานเข้มงวดมาก

### โครงสร้าง SOAP Message

```xml
<?xml version="1.0" encoding="UTF-8"?>
<!--
  SOAP Envelope — ครอบทุกอย่าง
  ├── Header (ไม่บังคับ) — ข้อมูลเพิ่มเติม เช่น auth, transaction
  └── Body (บังคับ) — ข้อมูลหลักที่ส่ง/รับ
       └── Fault (ถ้ามี error)
-->

<soap:Envelope
  xmlns:soap="http://www.w3.org/2003/05/soap-envelope"
  xmlns:usr="http://example.com/users">

  <soap:Header>
    <!-- Authentication -->
    <usr:AuthToken>Bearer abc123xyz</usr:AuthToken>
    <!-- Transaction ID -->
    <usr:TransactionId>txn-456</usr:TransactionId>
  </soap:Header>

  <soap:Body>
    <!-- Request: ดึงข้อมูลผู้ใช้ -->
    <usr:GetUserRequest>
      <usr:UserId>123</usr:UserId>
    </usr:GetUserRequest>
  </soap:Body>

</soap:Envelope>
```

### SOAP Response

```xml
<!-- Response สำเร็จ -->
<soap:Envelope xmlns:soap="http://www.w3.org/2003/05/soap-envelope">
  <soap:Body>
    <GetUserResponse>
      <User>
        <Id>123</Id>
        <Name>John Doe</Name>
        <Email>john@example.com</Email>
        <Department>Engineering</Department>
      </User>
    </GetUserResponse>
  </soap:Body>
</soap:Envelope>

<!-- Response Error (SOAP Fault) -->
<soap:Envelope xmlns:soap="http://www.w3.org/2003/05/soap-envelope">
  <soap:Body>
    <soap:Fault>
      <soap:Code>
        <soap:Value>soap:Sender</soap:Value>
      </soap:Code>
      <soap:Reason>
        <soap:Text>ไม่พบผู้ใช้ ID 999</soap:Text>
      </soap:Reason>
      <soap:Detail>
        <ErrorCode>USER_NOT_FOUND</ErrorCode>
      </soap:Detail>
    </soap:Fault>
  </soap:Body>
</soap:Envelope>
```

### WSDL (Web Services Description Language)

เอกสาร XML ที่อธิบาย SOAP API ทั้งหมด — endpoint, operations, data types

```xml
<!-- ตัวอย่าง WSDL แบบย่อ -->
<definitions name="UserService"
  targetNamespace="http://example.com/users">

  <!-- ประเภทข้อมูล -->
  <types>
    <schema>
      <element name="GetUserRequest">
        <complexType>
          <sequence>
            <element name="UserId" type="int"/>
          </sequence>
        </complexType>
      </element>
      <element name="GetUserResponse">
        <complexType>
          <sequence>
            <element name="Name" type="string"/>
            <element name="Email" type="string"/>
          </sequence>
        </complexType>
      </element>
    </schema>
  </types>

  <!-- Operations ที่มี -->
  <portType name="UserPortType">
    <operation name="GetUser">
      <input message="GetUserRequest"/>
      <output message="GetUserResponse"/>
    </operation>
  </portType>

  <!-- Endpoint -->
  <service name="UserService">
    <port binding="UserBinding">
      <address location="http://api.example.com/soap/users"/>
    </port>
  </service>
</definitions>
```

### WS-Security

มาตรฐานความปลอดภัยสำหรับ SOAP

```xml
<soap:Header>
  <wsse:Security>
    <!-- Username/Password -->
    <wsse:UsernameToken>
      <wsse:Username>admin</wsse:Username>
      <wsse:Password>secret123</wsse:Password>
      <wsse:Nonce>abc123</wsse:Nonce>
      <wsu:Created>2024-01-15T10:00:00Z</wsu:Created>
    </wsse:UsernameToken>

    <!-- หรือใช้ Digital Signature -->
    <ds:Signature>...</ds:Signature>

    <!-- หรือ Encryption -->
    <xenc:EncryptedData>...</xenc:EncryptedData>
  </wsse:Security>
</soap:Header>
```

### เมื่อไหร่ใช้ SOAP

```
✅ ใช้ SOAP เมื่อ:
- ระบบ Enterprise ที่ต้องการมาตรฐานเข้มงวด
- ธุรกรรมทางการเงิน (ธนาคาร, การชำระเงิน)
- ต้องการ WS-Security, WS-Transaction
- ต้อง integrate กับระบบเก่า (Legacy systems)
- ต้องการ ACID transactions ข้าม services

❌ ไม่ควรใช้ SOAP เมื่อ:
- โปรเจกต์ใหม่ทั่วไป
- Mobile app (XML หนักเกินไป)
- ต้องการความเร็ว
- ทีมเล็ก ไม่ต้องการความซับซ้อน
```

### ข้อดี-ข้อเสีย

| ข้อดี | ข้อเสีย |
|-------|---------|
| มาตรฐานเข้มงวด (WSDL) | XML verbose (ข้อมูลเยอะ ช้า) |
| WS-Security ระดับ enterprise | ซับซ้อนมาก เรียนรู้ยาก |
| รองรับ ACID transactions | ไม่เหมาะกับ mobile/web สมัยใหม่ |
| Error handling ชัดเจน (SOAP Fault) | ไม่ cache ง่ายเหมือน REST |
| สร้าง client code จาก WSDL ได้ | ต้องใช้เครื่องมือเฉพาะ |
| ทำงานผ่าน HTTP, SMTP, TCP ได้ | Payload ใหญ่กว่า JSON มาก |

---

## 4. GraphQL APIs

### GraphQL คืออะไร

GraphQL เป็น **query language** สำหรับ API คิดค้นโดย Facebook ในปี 2012 (เปิดให้สาธารณะ 2015) ให้ Client กำหนดเองว่าต้องการข้อมูลอะไร ไม่มี over-fetching หรือ under-fetching

### ปัญหาของ REST ที่ GraphQL แก้

```
ปัญหา 1: Over-fetching (ได้ข้อมูลเกิน)
REST: GET /users/123 → ได้ทุก field แม้ต้องการแค่ชื่อ
{
  "id": 123,
  "name": "John",          ← ต้องการแค่นี้
  "email": "...",           ← ไม่ต้องการ
  "address": "...",         ← ไม่ต้องการ
  "phone": "...",           ← ไม่ต้องการ
  "created_at": "...",      ← ไม่ต้องการ
  "preferences": {...}      ← ไม่ต้องการ
}

ปัญหา 2: Under-fetching (ต้องเรียกหลาย endpoint)
REST: ต้องการข้อมูลผู้ใช้ + ออเดอร์ + รีวิว
  Request 1: GET /users/123
  Request 2: GET /users/123/orders
  Request 3: GET /users/123/reviews
  → 3 round trips!

GraphQL: ครั้งเดียวได้ทุกอย่าง
```

### Schema Definition

```graphql
# กำหนด Type (โครงสร้างข้อมูล)
type User {
  id: ID!                    # ! = ห้าม null
  name: String!
  email: String!
  age: Int
  orders: [Order!]!          # array ของ Order
  profile: Profile
}

type Order {
  id: ID!
  product: String!
  quantity: Int!
  total: Float!
  status: OrderStatus!
  created_at: String!
}

type Profile {
  bio: String
  avatar: String
  website: String
}

# Enum
enum OrderStatus {
  PENDING
  PROCESSING
  SHIPPED
  DELIVERED
  CANCELLED
}

# Input Type (สำหรับ mutation)
input CreateUserInput {
  name: String!
  email: String!
  age: Int
}

input UpdateUserInput {
  name: String
  email: String
  age: Int
}
```

### Query (ดึงข้อมูล)

```graphql
# Query — ดึงเฉพาะข้อมูลที่ต้องการ
query {
  user(id: 123) {
    name
    email
    orders {
      id
      product
      total
      status
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
        {
          "id": "1",
          "product": "Laptop",
          "total": 25000.00,
          "status": "DELIVERED"
        },
        {
          "id": "2",
          "product": "Mouse",
          "total": 500.00,
          "status": "SHIPPED"
        }
      ]
    }
  }
}

# Query หลายอย่างพร้อมกัน
query {
  user(id: 123) {
    name
  }
  recentOrders: orders(limit: 5) {
    id
    total
  }
  stats {
    totalUsers
    totalOrders
  }
}

# Query with Variables
query GetUser($userId: ID!) {
  user(id: $userId) {
    name
    email
  }
}
# Variables: { "userId": 123 }

# Query with Fragments (ใช้ซ้ำ)
fragment UserBasic on User {
  id
  name
  email
}

query {
  user(id: 123) {
    ...UserBasic
    orders { id }
  }
  admin: user(id: 1) {
    ...UserBasic
  }
}
```

### Mutation (แก้ไขข้อมูล)

```graphql
# สร้างผู้ใช้ใหม่
mutation {
  createUser(input: {
    name: "John Doe"
    email: "john@example.com"
    age: 30
  }) {
    id
    name
    email
  }
}

# Response
{
  "data": {
    "createUser": {
      "id": "124",
      "name": "John Doe",
      "email": "john@example.com"
    }
  }
}

# อัปเดตผู้ใช้
mutation {
  updateUser(id: 123, input: { name: "Jane Doe" }) {
    id
    name
  }
}

# ลบผู้ใช้
mutation {
  deleteUser(id: 123) {
    success
    message
  }
}
```

### Subscription (ข้อมูลเรียลไทม์)

```graphql
# ติดตาม event แบบ real-time ผ่าน WebSocket
subscription {
  orderStatusChanged(userId: 123) {
    id
    status
    updated_at
  }
}

# เมื่อ status เปลี่ยน Client จะได้รับอัตโนมัติ:
{
  "data": {
    "orderStatusChanged": {
      "id": "1",
      "status": "SHIPPED",
      "updated_at": "2024-01-15T12:00:00Z"
    }
  }
}
```

### Error Handling ใน GraphQL

```json
// GraphQL ตอบ HTTP 200 เสมอ แม้มี error
// ดู error จาก "errors" field
{
  "data": {
    "user": null
  },
  "errors": [
    {
      "message": "ไม่พบผู้ใช้ ID 999",
      "locations": [{ "line": 2, "column": 3 }],
      "path": ["user"],
      "extensions": {
        "code": "USER_NOT_FOUND",
        "http": { "status": 404 }
      }
    }
  ]
}

// Partial Success — บาง field สำเร็จ บาง field ล้มเหลว
{
  "data": {
    "user": {
      "name": "John",
      "orders": null
    }
  },
  "errors": [
    {
      "message": "ไม่สามารถดึงข้อมูลออเดอร์ได้",
      "path": ["user", "orders"]
    }
  ]
}
```

### N+1 Problem และ DataLoader

```graphql
# ปัญหา: query ผู้ใช้ 10 คน + ออเดอร์ของแต่ละคน
query {
  users(limit: 10) {
    name
    orders { id, total }
  }
}

# ❌ ไม่ใช้ DataLoader:
# Query 1: SELECT * FROM users LIMIT 10
# Query 2: SELECT * FROM orders WHERE user_id = 1
# Query 3: SELECT * FROM orders WHERE user_id = 2
# ... (รวม 11 queries!)

# ✅ ใช้ DataLoader:
# Query 1: SELECT * FROM users LIMIT 10
# Query 2: SELECT * FROM orders WHERE user_id IN (1,2,3,...,10)
# → รวมแค่ 2 queries!
```

### เครื่องมือ GraphQL

| เครื่องมือ | ใช้ทำอะไร |
|-----------|---------|
| **Apollo Server** | สร้าง GraphQL server (Node.js) |
| **Apollo Client** | GraphQL client สำหรับ React/Vue |
| **GraphQL Playground** | UI สำหรับทดสอบ query |
| **Hasura** | สร้าง GraphQL API จาก database อัตโนมัติ |
| **Relay** | GraphQL client จาก Facebook |
| **graphql-codegen** | สร้าง TypeScript types จาก schema |

### ข้อดี-ข้อเสีย

| ข้อดี | ข้อเสีย |
|-------|---------|
| ไม่มี over/under-fetching | ซับซ้อนกว่า REST |
| Endpoint เดียว | Caching ยากกว่า (POST ทั้งหมด) |
| Type system + self-documenting | N+1 query problem |
| Real-time ด้วย Subscription | File upload ไม่ง่าย |
| Introspection (ดู schema ได้) | HTTP caching ใช้ไม่ได้ |
| Frontend ไม่ต้องรอ Backend เพิ่ม endpoint | อาจถูก abuse (query ซับซ้อนเกินไป) |

---

## 5. gRPC APIs

### gRPC คืออะไร

gRPC (Google Remote Procedure Call) เป็น framework จาก Google สำหรับเรียกฟังก์ชันข้าม service เหมือนเรียกฟังก์ชันในเครื่องเดียวกัน ใช้ **Protocol Buffers (protobuf)** เป็นรูปแบบข้อมูล (binary format) เร็วกว่า JSON มาก

### Protocol Buffers (protobuf)

```protobuf
// user.proto — กำหนดโครงสร้างข้อมูลและ service
syntax = "proto3";

package user;

// ข้อความ (Message) = โครงสร้างข้อมูล
message User {
  int32 id = 1;           // field number (ไม่ใช่ค่า)
  string name = 2;
  string email = 3;
  int32 age = 4;
  repeated Order orders = 5;  // repeated = array
  UserStatus status = 6;
}

message Order {
  int32 id = 1;
  string product = 2;
  float total = 3;
}

// Enum
enum UserStatus {
  ACTIVE = 0;
  INACTIVE = 1;
  SUSPENDED = 2;
}

// Request/Response Messages
message GetUserRequest {
  int32 id = 1;
}

message CreateUserRequest {
  string name = 1;
  string email = 2;
  int32 age = 3;
}

message UserResponse {
  User user = 1;
}

message UserListResponse {
  repeated User users = 1;
  int32 total = 2;
}

message DeleteResponse {
  bool success = 1;
  string message = 2;
}
```

### Service Definition

```protobuf
// กำหนด RPC service
service UserService {
  // Unary RPC — 1 request → 1 response
  rpc GetUser (GetUserRequest) returns (UserResponse);
  rpc CreateUser (CreateUserRequest) returns (UserResponse);
  rpc DeleteUser (GetUserRequest) returns (DeleteResponse);

  // Server Streaming — 1 request → หลาย responses
  rpc ListUsers (ListUsersRequest) returns (stream UserResponse);

  // Client Streaming — หลาย requests → 1 response
  rpc UploadUsers (stream CreateUserRequest) returns (UserListResponse);

  // Bidirectional Streaming — หลาย requests ↔ หลาย responses
  rpc Chat (stream ChatMessage) returns (stream ChatMessage);
}
```

### 4 รูปแบบการสื่อสาร

```
1. Unary RPC (เหมือน HTTP ปกติ)
   Client ──[request]──→ Server
   Client ←─[response]── Server

2. Server Streaming (Server ส่งข้อมูลหลายชุด)
   Client ──[request]──→ Server
   Client ←─[response 1]── Server
   Client ←─[response 2]── Server
   Client ←─[response 3]── Server
   ใช้เมื่อ: ดึงข้อมูลจำนวนมาก, real-time feed

3. Client Streaming (Client ส่งข้อมูลหลายชุด)
   Client ──[request 1]──→ Server
   Client ──[request 2]──→ Server
   Client ──[request 3]──→ Server
   Client ←─[response]── Server
   ใช้เมื่อ: upload ไฟล์, batch insert

4. Bidirectional Streaming (ส่งสองทาง)
   Client ←→ Server (คุยกันอิสระ)
   ใช้เมื่อ: chat, multiplayer game
```

### ตัวอย่าง Code

```javascript
// Server (Node.js)
const grpc = require('@grpc/grpc-js')
const protoLoader = require('@grpc/proto-loader')

const packageDef = protoLoader.loadSync('user.proto')
const proto = grpc.loadPackageDefinition(packageDef)

const server = new grpc.Server()

server.addService(proto.user.UserService.service, {
  GetUser: (call, callback) => {
    const userId = call.request.id
    const user = { id: userId, name: 'John', email: 'john@example.com' }
    callback(null, { user })
  },

  ListUsers: (call) => {
    // Server streaming
    const users = [
      { id: 1, name: 'John' },
      { id: 2, name: 'Jane' },
      { id: 3, name: 'Bob' }
    ]
    users.forEach(user => call.write({ user }))
    call.end()
  }
})

server.bindAsync('0.0.0.0:50051', grpc.ServerCredentials.createInsecure(), () => {
  server.start()
})
```

```javascript
// Client (Node.js)
const client = new proto.user.UserService(
  'localhost:50051',
  grpc.credentials.createInsecure()
)

// Unary call
client.GetUser({ id: 123 }, (err, response) => {
  console.log(response.user)  // { id: 123, name: 'John', ... }
})

// Server streaming
const stream = client.ListUsers({})
stream.on('data', (response) => {
  console.log(response.user)
})
stream.on('end', () => {
  console.log('Stream ended')
})
```

### gRPC vs REST ขนาดข้อมูล

```
ตัวอย่าง: ส่งข้อมูลผู้ใช้ 1 คน

REST (JSON):
{"id":123,"name":"John Doe","email":"john@example.com","age":30}
→ ~65 bytes (text, อ่านง่าย)

gRPC (protobuf):
→ ~28 bytes (binary, อ่านไม่ได้ด้วยตา แต่เร็วกว่ามาก)

ส่ง 10,000 records:
REST  → ~650 KB
gRPC  → ~280 KB (เล็กกว่า 57%)
+ การ serialize/deserialize เร็วกว่า 5-10 เท่า
```

### gRPC-Web

ให้ browser เรียก gRPC ได้ผ่าน proxy

```
Browser → gRPC-Web Proxy (Envoy) → gRPC Server

เพราะ browser ไม่รองรับ HTTP/2 trailers โดยตรง
ต้องใช้ proxy แปลงก่อน
```

### ข้อดี-ข้อเสีย

| ข้อดี | ข้อเสีย |
|-------|---------|
| เร็วมาก (binary + HTTP/2) | อ่านไม่ได้ด้วยตา (binary) |
| Type-safe (protobuf schema) | ไม่รองรับ browser โดยตรง |
| Code generation ทุกภาษา | ต้องใช้ HTTP/2 |
| รองรับ 4 แบบ streaming | Debug ยากกว่า REST |
| Deadline/Timeout built-in | Tooling น้อยกว่า REST |
| ข้อมูลเล็กกว่า JSON ~50% | Learning curve สูง |

---

## 6. เปรียบเทียบทั้งหมด

### ตารางเปรียบเทียบ

| | REST | Simple JSON | SOAP | GraphQL | gRPC |
|---|---|---|---|---|---|
| **รูปแบบข้อมูล** | JSON/XML | JSON | XML | JSON | Protobuf (binary) |
| **Protocol** | HTTP | HTTP | HTTP/SMTP/TCP | HTTP | HTTP/2 |
| **ความเร็ว** | ดี | ดี | ช้า | ดี | เร็วมาก |
| **Type Safety** | ไม่มี | ไม่มี | WSDL | Schema | Proto |
| **Caching** | ง่าย | ยาก | ยาก | ยาก | ไม่มี built-in |
| **Learning Curve** | ต่ำ | ต่ำมาก | สูง | ปานกลาง | สูง |
| **Real-time** | ไม่มี | ไม่มี | ไม่มี | Subscription | Streaming |
| **Browser Support** | ดีมาก | ดีมาก | ต้องใช้ lib | ดี | ต้องใช้ proxy |
| **เอกสาร** | OpenAPI | ไม่มีมาตรฐาน | WSDL | Introspection | Proto files |

### แผนภาพเลือก API Style

```
เริ่มต้น: ต้องการสร้าง API

├─ Web/Mobile app ทั่วไป?
│  ├─ ข้อมูลซับซ้อน + หลาย client?  → GraphQL
│  └─ CRUD ปกติ?                     → REST
│
├─ Microservices คุยกัน?
│  ├─ ต้องการความเร็วสูง?            → gRPC
│  └─ ง่าย ทีมเล็ก?                  → REST
│
├─ Enterprise / ธนาคาร?
│  ├─ ต้องการมาตรฐานเข้มงวด?        → SOAP
│  └─ ระบบใหม่?                      → REST + OAuth
│
├─ Prototype / MVP?
│  └─ → Simple JSON API
│
└─ Real-time + Query flexibility?
   └─ → GraphQL (with Subscription)
```

### ใช้ร่วมกันได้

ในระบบจริง สามารถใช้หลาย API style ร่วมกันได้:

```
                    ┌──────────────┐
Mobile App ────→    │              │
                    │   GraphQL    │ ← Frontend Gateway
Web App ────→       │   Gateway    │
                    │              │
                    └──────┬───────┘
                           │
            ┌──────────────┼──────────────┐
            │              │              │
     ┌──────┴──────┐ ┌────┴────┐ ┌───────┴───────┐
     │ User Service│ │ Order   │ │ Payment       │
     │   (gRPC)    │ │ Service │ │ Service       │
     │             │ │ (gRPC)  │ │ (SOAP/REST)   │
     └─────────────┘ └─────────┘ └───────────────┘

- Frontend ↔ Gateway: GraphQL (ยืดหยุ่นสำหรับ client)
- Service ↔ Service: gRPC (เร็ว)
- Payment integration: SOAP/REST (ตาม third-party)
```

---

## 7. สรุป

| ถ้าคุณ... | ใช้ |
|-----------|-----|
| เริ่มเรียน API | **REST** — พื้นฐานที่ต้องรู้ |
| สร้าง prototype เร็วๆ | **Simple JSON** |
| ทำระบบที่ frontend ต้องการข้อมูลหลากหลาย | **GraphQL** |
| ทำ microservices ที่ต้องเร็ว | **gRPC** |
| ทำงานกับระบบ enterprise/ธนาคาร | **SOAP** |
| ไม่แน่ใจ | **REST** — เป็นค่าเริ่มต้นที่ดีที่สุด |
