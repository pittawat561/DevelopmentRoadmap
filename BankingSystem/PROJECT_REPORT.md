# Banking System - Project Report

## Overview

Banking System เป็น **RESTful Web API** สำหรับระบบธนาคาร พัฒนาด้วย **.NET 10.0 + ASP.NET Core** ใช้สถาปัตยกรรม **Clean Architecture** รองรับการจัดการบัญชี ธุรกรรมฝาก-ถอน-โอนเงิน ระบบยืนยันตัวตน (JWT) พร้อม **Redis** สำหรับ caching, distributed lock, rate limiting, token blacklist และ **SignalR** สำหรับ real-time notifications

---

## Tech Stack

| Category | Technology | Version |
|---|---|---|
| **Runtime** | .NET | 10.0 |
| **Web Framework** | ASP.NET Core (Controllers) | 10.0.5 |
| **Database** | PostgreSQL | localhost:5432 |
| **ORM** | Entity Framework Core (Npgsql) | 10.0.5 |
| **Cache / Lock / Rate Limit** | StackExchange.Redis | 2.12.8 |
| **Real-time** | ASP.NET Core SignalR + Redis Backplane | 10.0.5 |
| **Authentication** | JWT Bearer (HMAC SHA-256) | 10.0.5 |
| **Password Hashing** | BCrypt.Net-Next | 4.1.0 |
| **Validation** | FluentValidation | 12.1.1 |
| **Mediator** | MediatR (พร้อมสำหรับ CQRS) | 14.1.0 |
| **Logging** | Serilog (Console + File sinks) | 10.0.0 |
| **API Docs** | Swashbuckle / Swagger | 10.1.7 |
| **Unit Testing** | xUnit + FluentAssertions + Moq + Bogus | 2.9.3 / 8.9.0 / 4.20.72 / 35.6.5 |
| **Integration Testing** | xUnit + Respawn | 2.9.3 / 7.0.0 |

---

## Project Structure

```
BankingSystem/
├── Banking.Api/                     # Presentation Layer
│   ├── Controllers/
│   │   ├── AuthController.cs        # /api/auth - Register, Login, Profile, Logout
│   │   ├── AccountsController.cs    # /api/accounts - CRUD บัญชี + Balance Cache
│   │   ├── TransactionsController.cs# /api/transactions - ฝาก, ถอน, โอน
│   │   └── AdminController.cs       # /api/admin - Dashboard, Freeze, Unlock
│   ├── Hubs/
│   │   └── NotificationHub.cs       # SignalR Hub - real-time notifications
│   ├── Middleware/
│   │   ├── ExceptionMiddleware.cs   # Global error handling
│   │   ├── TokenBlacklistMiddleware.cs # ตรวจ JWT blacklist (Redis)
│   │   └── RateLimitMiddleware.cs   # จำกัด request rate (Redis)
│   └── Program.cs                   # DI, Auth, Redis, SignalR, Pipeline
│
├── Banking.Application/             # Application/Business Logic Layer
│   ├── DTOs/
│   │   ├── AuthDtos.cs              # Register/Login/Auth Response
│   │   └── TransactionDtos.cs       # Deposit/Withdraw/Transfer/Account Response
│   ├── Services/
│   │   ├── AuthService.cs           # สมัครสมาชิก, เข้าสู่ระบบ
│   │   ├── TransactionService.cs    # ฝาก, ถอน, โอน (ACID + Distributed Lock)
│   │   ├── IJwtService.cs           # JWT interface
│   │   ├── IRedisCacheService.cs    # Redis cache/lock/rate limit interface
│   │   ├── INotificationService.cs  # SignalR notification interface
│   │   ├── AccountNumberGenerator.cs
│   │   └── ReferenceNumberGenerator.cs
│   └── Validators/                  # FluentValidation rules
│       ├── RegisterRequestValidator.cs
│       ├── LoginRequestValidator.cs
│       ├── DepositRequestValidator.cs
│       ├── WithdrawRequestValidator.cs
│       └── TransferRequestValidator.cs
│
├── Banking.Domain/                  # Domain Layer (ไม่มี dependency ภายนอก)
│   ├── Entities/
│   │   ├── BaseEntity.cs            # Id, CreatedAt, UpdatedAt, IsDeleted
│   │   ├── User.cs                  # ผู้ใช้งาน
│   │   ├── Account.cs               # บัญชีธนาคาร
│   │   ├── Transaction.cs           # รายการธุรกรรม
│   │   ├── Transfer.cs              # การโอนเงิน
│   │   └── AuditLog.cs              # บันทึกการตรวจสอบ
│   ├── Enums/
│   │   └── Enums.cs                 # AccountType, TransactionType, etc.
│   ├── Exceptions/
│   │   └── DomainExceptions.cs      # Custom exceptions
│   └── Interfaces/
│       └── IRepositories.cs         # Repository + UnitOfWork contracts
│
├── Banking.Infrastructure/          # Infrastructure Layer
│   ├── Data/
│   │   └── AppDbContext.cs          # EF Core DbContext
│   ├── Configurations/              # Entity type configurations (Fluent API)
│   │   ├── UserConfiguration.cs
│   │   ├── AccountConfiguration.cs
│   │   ├── TransactionConfiguration.cs
│   │   ├── TransferConfiguration.cs
│   │   └── AuditLogConfiguration.cs
│   ├── Repositories/
│   │   ├── Repository.cs            # Generic CRUD
│   │   ├── UserRepository.cs
│   │   ├── AccountRepository.cs     # + GetByIdForUpdateAsync (Row Lock)
│   │   ├── TransactionRepository.cs # + GetTodayWithdrawalTotalAsync
│   │   └── UnitOfWork.cs            # DB Transaction management
│   ├── Services/
│   │   ├── JwtService.cs            # Token generation/validation
│   │   ├── RedisCacheService.cs     # Cache, Lock, Rate Limit, Token Blacklist
│   │   └── NotificationService.cs   # SignalR real-time notifications
│   ├── Seeds/
│   │   └── DataSeeder.cs            # Demo data (Admin + Demo user)
│   └── Migrations/                  # EF Core migrations
│
├── Banking.Tests.Unit/              # Unit Tests (xUnit + Moq + FluentAssertions + Bogus)
└── Banking.Tests.Integration/       # Integration Tests (xUnit + Respawn)
```

---

## Architecture

ใช้ **Clean Architecture** แบ่ง 4 layers ชัดเจน โดย dependency ไหลจากนอกเข้าใน

```
┌─────────────────────────────────────────────────┐
│          Banking.Api (Presentation)              │  Controllers, Hubs, Middleware
├─────────────────────────────────────────────────┤
│       Banking.Application (Business)             │  Services, DTOs, Validators
├─────────────────────────────────────────────────┤
│          Banking.Domain (Core)                   │  Entities, Enums, Interfaces (ไม่มี dependency)
├─────────────────────────────────────────────────┤
│     Banking.Infrastructure (Data Access)         │  EF Core, Redis, JWT, SignalR, Repositories
└─────────────────────────────────────────────────┘
```

### Design Patterns ที่ใช้

- **Repository Pattern** — แยก data access logic ออกจาก business logic
- **Unit of Work** — จัดการ database transaction ข้าม repositories (Begin/Commit/Rollback)
- **Generic Repository** — `Repository<T>` base class สำหรับ CRUD ทั่วไป
- **DTO Pattern** — แยก domain entities จาก API request/response (ใช้ C# records)
- **Soft Delete** — ใช้ `IsDeleted` flag แทนการลบจริง พร้อม Global Query Filters
- **Exception Middleware** — จัดการ error แบบ centralized
- **Distributed Locking** — Redis `SET NX` + Lua script ป้องกัน race conditions ข้าม API servers
- **Cache-Aside** — อ่านจาก Redis cache ก่อน → cache miss → query DB → เก็บ cache
- **Deadlock Prevention** — Transfer lock 2 บัญชีตามลำดับ ID (เรียงจากน้อยไปมาก)

---

## Domain Entities

### User (ผู้ใช้งาน)
| Field | Type | Description |
|---|---|---|
| Id | Guid | Primary Key |
| FirstName, LastName | string | ชื่อ-นามสกุล |
| Email | string | อีเมล (Unique) |
| Phone | string | เบอร์โทร (Unique, format: `0XXXXXXXXX`) |
| PasswordHash | string | รหัสผ่าน BCrypt |
| KycStatus | Enum | Pending, Verified, Rejected |
| IsLocked | bool | ล็อกบัญชี (5 ครั้งที่ login ผิด) |
| FailedLoginAttempts | int | จำนวนครั้งที่ login ผิด |

### Account (บัญชีธนาคาร)
| Field | Type | Description |
|---|---|---|
| AccountNumber | string | เลขบัญชี (format: `XXXX-XXXX-XXXX`) |
| Type | Enum | Savings, Checking, FixedDeposit |
| Currency | string | สกุลเงิน (default: THB) |
| Balance | decimal(18,2) | ยอดเงินคงเหลือ (ต้อง >= 0) |
| AvailableBalance | decimal(18,2) | ยอดเงินที่ใช้ได้จริง |
| DailyWithdrawalLimit | decimal | วงเงินถอน/วัน (default: 50,000) |
| Status | Enum | Active, Frozen, Closed |

### Transaction (ธุรกรรม)
| Field | Type | Description |
|---|---|---|
| ReferenceNumber | string | เลขอ้างอิง (`TXN-YYYYMMDD-XXXXXX`) |
| Type | Enum | Deposit, Withdrawal, TransferIn, TransferOut, Fee, Interest |
| Amount | decimal | จำนวนเงิน |
| BalanceBefore / BalanceAfter | decimal | snapshot ยอดเงินก่อน-หลัง |
| Status | Enum | Pending, Processing, Completed, Failed, Reversed |
| RelatedTransactionId | Guid? | เชื่อมธุรกรรมคู่ (โอนเข้า-โอนออก) |
| IpAddress | string? | IP ผู้ทำรายการ |

### Transfer (การโอนเงิน)
- เชื่อมบัญชีต้นทาง-ปลายทาง พร้อม Transaction คู่ (Debit + Credit)

### AuditLog (บันทึกตรวจสอบ)
- เก็บ Action, OldValues/NewValues (jsonb), UserId, IpAddress, UserAgent
- ใช้ `long` auto-increment ID (ไม่ใช่ Guid — เพื่อ performance)

---

## API Endpoints

### Authentication (`/api/auth`)

| Method | Endpoint | Description | Auth |
|---|---|---|---|
| POST | `/register` | สมัครสมาชิก + สร้างบัญชี Savings อัตโนมัติ | No |
| POST | `/login` | เข้าสู่ระบบ → JWT + Refresh Token | No |
| GET | `/profile` | ดูข้อมูลส่วนตัว | Yes |
| POST | `/logout` | ออกจากระบบ (Blacklist token ใน Redis) | Yes |

### Accounts (`/api/accounts`)

| Method | Endpoint | Description | Auth |
|---|---|---|---|
| GET | `/?userId=` | ดูบัญชีทั้งหมดของผู้ใช้ | No* |
| GET | `/{id}` | ดูรายละเอียดบัญชี | No* |
| GET | `/{id}/balance` | ดูยอดเงิน (Redis Cache-Aside) | No* |
| POST | `/` | เปิดบัญชีใหม่ | No* |

### Transactions (`/api/transactions`)

| Method | Endpoint | Description | Auth |
|---|---|---|---|
| POST | `/deposit` | ฝากเงิน (+ Distributed Lock + Cache Update + SignalR Notify) | No* |
| POST | `/withdraw` | ถอนเงิน (ตรวจยอด + วงเงิน/วัน + Lock) | No* |
| POST | `/transfer` | โอนเงินระหว่างบัญชี (Atomic + Lock ทั้ง 2 บัญชี) | No* |
| GET | `/?accountId=&page=&pageSize=` | ดูประวัติธุรกรรม (Pagination) | No* |

### Admin (`/api/admin`)

| Method | Endpoint | Description | Auth |
|---|---|---|---|
| GET | `/dashboard` | สถิติระบบ (Users, Accounts, Balance, Locked) | Yes |
| POST | `/accounts/{id}/freeze` | อายัดบัญชี | Yes |
| POST | `/accounts/{id}/unfreeze` | ปลดอายัดบัญชี | Yes |
| POST | `/users/{id}/unlock` | ปลดล็อกผู้ใช้ | Yes |

### SignalR Hub (`/hubs/notifications`)

| Event | Direction | Description |
|---|---|---|
| `BalanceUpdated` | Server → Client | แจ้งยอดเงินเปลี่ยน (accountId, balance, availableBalance) |
| `TransactionCompleted` | Server → Client | แจ้งธุรกรรมสำเร็จ (type, amount, referenceNumber) |
| `JoinAccountGroup` | Client → Server | subscribe รับ notification ของบัญชีเฉพาะ |
| `LeaveAccountGroup` | Client → Server | unsubscribe |

---

## การทำงานหลัก (Core Workflows)

### 1. สมัครสมาชิก (Register)

```
Client → POST /api/auth/register
  ├── FluentValidation ตรวจ input
  ├── ตรวจ email/phone ซ้ำ
  ├── Hash password ด้วย BCrypt
  ├── สร้าง User entity (KycStatus = Pending)
  ├── สร้าง Account (Savings, THB) อัตโนมัติ
  ├── Generate JWT Access Token (15 นาที)
  ├── Generate Refresh Token (7 วัน)
  └── Return AuthResponse
```

### 2. เข้าสู่ระบบ (Login)

```
Client → POST /api/auth/login
  ├── ค้นหา User จาก email
  ├── ตรวจสอบ IsLocked (ถ้าล็อก → 403)
  ├── Verify password ด้วย BCrypt
  │   ├── ผิด → เพิ่ม FailedLoginAttempts
  │   │         (ถึง 5 ครั้ง → IsLocked = true)
  │   └── ถูก → Reset FailedLoginAttempts = 0
  ├── อัพเดท LastLoginAt
  ├── Generate JWT + Refresh Token
  └── Return AuthResponse
```

### 3. ออกจากระบบ (Logout)

```
Client → POST /api/auth/logout
  ├── ดึง JWT จาก Authorization header
  ├── อ่าน JTI (JWT ID) + expiration
  ├── เก็บ JTI ลง Redis blacklist (TTL = เวลาที่เหลือก่อน token หมดอายุ)
  └── ทุก request หลังจากนี้ → TokenBlacklistMiddleware reject (401)
```

### 4. ฝากเงิน (Deposit)

```
Client → POST /api/transactions/deposit
  ├── FluentValidation ตรวจ input
  ├── Acquire Redis Distributed Lock (account:{id})
  ├── Begin Database Transaction
  ├── SELECT account FOR UPDATE (Row Lock)
  ├── ตรวจสอบ Account Status (Active?)
  ├── บันทึก BalanceBefore → เพิ่ม Balance + AvailableBalance
  ├── สร้าง Transaction (Type: Deposit, Status: Completed)
  ├── Commit Transaction
  ├── Update Redis Balance Cache
  ├── SignalR: Notify BalanceUpdated + TransactionCompleted
  ├── Release Redis Lock (Lua script)
  └── Return TransactionResponse
```

### 5. ถอนเงิน (Withdraw)

```
Client → POST /api/transactions/withdraw
  ├── FluentValidation ตรวจ input
  ├── Acquire Redis Distributed Lock (account:{id})
  ├── Begin Database Transaction
  ├── SELECT account FOR UPDATE (Row Lock)
  ├── ตรวจสอบ Account Status (Active?)
  ├── ตรวจ Balance >= Amount
  ├── คำนวณยอดถอนวันนี้ + ตรวจ DailyWithdrawalLimit
  ├── หัก Balance + AvailableBalance
  ├── สร้าง Transaction (Type: Withdrawal, Status: Completed)
  ├── Commit Transaction
  ├── Update Redis Balance Cache
  ├── SignalR: Notify BalanceUpdated + TransactionCompleted
  ├── Release Redis Lock (Lua script)
  └── Return TransactionResponse
```

### 6. โอนเงิน (Transfer) - Atomic Operation

```
Client → POST /api/transactions/transfer
  ├── FluentValidation ตรวจ input + FromAccountId ≠ ToAccountId
  ├── Acquire Redis Lock ตามลำดับ ID (ป้องกัน Deadlock)
  │   ├── Lock account ที่ ID น้อยกว่าก่อน
  │   └── Lock account ที่ ID มากกว่าทีหลัง
  ├── Begin Database Transaction
  ├── SELECT ทั้ง 2 บัญชี FOR UPDATE (Row Lock)
  ├── ตรวจทั้ง 2 บัญชี Active
  ├── ตรวจ Balance >= Amount + DailyWithdrawalLimit
  ├── หักเงินบัญชีต้นทาง + เพิ่มเงินบัญชีปลายทาง
  ├── สร้าง 2 Transactions:
  │   ├── TransferOut (บัญชีต้นทาง)
  │   └── TransferIn (บัญชีปลายทาง)
  │   └── เชื่อมกันด้วย RelatedTransactionId
  ├── สร้าง Transfer record
  ├── Commit Transaction
  ├── Update Redis Balance Cache ทั้ง 2 บัญชี
  ├── SignalR: Notify ทั้ง 2 users (BalanceUpdated + TransactionCompleted)
  ├── Release Redis Lock ทั้ง 2 (Lua script, ลำดับกลับ)
  └── Return TransactionResponse
```

---

## Redis Strategy

Redis ใช้ทำ 5 อย่าง:

| Feature | Redis Key Pattern | TTL | Description |
|---|---|---|---|
| **Balance Cache** | `banking:balance:{accountId}` | 5 นาที | Cache ยอดเงิน (Hash: balance + available) |
| **Distributed Lock** | `banking:lock:account:{accountId}` | 10 วินาที | ล็อคบัญชีระหว่างทำ transaction (SET NX) |
| **Rate Limiting** | `banking:ratelimit:{userId}:{endpoint}` | 60 วินาที | จำกัด 10 requests/นาที/user/endpoint (INCR) |
| **Token Blacklist** | `banking:blacklist:{jti}` | เท่า token TTL | Logout → blacklist JWT ID |
| **SignalR Backplane** | `banking-signalr:*` | - | Broadcast SignalR messages ข้าม API servers |

---

## Middleware Pipeline

ลำดับ middleware ใน Program.cs (สำคัญ — ห้ามสลับ):

```
1. ExceptionMiddleware          ← จับ error ก่อนทุกอย่าง
2. HttpsRedirection             ← HTTP → HTTPS
3. CORS (AllowFrontend)         ← อนุญาต cross-origin (ก่อน auth เพื่อให้ preflight ผ่าน)
4. Authentication               ← Decode JWT → ได้ userId
5. TokenBlacklistMiddleware     ← เช็ค Redis ว่า token ถูก revoke ไหม
6. RateLimitMiddleware          ← จำกัด request rate (ใช้ userId จาก step 4)
7. Authorization                ← ตรวจสิทธิ์ [Authorize]
8. MapControllers               ← Route → Controller
9. MapHub<NotificationHub>      ← SignalR endpoint: /hubs/notifications
```

---

## Security

### Authentication & Authorization
- **JWT Bearer Token** — Access Token หมดอายุ 15 นาที
- **Refresh Token** — หมดอายุ 7 วัน
- **Token Blacklist** — Logout → เก็บ JTI ใน Redis → ทุก request เช็ค blacklist
- **HMAC SHA-256** signing algorithm
- **ClockSkew = 0** — ไม่มี tolerance สำหรับ token หมดอายุ
- **SignalR Auth** — JWT ส่งผ่าน query string (WebSocket ไม่มี header)

### Password Protection
- **BCrypt** salted hashing (ไม่เก็บ plaintext)
- **Password Rules**: 8+ ตัวอักษร, ตัวพิมพ์ใหญ่, ตัวพิมพ์เล็ก, ตัวเลข

### Brute Force Protection
- ล็อกบัญชีหลัง login ผิด 5 ครั้งติดต่อกัน
- Admin ปลดล็อกผ่าน `/api/admin/users/{id}/unlock`

### Rate Limiting
- จำกัด 10 requests/นาที/user/endpoint (configurable)
- ข้าม Swagger + health check endpoints
- ใช้ Redis INCR + EXPIRE (Fixed Window Counter)
- เกิน limit → 429 Too Many Requests

### Data Integrity
- **Row-level locking** (`SELECT ... FOR UPDATE`) — ป้องกัน race conditions ระดับ DB
- **Distributed locking** (Redis SET NX) — ป้องกัน race conditions ข้าม API servers
- **Deadlock prevention** — Transfer lock ตามลำดับ account ID
- **Database Transaction** — ทุกการเงินเป็น atomic (Begin/Commit/Rollback)
- **Check Constraint** — `Balance >= 0` ป้องกันยอดติดลบ
- **Daily Withdrawal Limit** — จำกัดวงเงินถอน 50,000 THB/วัน

### Audit Trail
- ทุก action บันทึกลง AuditLog
- เก็บ OldValues/NewValues เป็น jsonb
- Track IpAddress, UserAgent, UserId

---

## Error Handling

Exception Middleware แปลง domain exceptions เป็น HTTP status codes:

| Exception | HTTP Status | Description |
|---|---|---|
| `NotFoundException` | 404 | ไม่พบข้อมูล |
| `InsufficientFundsException` | 400 | ยอดเงินไม่เพียงพอ |
| `DailyLimitExceededException` | 400 | เกินวงเงินถอน/วัน |
| `AccountFrozenException` | 403 | บัญชีถูกอายัด |
| `AccountLockedException` | 403 | บัญชีถูกล็อก |
| `DuplicateException` | 409 | ข้อมูลซ้ำ |
| `ArgumentException` | 400 | Input ไม่ถูกต้อง |
| `InvalidOperationException` | 400 | Account being processed (lock timeout) |
| Unhandled | 500 | Internal Server Error |

Response format มาตรฐาน: `{ success: bool, message: string, statusCode: int }`

---

## Database

- **Provider**: PostgreSQL (localhost:5432)
- **Databases**: `banking_db` (default), `banking_dev` (Development), `banking_debug` (Debug)
- **ORM**: Entity Framework Core 10.0.5 (Npgsql)
- **Migration**: Auto-migrate on startup (Debug/Development)
- **Retry Policy**: 3 attempts สำหรับ transient errors
- **Command Timeout**: 30 วินาที
- **Soft Delete**: Global Query Filters กรอง `IsDeleted = true` อัตโนมัติ
- **Unique Indexes**: Email, Phone, AccountNumber, ReferenceNumber
- **Decimal Precision**: `decimal(18,2)` สำหรับทุกค่าเงิน
- **Timestamps**: UTC (`DateTime.UtcNow`) ทุก field

---

## Environment Profiles

| Profile | Database | Swagger | Log Level | Use Case |
|---|---|---|---|---|
| Debug | banking_debug | Enabled | Debug | Debugging with detailed EF logs |
| Development | banking_dev | Enabled | Information | Standard development |
| UAT | banking_db | Disabled | Warning | User Acceptance Testing |
| Production | banking_db | Disabled | Warning | Production (HTTPS only) |

---

## Configuration (appsettings.json)

```
ConnectionStrings:
  DefaultConnection  → PostgreSQL connection
  Redis              → Redis connection (localhost:6379)

Jwt:
  Key, Issuer, Audience
  AccessTokenExpirationMinutes: 15
  RefreshTokenExpirationDays: 7

Redis:
  InstanceName: "banking:"          → Key prefix
  BalanceCacheTtlMinutes: 5         → Balance cache TTL
  LockTimeoutSeconds: 10            → Distributed lock TTL
  RateLimitWindowSeconds: 60        → Rate limit window
  RateLimitMaxRequests: 10          → Max requests per window
  TokenBlacklistTtlMinutes: 20      → Blacklisted token TTL

Frontend:
  Url: "http://localhost:3000"      → CORS origin (Next.js)

Swagger:
  Enabled: true/false
```

---

## Phase Roadmap

| Phase | Status | Features |
|---|---|---|
| **Phase 1** | Done | Domain entities, EF Core, PostgreSQL, Migrations, Seed Data |
| **Phase 2** | Done | Repository + UnitOfWork, Services, JWT Auth, Controllers, FluentValidation, ExceptionMiddleware |
| **Phase 3** | Done | Redis (Cache, Distributed Lock, Rate Limiting, Token Blacklist), SignalR (Real-time notifications), CORS |
| **Phase 4** | Planned | Next.js 15 Frontend (App Router, shadcn/ui, React Query, Zustand, SignalR client) |
| **Phase 5** | Planned | Load Balancing, Docker, Nginx, DB Read Replicas, CI/CD |
| **Phase 6** | Planned | CI/CD Pipeline (GitHub Actions, Docker Build, Deploy) |
| **Phase 7** | Planned | Cloud Deploy (Vercel + Railway + Neon/Supabase + Upstash Redis) |
