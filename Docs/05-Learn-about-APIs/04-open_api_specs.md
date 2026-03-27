# OpenAPI Specification (Swagger)

> มาตรฐานสำหรับอธิบาย RESTful API — ทำให้คน เครื่องมือ และ code generator เข้าใจ API ตรงกัน

---

## 1. OpenAPI คืออะไร

OpenAPI Specification (เดิมชื่อ Swagger Specification) คือมาตรฐาน **ภาษากลาง** สำหรับอธิบาย REST API ในรูปแบบ YAML หรือ JSON

```
ประโยชน์:
- สร้างเอกสาร API อัตโนมัติ (Swagger UI)
- สร้าง client/server code อัตโนมัติ (Code Generation)
- ทดสอบ API ได้จากเอกสาร
- ทุกคนในทีมเข้าใจ API ตรงกัน
- เป็นสัญญา (Contract) ระหว่าง Frontend กับ Backend
```

---

## 2. โครงสร้าง OpenAPI Document

```yaml
# openapi.yaml — โครงสร้างหลัก
openapi: 3.0.3                    # เวอร์ชัน spec

info:                              # ข้อมูลทั่วไปของ API
  title: My API
  version: 1.0.0

servers:                           # URL ของ API
  - url: https://api.example.com

paths:                             # Endpoints ทั้งหมด
  /users:
    get: ...
    post: ...

components:                        # ส่วนประกอบที่ใช้ซ้ำ
  schemas: ...                     # โครงสร้างข้อมูล
  securitySchemes: ...             # วิธียืนยันตัวตน
  parameters: ...                  # Parameters ที่ใช้ซ้ำ
  responses: ...                   # Responses ที่ใช้ซ้ำ

security:                          # การยืนยันตัวตนทั้ง API
  - bearerAuth: []

tags:                              # จัดกลุ่ม endpoints
  - name: Users
  - name: Orders
```

---

## 3. Info & Servers

```yaml
info:
  title: E-Commerce API
  description: |
    API สำหรับระบบ E-Commerce

    ## Authentication
    ใช้ Bearer Token ในทุก request
  version: 2.1.0
  contact:
    name: API Team
    email: api@example.com
    url: https://example.com/support
  license:
    name: MIT
    url: https://opensource.org/licenses/MIT
  termsOfService: https://example.com/terms

servers:
  - url: https://api.example.com/v2
    description: Production
  - url: https://staging-api.example.com/v2
    description: Staging
  - url: http://localhost:3000/v2
    description: Development
```

---

## 4. Paths (Endpoints)

### GET — ดึงข้อมูล

```yaml
paths:
  /users:
    get:
      tags:
        - Users
      summary: ดึงรายการผู้ใช้
      description: ดึงรายการผู้ใช้ทั้งหมด รองรับ pagination และ filtering
      operationId: getUsers
      parameters:
        - name: page
          in: query
          description: หมายเลขหน้า
          required: false
          schema:
            type: integer
            default: 1
            minimum: 1
        - name: limit
          in: query
          description: จำนวนต่อหน้า
          required: false
          schema:
            type: integer
            default: 20
            minimum: 1
            maximum: 100
        - name: role
          in: query
          description: กรองตามบทบาท
          schema:
            type: string
            enum: [admin, user, moderator]
        - name: sort
          in: query
          description: เรียงตามฟิลด์
          schema:
            type: string
            enum: [name, created_at, email]
      responses:
        '200':
          description: สำเร็จ
          content:
            application/json:
              schema:
                type: object
                properties:
                  data:
                    type: array
                    items:
                      $ref: '#/components/schemas/User'
                  pagination:
                    $ref: '#/components/schemas/Pagination'
              example:
                data:
                  - id: 1
                    name: "John Doe"
                    email: "john@example.com"
                    role: "admin"
                pagination:
                  page: 1
                  limit: 20
                  total: 150
        '401':
          $ref: '#/components/responses/Unauthorized'
        '500':
          $ref: '#/components/responses/InternalError'
```

### GET by ID — ดึงข้อมูลเดี่ยว

```yaml
  /users/{id}:
    get:
      tags:
        - Users
      summary: ดึงผู้ใช้ตาม ID
      operationId: getUserById
      parameters:
        - name: id
          in: path
          required: true
          description: ID ของผู้ใช้
          schema:
            type: integer
            example: 123
      responses:
        '200':
          description: สำเร็จ
          content:
            application/json:
              schema:
                $ref: '#/components/schemas/User'
        '404':
          $ref: '#/components/responses/NotFound'
```

### POST — สร้างข้อมูล

```yaml
  /users:
    post:
      tags:
        - Users
      summary: สร้างผู้ใช้ใหม่
      operationId: createUser
      requestBody:
        required: true
        content:
          application/json:
            schema:
              $ref: '#/components/schemas/CreateUserRequest'
            example:
              name: "John Doe"
              email: "john@example.com"
              password: "securePassword123"
              role: "user"
      responses:
        '201':
          description: สร้างสำเร็จ
          content:
            application/json:
              schema:
                $ref: '#/components/schemas/User'
          headers:
            Location:
              description: URL ของ resource ที่สร้าง
              schema:
                type: string
                example: /api/users/124
        '400':
          $ref: '#/components/responses/BadRequest'
        '409':
          description: อีเมลซ้ำ
          content:
            application/json:
              schema:
                $ref: '#/components/schemas/Error'
```

### PUT — อัปเดตข้อมูล

```yaml
    put:
      tags:
        - Users
      summary: อัปเดตผู้ใช้
      operationId: updateUser
      parameters:
        - name: id
          in: path
          required: true
          schema:
            type: integer
      requestBody:
        required: true
        content:
          application/json:
            schema:
              $ref: '#/components/schemas/UpdateUserRequest'
      responses:
        '200':
          description: อัปเดตสำเร็จ
          content:
            application/json:
              schema:
                $ref: '#/components/schemas/User'
        '404':
          $ref: '#/components/responses/NotFound'
```

### DELETE — ลบข้อมูล

```yaml
    delete:
      tags:
        - Users
      summary: ลบผู้ใช้
      operationId: deleteUser
      parameters:
        - name: id
          in: path
          required: true
          schema:
            type: integer
      responses:
        '204':
          description: ลบสำเร็จ (ไม่มี body)
        '404':
          $ref: '#/components/responses/NotFound'
```

---

## 5. Components (ส่วนประกอบที่ใช้ซ้ำ)

### Schemas (โครงสร้างข้อมูล)

```yaml
components:
  schemas:
    # User Model
    User:
      type: object
      required:
        - id
        - name
        - email
      properties:
        id:
          type: integer
          readOnly: true
          description: ID ผู้ใช้ (สร้างอัตโนมัติ)
          example: 123
        name:
          type: string
          minLength: 2
          maxLength: 100
          description: ชื่อผู้ใช้
          example: "John Doe"
        email:
          type: string
          format: email
          description: อีเมล
          example: "john@example.com"
        role:
          type: string
          enum: [admin, user, moderator]
          default: user
        avatar:
          type: string
          format: uri
          nullable: true
        created_at:
          type: string
          format: date-time
          readOnly: true

    # Request Bodies
    CreateUserRequest:
      type: object
      required:
        - name
        - email
        - password
      properties:
        name:
          type: string
          minLength: 2
          maxLength: 100
        email:
          type: string
          format: email
        password:
          type: string
          format: password
          minLength: 8
        role:
          type: string
          enum: [admin, user, moderator]
          default: user

    UpdateUserRequest:
      type: object
      properties:
        name:
          type: string
        email:
          type: string
          format: email

    # Pagination
    Pagination:
      type: object
      properties:
        page:
          type: integer
          example: 1
        limit:
          type: integer
          example: 20
        total:
          type: integer
          example: 150
        total_pages:
          type: integer
          example: 8

    # Error
    Error:
      type: object
      required:
        - code
        - message
      properties:
        code:
          type: string
          example: "VALIDATION_ERROR"
        message:
          type: string
          example: "ข้อมูลไม่ถูกต้อง"
        details:
          type: array
          items:
            type: object
            properties:
              field:
                type: string
              message:
                type: string
```

### Security Schemes

```yaml
  securitySchemes:
    # Bearer Token (JWT)
    bearerAuth:
      type: http
      scheme: bearer
      bearerFormat: JWT
      description: |
        ส่ง JWT token ใน Authorization header
        `Authorization: Bearer <token>`

    # API Key
    apiKeyAuth:
      type: apiKey
      in: header
      name: X-API-Key
      description: API Key สำหรับระบุ application

    # OAuth 2.0
    oAuth2:
      type: oauth2
      flows:
        authorizationCode:
          authorizationUrl: https://auth.example.com/authorize
          tokenUrl: https://auth.example.com/token
          scopes:
            read:users: อ่านข้อมูลผู้ใช้
            write:users: แก้ไขข้อมูลผู้ใช้
            admin: สิทธิ์ admin

    # Basic Auth
    basicAuth:
      type: http
      scheme: basic

# ใช้ security กับทั้ง API
security:
  - bearerAuth: []

# หรือใช้กับ endpoint เฉพาะ
paths:
  /admin/users:
    get:
      security:
        - bearerAuth: []
        - oAuth2: [admin]
```

### Reusable Responses

```yaml
  responses:
    BadRequest:
      description: คำขอไม่ถูกต้อง
      content:
        application/json:
          schema:
            $ref: '#/components/schemas/Error'
          example:
            code: "BAD_REQUEST"
            message: "ข้อมูลไม่ถูกต้อง"

    Unauthorized:
      description: ไม่ได้ยืนยันตัวตน
      content:
        application/json:
          schema:
            $ref: '#/components/schemas/Error'
          example:
            code: "UNAUTHORIZED"
            message: "กรุณา login ก่อน"

    NotFound:
      description: ไม่พบข้อมูล
      content:
        application/json:
          schema:
            $ref: '#/components/schemas/Error'
          example:
            code: "NOT_FOUND"
            message: "ไม่พบข้อมูลที่ต้องการ"

    InternalError:
      description: เซิร์ฟเวอร์ผิดพลาด
      content:
        application/json:
          schema:
            $ref: '#/components/schemas/Error'
          example:
            code: "INTERNAL_ERROR"
            message: "เกิดข้อผิดพลาดภายในระบบ"
```

---

## 6. Data Types & Validation

```yaml
# ประเภทข้อมูลที่รองรับ
properties:
  # String
  name:
    type: string
    minLength: 2
    maxLength: 100
    pattern: '^[a-zA-Z\s]+$'     # regex

  email:
    type: string
    format: email                  # format ช่วย validate

  website:
    type: string
    format: uri

  # Number
  age:
    type: integer
    minimum: 0
    maximum: 150

  price:
    type: number
    format: float
    minimum: 0
    exclusiveMinimum: true        # > 0 (ไม่ใช่ >= 0)

  # Boolean
  is_active:
    type: boolean
    default: true

  # Date/Time
  created_at:
    type: string
    format: date-time              # 2024-01-15T10:30:00Z

  birthday:
    type: string
    format: date                   # 2024-01-15

  # Array
  tags:
    type: array
    items:
      type: string
    minItems: 1
    maxItems: 10
    uniqueItems: true

  # Object
  address:
    type: object
    properties:
      street:
        type: string
      city:
        type: string
      country:
        type: string

  # Enum
  status:
    type: string
    enum: [active, inactive, suspended]

  # Nullable
  avatar:
    type: string
    nullable: true

  # oneOf / anyOf / allOf (composition)
  pet:
    oneOf:                         # ต้องเป็นอันใดอันหนึ่ง
      - $ref: '#/components/schemas/Cat'
      - $ref: '#/components/schemas/Dog'

  employee:
    allOf:                         # รวมทุก schema
      - $ref: '#/components/schemas/Person'
      - type: object
        properties:
          employee_id:
            type: string
```

---

## 7. เครื่องมือ OpenAPI

### Swagger UI
```
แสดงเอกสาร API แบบ interactive
- ดู endpoints ทั้งหมด
- ทดสอบ API ได้จากหน้าเว็บ
- ใส่ auth token แล้วเรียก API ได้เลย

ติดตั้ง: npm install swagger-ui-express
```

### Swagger Editor
```
เขียน OpenAPI spec ออนไลน์
- URL: https://editor.swagger.io
- เขียน YAML ด้านซ้าย เห็นผลด้านขวา
- ตรวจสอบ syntax อัตโนมัติ
```

### Code Generation
```bash
# สร้าง client code จาก spec
npx openapi-generator-cli generate \
  -i openapi.yaml \
  -g typescript-axios \
  -o ./generated-client

# สร้าง server stub
npx openapi-generator-cli generate \
  -i openapi.yaml \
  -g nodejs-express-server \
  -o ./generated-server

# ภาษาที่รองรับ: TypeScript, Java, Python, Go, C#, Ruby, PHP, ...
```

### API-First vs Code-First

```
API-First (แนะนำ):
1. เขียน OpenAPI spec ก่อน
2. ทีม review spec
3. Frontend + Backend เริ่มทำงานพร้อมกัน
4. Frontend ใช้ mock จาก spec
5. Backend implement ตาม spec

Code-First:
1. Backend เขียน code ก่อน
2. สร้าง spec จาก code (annotations/decorators)
3. Frontend รอ Backend เสร็จ

// Code-First ตัวอย่าง (Express + swagger-jsdoc)
/**
 * @openapi
 * /users:
 *   get:
 *     summary: Get all users
 *     responses:
 *       200:
 *         description: Success
 */
app.get('/users', (req, res) => { ... })
```

---

## 8. ตัวอย่างเต็ม (Complete Example)

```yaml
openapi: 3.0.3
info:
  title: Todo API
  version: 1.0.0
  description: API สำหรับจัดการ Todo List

servers:
  - url: http://localhost:3000/api

tags:
  - name: Todos
    description: จัดการรายการ Todo

paths:
  /todos:
    get:
      tags: [Todos]
      summary: ดึง Todo ทั้งหมด
      parameters:
        - name: completed
          in: query
          schema:
            type: boolean
      responses:
        '200':
          description: สำเร็จ
          content:
            application/json:
              schema:
                type: array
                items:
                  $ref: '#/components/schemas/Todo'
    post:
      tags: [Todos]
      summary: สร้าง Todo ใหม่
      requestBody:
        required: true
        content:
          application/json:
            schema:
              $ref: '#/components/schemas/CreateTodo'
      responses:
        '201':
          description: สร้างสำเร็จ
          content:
            application/json:
              schema:
                $ref: '#/components/schemas/Todo'

  /todos/{id}:
    put:
      tags: [Todos]
      summary: อัปเดต Todo
      parameters:
        - name: id
          in: path
          required: true
          schema:
            type: integer
      requestBody:
        required: true
        content:
          application/json:
            schema:
              $ref: '#/components/schemas/UpdateTodo'
      responses:
        '200':
          description: อัปเดตสำเร็จ
          content:
            application/json:
              schema:
                $ref: '#/components/schemas/Todo'
    delete:
      tags: [Todos]
      summary: ลบ Todo
      parameters:
        - name: id
          in: path
          required: true
          schema:
            type: integer
      responses:
        '204':
          description: ลบสำเร็จ

components:
  schemas:
    Todo:
      type: object
      properties:
        id:
          type: integer
          example: 1
        title:
          type: string
          example: "ซื้อของ"
        completed:
          type: boolean
          example: false
        created_at:
          type: string
          format: date-time

    CreateTodo:
      type: object
      required: [title]
      properties:
        title:
          type: string
          minLength: 1
          maxLength: 200

    UpdateTodo:
      type: object
      properties:
        title:
          type: string
        completed:
          type: boolean
```
