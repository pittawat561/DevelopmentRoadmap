# Banking System - Developer Guide

> คู่มือฉบับสมบูรณ์สำหรับนักพัฒนาที่ต้องการทำความเข้าใจและพัฒนาต่อยอดระบบ Banking System

---

## สารบัญ

1. [ภาพรวมโปรเจค (Project Overview)](#1-ภาพรวมโปรเจค)
2. [Tech Stack และเหตุผลที่เลือกใช้](#2-tech-stack-และเหตุผลที่เลือกใช้)
3. [สถาปัตยกรรม (Architecture)](#3-สถาปัตยกรรม)
4. [โครงสร้างโปรเจค (Project Structure)](#4-โครงสร้างโปรเจค)
5. [Domain Layer](#5-domain-layer)
6. [Application Layer](#6-application-layer)
7. [Infrastructure Layer](#7-infrastructure-layer)
8. [API Layer](#8-api-layer)
9. [Middleware Pipeline](#9-middleware-pipeline)
10. [Real-time Communication (SignalR)](#10-real-time-communication-signalr)
11. [Caching & Distributed Lock (Redis)](#11-caching--distributed-lock-redis)
12. [Docker & Infrastructure](#12-docker--infrastructure)
13. [CI/CD Pipeline](#13-cicd-pipeline)
14. [Frontend (Next.js)](#14-frontend-nextjs)
15. [Testing](#15-testing)
16. [Security Features](#16-security-features)
17. [การ Setup โปรเจค](#17-การ-setup-โปรเจค)

---

## 1. ภาพรวมโปรเจค

Banking System เป็นระบบธนาคารจำลองแบบ Full-Stack ที่พัฒนาด้วย **ASP.NET Core 10** (Backend) และ **Next.js** (Frontend) ออกแบบตามหลัก **Clean Architecture** พร้อมระบบ security ระดับ production-grade

### ความสามารถหลัก

- **Authentication** — สมัครสมาชิก, เข้าสู่ระบบ, JWT + Refresh Token, Token Blacklist (Logout)
- **Account Management** — สร้างบัญชี, ดูข้อมูลบัญชี, ดูยอดเงิน (Cache-First)
- **Transactions** — ฝากเงิน, ถอนเงิน, โอนเงิน พร้อม PIN verification
- **PIN System** — ตั้ง PIN, เปลี่ยน PIN, ล็อกธุรกรรมเมื่อ PIN ผิดเกิน 3 ครั้ง
- **Admin Dashboard** — สถิติระบบ, อายัด/ปลดอายัดบัญชี, ปลดล็อก user
- **Real-time Notification** — แจ้งเตือนยอดเงินและธุรกรรมผ่าน SignalR WebSocket
- **Fraud Detection** — ตรวจจับธุรกรรมผิดปกติแบบ Rule-based
- **Audit Logging** — บันทึกทุก action ลง database
- **Rate Limiting** — จำกัด request ทั้งระดับ Nginx และ Application
- **Monitoring** — Prometheus metrics + Grafana dashboard

---

## 2. Tech Stack และเหตุผลที่เลือกใช้

### Backend

| เทคโนโลยี | เวอร์ชัน | เหตุผลที่เลือกใช้ |
|---|---|---|
| **ASP.NET Core** | 10.0 | High performance, cross-platform, ระบบ middleware ยืดหยุ่น, รองรับ DI built-in |
| **Entity Framework Core** | 10.0 | ORM ที่รองรับ LINQ, migration, database-first/code-first, lazy/eager loading |
| **PostgreSQL** | 16 | Open-source, รองรับ JSONB, full-text search, replication, performance ดีกว่า MySQL สำหรับ complex queries |
| **Redis** | 7 | In-memory data store สำหรับ caching, distributed lock, rate limiting, pub/sub, token blacklist |
| **SignalR** | 10.0 | Real-time bidirectional communication สำหรับ WebSocket + fallback transports |
| **FluentValidation** | 11.3 | Validation library ที่แยก validation logic ออกจาก model, อ่านง่าย, test ง่าย |
| **BCrypt.NET** | 4.1 | Password hashing ที่ปลอดภัย ปรับ work factor ได้ ป้องกัน rainbow table attack |
| **Serilog** | 10.0 | Structured logging รองรับหลาย sinks (Console, File, Seq, etc.) |
| **Prometheus.NET** | 8.2 | Expose metrics endpoint สำหรับ monitoring |
| **Swagger/OpenAPI** | 10.1 | Auto-generate API documentation + testing UI |

### Frontend

| เทคโนโลยี | เหตุผลที่เลือกใช้ |
|---|---|
| **Next.js** (App Router) | SSR/SSG, file-based routing, middleware, optimized performance |
| **TypeScript** | Type safety, better DX, catch errors at compile time |
| **Tailwind CSS** | Utility-first CSS, rapid UI development, no CSS file management |
| **shadcn/ui** | Accessible, customizable component library built on Radix UI |
| **TanStack Query** | Server state management, caching, auto-refetch, optimistic updates |
| **Zustand** | Lightweight client state management (auth store) |
| **Zod** | Schema validation ที่ทำงานร่วมกับ TypeScript types ได้ดี |

### Infrastructure

| เทคโนโลยี | เหตุผลที่เลือกใช้ |
|---|---|
| **Docker** + **Docker Compose** | Containerization, reproducible environment, orchestration |
| **Nginx** | Reverse proxy, load balancer, rate limiting (Layer 1), security headers |
| **Prometheus** | Metrics collection, alerting |
| **Grafana** | Dashboard visualization สำหรับ monitoring |
| **GitHub Actions** | CI/CD automation, integrated กับ GitHub |
| **Railway** | Backend deployment platform (Staging) |
| **Vercel** | Frontend deployment platform |

---

## 3. สถาปัตยกรรม

### Clean Architecture (4 Layers)

```
┌─────────────────────────────────────────────┐
│                  API Layer                   │  ← Controllers, Middleware, Program.cs
│            (Banking.Api)                     │
├─────────────────────────────────────────────┤
│             Application Layer               │  ← Services, DTOs, Validators, Interfaces
│         (Banking.Application)               │
├─────────────────────────────────────────────┤
│             Infrastructure Layer            │  ← EF Core, Repositories, Redis, SignalR, JWT
│         (Banking.Infrastructure)            │
├─────────────────────────────────────────────┤
│               Domain Layer                  │  ← Entities, Enums, Exceptions, Interfaces
│           (Banking.Domain)                  │
└─────────────────────────────────────────────┘
```

**ทิศทางการ Depend:**
- API → Application, Infrastructure
- Application → Domain
- Infrastructure → Domain, Application
- Domain → ไม่ depend ใคร (innermost layer)

**หลักการ:**
- **Domain** เป็นแกนกลาง ไม่รู้จัก framework ใดๆ
- **Application** มี business logic + interface definitions
- **Infrastructure** implement interface ที่ Application กำหนด (Dependency Inversion)
- **API** เป็น entry point, wire ทุกอย่างเข้าด้วยกันผ่าน DI

### Request Flow

```
Client Request
    │
    ▼
[Nginx Load Balancer]        ← Rate limit (Layer 1), Security headers
    │
    ▼
[ASP.NET Core Pipeline]
    │
    ├── ExceptionMiddleware   ← จับ error ทุกประเภท
    ├── ForwardedHeaders      ← อ่าน X-Forwarded-For จาก Nginx
    ├── HTTPS Redirect
    ├── CORS
    ├── Authentication        ← ตรวจ JWT
    ├── TokenBlacklist        ← เช็ค token ถูก revoke ไหม
    ├── RateLimit             ← Rate limit (Layer 2, Redis)
    ├── Idempotency           ← ป้องกัน duplicate request
    ├── AdminIpWhitelist      ← IP whitelist สำหรับ admin endpoints
    ├── Authorization         ← ตรวจ [Authorize] attribute
    ├── AuditMiddleware       ← บันทึก audit log
    │
    ▼
[Controller]                  ← Validate input, เรียก Service
    │
    ▼
[Service]                     ← Business logic, PIN verify, Lock
    │
    ▼
[Repository / UnitOfWork]     ← Database operations
    │
    ▼
[PostgreSQL]                  ← Data persistence
```

### Database Architecture (Read/Write Splitting)

```
                    ┌──────────────┐
                    │   API Server │
                    └──────┬───────┘
                           │
              ┌────────────┼────────────┐
              ▼                         ▼
    ┌─────────────────┐      ┌─────────────────┐
    │ PostgreSQL       │      │ PostgreSQL       │
    │ Primary (Write)  │ ───► │ Replica (Read)   │
    │ Port 5432        │      │ Port 5433        │
    └─────────────────┘      └─────────────────┘
```

- **AppDbContext** → เชื่อมต่อ Primary (Read + Write)
- **ReadOnlyDbContext** → เชื่อมต่อ Replica (Read Only), NoTracking

---

## 4. โครงสร้างโปรเจค

```
BankingSystem/
├── BankingSystem.slnx                          # Solution file
│
├── Banking.Domain/                              # Domain Layer
│   ├── Entities/
│   │   ├── BaseEntity.cs                        # Base class (Id, CreatedAt, UpdatedAt, IsDeleted)
│   │   ├── User.cs                              # ผู้ใช้
│   │   ├── Account.cs                           # บัญชีธนาคาร
│   │   ├── Transaction.cs                       # ธุรกรรม
│   │   ├── Transfer.cs                          # การโอนเงิน
│   │   └── AuditLog.cs                          # Audit log
│   ├── Enums/
│   │   └── Enums.cs                             # AccountType, TransactionType, etc.
│   ├── Exceptions/
│   │   └── DomainExceptions.cs                  # Custom exceptions
│   └── Interfaces/
│       └── IRepositories.cs                     # Repository + UnitOfWork interfaces
│
├── Banking.Application/                         # Application Layer
│   ├── DTOs/
│   │   ├── AuthDtos.cs                          # Register, Login, Profile DTOs
│   │   ├── TransactionDtos.cs                   # Transaction, Account, Paged DTOs
│   │   └── PinDtos.cs                           # SetPin, ChangePin DTOs
│   ├── Services/
│   │   ├── AuthService.cs                       # Authentication business logic
│   │   ├── TransactionService.cs                # Transaction business logic
│   │   ├── PinService.cs                        # PIN management
│   │   ├── DataMasking.cs                       # Data masking utility
│   │   ├── AccountNumberGenerator.cs            # สร้างเลขบัญชี
│   │   ├── ReferenceNumberGenerator.cs          # สร้างเลข reference ธุรกรรม
│   │   ├── IJwtService.cs                       # Interface: JWT
│   │   ├── INotificationService.cs              # Interface: Notification
│   │   ├── IRedisCacheService.cs                # Interface: Redis Cache
│   │   ├── IAuditService.cs                     # Interface: Audit
│   │   └── IFraudDetectionService.cs            # Interface: Fraud Detection
│   └── Validators/
│       ├── RegisterRequestValidator.cs
│       ├── LoginRequestValidator.cs
│       ├── DepositRequestValidator.cs
│       ├── WithdrawRequestValidator.cs
│       ├── TransferRequestValidator.cs
│       └── PinValidators.cs
│
├── Banking.Infrastructure/                      # Infrastructure Layer
│   ├── Data/
│   │   ├── AppDbContext.cs                      # EF Core DbContext (Write)
│   │   └── ReadOnlyDbContext.cs                 # Read-only DbContext (Replica)
│   ├── Repositories/
│   │   ├── Repository.cs                        # Generic repository
│   │   ├── UserRepository.cs
│   │   ├── AccountRepository.cs
│   │   ├── TransactionRepository.cs
│   │   └── UnitOfWork.cs                        # Unit of Work pattern
│   ├── Services/
│   │   ├── JwtService.cs                        # JWT token generation
│   │   ├── RedisCacheService.cs                 # Redis operations
│   │   ├── NotificationService.cs               # SignalR notifications
│   │   ├── AuditService.cs                      # Audit logging
│   │   └── FraudDetectionService.cs             # Fraud detection
│   ├── Hubs/
│   │   └── NotificationHub.cs                   # SignalR Hub
│   ├── Configurations/                          # EF Core entity configurations
│   ├── Migrations/                              # Database migrations
│   └── Seeds/
│       └── DataSeeder.cs                        # Demo data seeder
│
├── Banking.Api/                                 # API Layer
│   ├── Program.cs                               # Application entry point + DI setup
│   ├── Controllers/
│   │   ├── AuthController.cs                    # Auth endpoints
│   │   ├── AccountsController.cs                # Account endpoints
│   │   ├── TransactionsController.cs            # Transaction endpoints
│   │   └── AdminController.cs                   # Admin endpoints
│   ├── Middleware/
│   │   ├── ExceptionMiddleware.cs               # Global error handler
│   │   ├── RateLimitMiddleware.cs               # Rate limiting (Redis)
│   │   ├── TokenBlacklistMiddleware.cs          # Token revocation check
│   │   ├── IdempotencyMiddleware.cs             # Duplicate request prevention
│   │   ├── AuditMiddleware.cs                   # HTTP request audit
│   │   └── AdminIpWhitelistMiddleware.cs        # Admin IP restriction
│   ├── appsettings.json                         # Base config
│   ├── appsettings.Development.json
│   ├── appsettings.Debug.json
│   ├── appsettings.UAT.json
│   ├── appsettings.Production.json
│   └── Dockerfile                               # Multi-stage Docker build
│
├── Banking.Tests.Unit/                          # Unit Tests
│   └── Services/
│       ├── AuthServiceTests.cs
│       ├── PinServiceTests.cs
│       └── TransactionServiceTests.cs
│
├── Banking.Tests.Integration/                   # Integration Tests
│   ├── BankingApiFactory.cs                     # WebApplicationFactory setup
│   └── Controllers/
│       └── AuthControllerTests.cs
│
├── docker-compose.yml                           # Docker orchestration
└── docker/
    ├── nginx/nginx.conf                         # Load balancer config
    ├── postgres/                                # DB init scripts
    ├── redis/redis.conf                         # Redis config
    ├── prometheus/prometheus.yml                 # Monitoring config
    ├── grafana/                                 # Dashboard config
    └── k6/load-test.js                          # Load testing script
```

---

## 5. Domain Layer

Domain Layer เป็น **แกนกลาง** ของระบบ ไม่ depend กับ framework ใดๆ ประกอบด้วย Entities, Enums, Exceptions, และ Interfaces

### 5.1 Entities

#### `BaseEntity`
```
Properties:
  Id          : Guid      — Primary key (auto-generated UUID)
  CreatedAt   : DateTime  — เวลาสร้าง (set อัตโนมัติใน SaveChangesAsync)
  UpdatedAt   : DateTime? — เวลาแก้ไขล่าสุด (set อัตโนมัติ)
  IsDeleted   : bool      — Soft delete flag (default: false)
```
ทุก Entity สืบทอดจาก BaseEntity เพื่อให้มี fields มาตรฐานร่วมกัน

#### `User`
```
Properties:
  FirstName           : string   — ชื่อ
  LastName            : string   — นามสกุล
  Email               : string   — อีเมล (unique, lowercase)
  Phone               : string   — เบอร์โทร (unique)
  PasswordHash        : string   — Password hash (BCrypt)
  NationalIdHash      : string?  — บัตรประชาชน hash
  KycStatus           : KycStatus — สถานะ KYC (Pending/Verified/Rejected)
  IsActive            : bool     — บัญชีใช้งานได้ไหม
  IsLocked            : bool     — ถูกล็อก (login ผิดเกิน 5 ครั้ง)
  FailedLoginAttempts : int      — จำนวนครั้ง login ผิดติดต่อกัน
  LastLoginAt         : DateTime? — เวลา login ล่าสุด
  FullName            : string   — Computed property: FirstName + LastName
  Accounts            : ICollection<Account> — Navigation: บัญชีทั้งหมด
  PinHash             : string?  — Transaction PIN hash (BCrypt)
  FailedPinAttempts   : int      — จำนวนครั้ง PIN ผิดติดต่อกัน
  IsTransactionLocked : bool     — ธุรกรรมถูกล็อก (PIN ผิดเกิน 3 ครั้ง)
```

#### `Account`
```
Properties:
  UserId                : Guid          — เจ้าของบัญชี (FK → User)
  AccountNumber         : string        — เลขบัญชี (format: XXXX-XXXX-XXXX)
  Type                  : AccountType   — ประเภท (Savings/Checking/FixedDeposit)
  Currency              : string        — สกุลเงิน (default: "THB")
  Balance               : decimal       — ยอดเงินคงเหลือ
  AvailableBalance      : decimal       — ยอดเงินที่ใช้ได้
  DailyWithdrawalLimit  : decimal       — วงเงินถอน/โอนต่อวัน (default: 50,000)
  Status                : AccountStatus — สถานะ (Active/Frozen/Closed)
  User                  : User          — Navigation property
  Transactions          : ICollection<Transaction>
```

#### `Transaction`
```
Properties:
  ReferenceNumber       : string            — เลขอ้างอิง (format: TXN-YYYYMMDD-XXXXXX)
  AccountId             : Guid              — บัญชีที่เกี่ยวข้อง (FK)
  Type                  : TransactionType   — ประเภท (Deposit/Withdrawal/TransferIn/TransferOut/Fee/Interest)
  Amount                : decimal           — จำนวนเงิน
  BalanceBefore         : decimal           — ยอดเงินก่อนทำธุรกรรม
  BalanceAfter          : decimal           — ยอดเงินหลังทำธุรกรรม
  Status                : TransactionStatus — สถานะ (Pending/Processing/Completed/Failed/Reversed)
  Description           : string?           — รายละเอียด
  RelatedTransactionId  : Guid?             — ธุรกรรมคู่ (สำหรับ Transfer)
  Metadata              : string?           — ข้อมูลเพิ่มเติม (JSON)
  IpAddress             : string?           — IP ที่ทำธุรกรรม
```

#### `Transfer`
```
Properties:
  FromAccountId        : Guid              — บัญชีต้นทาง
  ToAccountId          : Guid              — บัญชีปลายทาง
  Amount               : decimal           — จำนวนเงิน
  Fee                  : decimal           — ค่าธรรมเนียม
  Status               : TransactionStatus — สถานะ
  DebitTransactionId   : Guid?             — FK → Transaction (ฝั่งหัก)
  CreditTransactionId  : Guid?             — FK → Transaction (ฝั่งเพิ่ม)
```

#### `AuditLog`
```
Properties:
  Id         : long     — Auto-increment ID
  UserId     : Guid?    — ผู้ทำ action (null = system)
  Action     : string   — ชื่อ action (เช่น "Deposit", "POST /api/auth/login")
  EntityType : string   — ประเภท entity (เช่น "Transaction", "HttpRequest")
  EntityId   : string?  — ID ของ entity ที่เกี่ยวข้อง
  OldValues  : string?  — ค่าเก่า (JSON)
  NewValues  : string?  — ค่าใหม่ (JSON)
  IpAddress  : string?  — IP ผู้ทำ
  UserAgent  : string?  — Browser/Client info
  CreatedAt  : DateTime — เวลาที่บันทึก
```

### 5.2 Enums

| Enum | ค่า | ใช้กับ |
|---|---|---|
| `AccountType` | Savings, Checking, FixedDeposit | Account.Type |
| `AccountStatus` | Active, Frozen, Closed | Account.Status |
| `TransactionType` | Deposit, Withdrawal, TransferIn, TransferOut, Fee, Interest | Transaction.Type |
| `TransactionStatus` | Pending, Processing, Completed, Failed, Reversed | Transaction.Status, Transfer.Status |
| `KycStatus` | Pending, Verified, Rejected | User.KycStatus |

### 5.3 Custom Exceptions

| Exception | HTTP Status | ใช้เมื่อ |
|---|---|---|
| `NotFoundException` | 404 | หา entity ไม่เจอ |
| `InsufficientFundsException` | 400 | เงินไม่พอถอน/โอน |
| `AccountFrozenException` | 403 | บัญชีถูกอายัด |
| `DailyLimitExceededException` | 400 | เกินวงเงินถอน/โอนต่อวัน |
| `DuplicateException` | 409 | Email/Phone ซ้ำ |
| `AccountLockedException` | 403 | User ถูกล็อก (login ผิดเกิน) |

### 5.4 Repository Interfaces

#### `IRepository<T>` — Generic Repository
| Method | การทำงาน |
|---|---|
| `GetByIdAsync(Guid id)` | ดึง entity ตาม ID |
| `GetAllAsync()` | ดึง entity ทั้งหมด |
| `AddAsync(T entity)` | เพิ่ม entity ใหม่ |
| `Update(T entity)` | อัปเดต entity |
| `Remove(T entity)` | Soft delete (set IsDeleted = true) |

#### `IUserRepository` — extends IRepository\<User\>
| Method | การทำงาน |
|---|---|
| `GetByEmailAsync(email)` | ค้นหา user ตาม email (include Accounts) |
| `EmailExistsAsync(email)` | เช็คว่า email ซ้ำไหม |
| `PhoneExistsAsync(phone)` | เช็คว่า phone ซ้ำไหม |

#### `IAccountRepository` — extends IRepository\<Account\>
| Method | การทำงาน |
|---|---|
| `GetByAccountNumberAsync(accountNumber)` | ค้นหาบัญชีตามเลขบัญชี |
| `GetByUserIdAsync(userId)` | ดึงบัญชีทั้งหมดของ user |
| `GetByIdForUpdateAsync(id)` | ดึงบัญชีพร้อม `FOR UPDATE` lock (ป้องกัน race condition) |
| `AccountNumberExistsAsync(accountNumber)` | เช็คเลขบัญชีซ้ำ |

#### `ITransactionRepository` — extends IRepository\<Transaction\>
| Method | การทำงาน |
|---|---|
| `GetByAccountIdAsync(accountId, page, pageSize)` | ดึงธุรกรรมแบบ pagination |
| `GetCountByAccountIdAsync(accountId)` | นับจำนวนธุรกรรม |
| `GetTodayWithdrawalTotalAsync(accountId)` | คำนวณยอดถอน+โอนออกวันนี้ |

#### `IUnitOfWork`
| Member | การทำงาน |
|---|---|
| `Users` | IUserRepository |
| `Accounts` | IAccountRepository |
| `Transactions` | ITransactionRepository |
| `SaveChangesAsync()` | บันทึกทุก change เข้า DB |
| `BeginTransactionAsync()` | เริ่ม DB transaction |
| `CommitTransactionAsync()` | Commit transaction |
| `RollbackTransactionAsync()` | Rollback transaction |

---

## 6. Application Layer

Application Layer มี **business logic** ทั้งหมด แยกจาก Controller เพื่อให้ test ได้ง่ายและ reuse ได้

### 6.1 AuthService

จัดการ Authentication ทั้งหมด

#### `RegisterAsync(RegisterRequest)`
**Flow:**
1. ตรวจว่า password กับ confirmPassword ตรงกัน
2. เช็ค email ซ้ำไหม → throw `DuplicateException`
3. เช็ค phone ซ้ำไหม → throw `DuplicateException`
4. Hash password ด้วย BCrypt
5. สร้าง User entity (KycStatus = Pending)
6. Generate เลขบัญชี (loop จนไม่ซ้ำ)
7. สร้าง default Savings Account
8. SaveChanges
9. Generate JWT Access Token + Refresh Token
10. Return `AuthResponse`

#### `LoginAsync(LoginRequest)`
**Flow:**
1. ค้นหา user ตาม email (lowercase + trim)
2. ถ้าไม่เจอ → throw "Invalid email or password." (ข้อความกว้างๆ เพื่อความปลอดภัย)
3. เช็คว่า user ถูกล็อกไหม → throw `AccountLockedException`
4. Verify password ด้วย BCrypt
5. ถ้า password ผิด:
   - เพิ่ม `FailedLoginAttempts`
   - ถ้าครบ 5 ครั้ง → set `IsLocked = true`
   - throw "Invalid email or password."
6. ถ้า password ถูก:
   - Reset `FailedLoginAttempts = 0`
   - Update `LastLoginAt`
   - Generate JWT + Refresh Token
   - Return `AuthResponse`

#### `GetProfileAsync(Guid userId)`
ดึงข้อมูล user แล้ว map เป็น `UserProfileResponse` (ไม่ส่ง sensitive data กลับ)

### 6.2 TransactionService

จัดการธุรกรรมทั้งหมด (ฝาก/ถอน/โอน) พร้อม concurrency control

#### `DepositAsync(DepositRequest, userId, pin, ipAddress)`
**Flow:**
1. Validate: amount > 0
2. **Verify PIN** (เรียก PinService)
3. **Acquire Distributed Lock** (Redis) ด้วย key `account:{id}`
4. Begin DB Transaction
5. **Lock account row** (`SELECT ... FOR UPDATE`) ป้องกัน race condition
6. Validate: account exists + status = Active
7. Update balance: `Balance += amount`, `AvailableBalance += amount`
8. สร้าง Transaction record (type = Deposit, status = Completed)
9. SaveChanges + Commit
10. **Update balance cache** (Redis)
11. **Audit log**: บันทึก action
12. **Notify** ผ่าน SignalR: BalanceUpdated + TransactionCompleted
13. Release lock (ใน `finally` block — ปลดเสมอไม่ว่าสำเร็จหรือไม่)

#### `WithdrawAsync(WithdrawRequest, userId, pin, ipAddress)`
เหมือน Deposit แต่เพิ่มการตรวจสอบ:
- **เงินพอไหม?** `balance >= amount` → throw `InsufficientFundsException`
- **เกิน Daily Limit ไหม?** `todayTotal + amount > DailyWithdrawalLimit` → throw `DailyLimitExceededException`
- Update balance: `Balance -= amount`

#### `TransferAsync(TransferRequest, userId, pin, ipAddress)`
**ซับซ้อนที่สุด — สร้าง 3 records ใน 1 DB transaction:**

**Flow:**
1. Validate: amount > 0, fromAccount != toAccount
2. Verify PIN
3. **Lock ทั้ง 2 บัญชี** ตามลำดับ ID (ป้องกัน deadlock):
   - Lock บัญชี ID น้อยกว่าก่อน → lock บัญชี ID มากกว่า
4. Begin DB Transaction
5. Lock ทั้ง 2 rows (`FOR UPDATE`)
6. Validate: ทั้ง 2 บัญชี exists + Active
7. เช็ค: เงินพอ? + Daily Limit?
8. Update balance ทั้ง 2 บัญชี:
   - from: `Balance -= amount`
   - to: `Balance += amount`
9. สร้าง 2 Transaction records:
   - **Debit** (TransferOut) สำหรับบัญชีต้นทาง
   - **Credit** (TransferIn) สำหรับบัญชีปลายทาง
   - ทั้ง 2 ชี้หากันผ่าน `RelatedTransactionId`
10. สร้าง 1 Transfer record (เชื่อม debit + credit)
11. SaveChanges + Commit
12. Update cache ทั้ง 2 บัญชี
13. Notify ทั้ง 2 users ผ่าน SignalR
14. Release ทั้ง 2 locks

**ทำไมต้อง Atomic:** ถ้าหักเงิน A สำเร็จแต่เพิ่มเงิน B ล้มเหลว → เงินหาย! DB Transaction ทำให้สำเร็จทั้งคู่หรือไม่ทำเลย

#### `GetHistoryAsync(accountId, page, pageSize)`
ดึงประวัติธุรกรรมแบบ pagination + map เป็น `TransactionResponse`

### 6.3 PinService

จัดการ Transaction PIN สำหรับ 2-factor authentication

#### `SetPinAsync(userId, SetPinRequest)`
- เช็คว่ายังไม่มี PIN → Hash PIN ด้วย BCrypt → บันทึก

#### `ChangePinAsync(userId, ChangePinRequest)`
- Verify PIN เก่า → Hash PIN ใหม่ → Reset FailedPinAttempts

#### `VerifyPinAsync(userId, pin)`
**เรียกก่อนทุกธุรกรรม:**
1. เช็คว่ามี PIN ไหม (ยังไม่ตั้ง → error)
2. เช็คว่าถูกล็อกธุรกรรมไหม → error
3. Verify PIN ด้วย BCrypt:
   - **ถูก** → reset `FailedPinAttempts = 0`
   - **ผิด** → เพิ่ม counter, ถ้าครบ 3 ครั้ง → `IsTransactionLocked = true`

### 6.4 Utility Services

#### `AccountNumberGenerator.Generate()`
สร้างเลขบัญชี format `XXXX-XXXX-XXXX` (random 4 หลัก x 3 ส่วน)

#### `ReferenceNumberGenerator.Generate()`
สร้างเลข reference format `TXN-YYYYMMDD-XXXXXX` (วันที่ + random 6 หลัก)

#### `DataMasking`
| Method | ตัวอย่าง |
|---|---|
| `MaskAccountNumber("1234-5678-9012")` | `****-****-9012` |
| `MaskEmail("admin@bank.com")` | `a****@bank.com` |
| `MaskPhone("0891234567")` | `******4567` |

### 6.5 Validators (FluentValidation)

ทุก Validator สืบทอดจาก `AbstractValidator<T>` แยก validation logic ออกจาก Controller

#### `DepositRequestValidator`
| Rule | เงื่อนไข |
|---|---|
| AccountId | NotEmpty |
| Amount | > 0, <= 1,000,000, ทศนิยม 2 ตำแหน่ง |
| Description | MaxLength 500 (ถ้ามี) |
| Pin | NotEmpty, Length 6, ตัวเลขเท่านั้น |

#### `WithdrawRequestValidator`
เหมือน DepositRequestValidator

#### `TransferRequestValidator`
เหมือน DepositRequestValidator + เพิ่ม:
- `FromAccountId` NotEmpty
- `ToAccountId` NotEmpty
- `FromAccountId != ToAccountId`

#### `SetPinRequestValidator`
- Pin: NotEmpty, Length 6, ตัวเลขเท่านั้น
- ConfirmPin: ต้องเท่ากับ Pin

#### `ChangePinRequestValidator`
- CurrentPin: NotEmpty, Length 6
- NewPin: NotEmpty, Length 6, ตัวเลขเท่านั้น
- ConfirmNewPin: ต้องเท่ากับ NewPin
- NewPin != CurrentPin

### 6.6 Interfaces (Application Layer)

| Interface | หน้าที่ | Implement โดย |
|---|---|---|
| `IJwtService` | Generate/Validate JWT | `JwtService` (Infrastructure) |
| `INotificationService` | ส่ง notification real-time | `NotificationService` (SignalR) |
| `IRedisCacheService` | Cache, Lock, Rate Limit, Blacklist | `RedisCacheService` (Redis) |
| `IAuditService` | บันทึก audit log | `AuditService` (DB) |
| `IFraudDetectionService` | ตรวจจับธุรกรรมผิดปกติ | `FraudDetectionService` |

---

## 7. Infrastructure Layer

Infrastructure Layer implement ทุก interface จาก Application/Domain Layer

### 7.1 AppDbContext

- สืบทอดจาก `DbContext`
- มี `DbSet` สำหรับ: Users, Accounts, Transactions, Transfers, AuditLogs
- Override `SaveChangesAsync` → set `CreatedAt` สำหรับ entity ใหม่, `UpdatedAt` สำหรับ entity ที่แก้ไข
- Apply configuration จาก assembly (Fluent API configurations)

### 7.2 ReadOnlyDbContext

- สืบทอดจาก `AppDbContext`
- Override `SaveChanges` / `SaveChangesAsync` → throw `InvalidOperationException`
- ใช้กับ Read Replica → ป้องกันเขียนข้อมูลผ่าน replica โดยไม่ตั้งใจ

### 7.3 Repository Implementation

#### `Repository<T>` (Generic)
| Method | Implementation |
|---|---|
| `GetByIdAsync` | `FindAsync(id)` |
| `GetAllAsync` | `ToListAsync()` |
| `AddAsync` | `AddAsync(entity)` |
| `Update` | `Update(entity)` |
| `Remove` | **Soft delete**: set `IsDeleted = true` แล้ว `Update` |

#### `UserRepository`
| Method | Implementation |
|---|---|
| `GetByEmailAsync` | `Include(Accounts).FirstOrDefault(email == x)` |
| `EmailExistsAsync` | `AnyAsync(email == x)` |
| `PhoneExistsAsync` | `AnyAsync(phone == x)` |

#### `AccountRepository`
| Method | Implementation |
|---|---|
| `GetByAccountNumberAsync` | `FirstOrDefault(accountNumber == x)` |
| `GetByUserIdAsync` | `Where(userId == x).OrderByDescending(CreatedAt)` |
| `GetByIdForUpdateAsync` | **Raw SQL**: `SELECT * FROM accounts WHERE Id = x FOR UPDATE` — PostgreSQL row-level lock ป้องกัน concurrent access |
| `AccountNumberExistsAsync` | `AnyAsync(accountNumber == x)` |

#### `TransactionRepository`
| Method | Implementation |
|---|---|
| `GetByAccountIdAsync` | `Where(accountId).OrderByDesc(CreatedAt).Skip().Take()` — Pagination |
| `GetCountByAccountIdAsync` | `CountAsync(accountId == x)` |
| `GetTodayWithdrawalTotalAsync` | `Where(accountId AND (Withdrawal OR TransferOut) AND Completed AND today).Sum(Amount)` |

#### `UnitOfWork`
- Lazy-initialize repositories (`_users ??= new UserRepository(_context)`)
- Manage DB transaction lifecycle (Begin → Commit/Rollback)
- Implement `IDisposable` สำหรับ cleanup

### 7.4 JwtService

#### `GenerateAccessToken(User)`
1. สร้าง Claims: NameIdentifier (userId), Email, Name, JTI (unique token ID)
2. สร้าง SymmetricSecurityKey จาก config `Jwt:Key`
3. Sign ด้วย HMAC-SHA256
4. Set expiry จาก config (default 15 นาที)
5. Return token string

#### `GenerateRefreshToken()`
- สร้าง random 64 bytes → encode Base64

#### `GetUserIdFromExpiredToken(token)`
- Validate token โดย **ไม่เช็ค lifetime** (สำหรับ refresh flow)
- Extract userId จาก claims

### 7.5 RedisCacheService

**Generic Cache:**
| Method | การทำงาน |
|---|---|
| `SetAsync<T>(key, value, expiry)` | Serialize เป็น JSON → SET key |
| `GetAsync<T>(key)` | GET key → Deserialize จาก JSON |
| `RemoveAsync(key)` | DEL key |
| `ExistsAsync(key)` | EXISTS key |

**Balance Cache (Redis Hash):**
| Method | การทำงาน |
|---|---|
| `SetBalanceCacheAsync(accountId, balance, availableBalance)` | HSET `balance:{id}` → set Balance + AvailableBalance + UpdatedAt, EXPIRE 5 นาที |
| `GetBalanceCacheAsync(accountId)` | HGETALL `balance:{id}` → return tuple |
| `InvalidateBalanceCacheAsync(accountId)` | DEL `balance:{id}` |

**Distributed Lock:**
| Method | การทำงาน |
|---|---|
| `AcquireLockAsync(key, value, expiry)` | `SET lock:{key} value NX EX` — Set ถ้ายังไม่มี (atomic) |
| `ReleaseLockAsync(key, value)` | **Lua Script**: เช็คว่า value ตรงกันก่อน DEL (ป้องกันปลด lock คนอื่น) |

**Rate Limiting (Fixed Window):**
| Method | การทำงาน |
|---|---|
| `CheckRateLimitAsync(key, max, window)` | INCR counter + EXPIRE → ถ้าเกิน max return false |

**Token Blacklist:**
| Method | การทำงาน |
|---|---|
| `BlacklistTokenAsync(jti, ttl)` | SET `blacklist:{jti}` 1 EX ttl |
| `IsTokenBlacklistedAsync(jti)` | EXISTS `blacklist:{jti}` |

### 7.6 NotificationService (SignalR)

#### `NotifyBalanceUpdatedAsync(userId, accountId, newBalance, newAvailableBalance)`
ส่ง event `BalanceUpdated` ไปยัง:
- Group `user:{userId}` — แจ้ง user ทุก tab/device
- Group `account:{accountId}` — แจ้ง client ที่ subscribe บัญชีนี้

#### `NotifyTransactionAsync(userId, type, amount, referenceNumber)`
ส่ง event `TransactionCompleted` ไปยัง Group `user:{userId}`

### 7.7 AuditService

#### `LogAsync(userId, action, entityType, entityId, oldValues, newValues, ipAddress, userAgent)`
- สร้าง `IServiceScope` ใหม่ (ใช้ separate DbContext เพื่อไม่กระทบ current transaction)
- Serialize oldValues/newValues เป็น JSON
- Insert `AuditLog` record

### 7.8 FraudDetectionService

#### `CheckTransactionAsync(accountId, amount, transactionType)`
ตรวจ 4 กฎ:
1. **Large Transaction**: amount >= 100,000 THB → Risk: Medium
2. **High Frequency**: > 5 ธุรกรรมใน 10 นาที (ใช้ Redis counter) → Risk: High
3. **Unusual Hours**: 01:00-05:00 (Bangkok time) → Risk: Medium
4. **Near Daily Limit**: ใช้ >= 80% ของ daily limit → Risk: Medium

Return `FraudCheckResult(IsSuspicious, Reason, RiskLevel)` + บันทึก audit log ถ้าพบ suspicious

---

## 8. API Layer

### 8.1 Program.cs (Entry Point + DI Configuration)

Program.cs ทำหน้าที่:

1. **Register Services** (Dependency Injection):
   - `AppDbContext` → PostgreSQL Primary (Write)
   - `ReadOnlyDbContext` → PostgreSQL Replica (Read)
   - `IConnectionMultiplexer` → Redis connection (Singleton)
   - JWT Authentication → Validate Bearer token + SignalR query string support
   - UnitOfWork, Services, Validators
   - SignalR + Redis backplane
   - CORS (Allow Frontend URL)
   - Forwarded Headers (สำหรับ Nginx proxy)

2. **Auto Migration & Seed**:
   - Development/Debug: migrate + seed demo data
   - Production: migrate เท่านั้น

3. **Middleware Pipeline** (ลำดับสำคัญ):
   - Exception → ForwardedHeaders → HTTPS → CORS → Auth → TokenBlacklist → RateLimit → Idempotency → AdminIpWhitelist → Authorization → Audit → Controllers

4. **Endpoints**:
   - `MapControllers()` — REST API
   - `MapHub<NotificationHub>("/hubs/notifications")` — SignalR WebSocket
   - `MapMetrics()` — Prometheus `/metrics`
   - `MapGet("/health")` — Health check (DB + Redis)

### 8.2 AuthController

| Endpoint | Method | Auth | การทำงาน |
|---|---|---|---|
| `/api/auth/register` | POST | Anonymous | สมัครสมาชิก → validate → AuthService.Register |
| `/api/auth/login` | POST | Anonymous | เข้าสู่ระบบ → validate → AuthService.Login |
| `/api/auth/profile` | GET | Authorize | ดูโปรไฟล์ → ดึง userId จาก JWT claims |
| `/api/auth/logout` | POST | Authorize | Logout → อ่าน JTI จาก JWT → blacklist ใน Redis พร้อม TTL |
| `/api/auth/pin/set` | POST | Authorize | ตั้ง PIN ครั้งแรก |
| `/api/auth/pin/change` | POST | Authorize | เปลี่ยน PIN (ต้องใส่ PIN เก่า) |

### 8.3 AccountsController

| Endpoint | Method | การทำงาน |
|---|---|---|
| `/api/accounts?userId=xxx` | GET | ดึงบัญชีทั้งหมดของ user |
| `/api/accounts/{id}` | GET | ดึงข้อมูลบัญชีตาม ID |
| `/api/accounts/{id}/balance` | GET | ดูยอดเงิน (**Cache-First**: เช็ค Redis ก่อน → ถ้าไม่มีค่อย query DB + set cache) |
| `/api/accounts` | POST | สร้างบัญชีใหม่ (generate เลขบัญชี unique) |

### 8.4 TransactionsController

| Endpoint | Method | การทำงาน |
|---|---|---|
| `/api/transactions/deposit` | POST | ฝากเงิน → validate → TransactionService.Deposit |
| `/api/transactions/withdraw` | POST | ถอนเงิน → validate → TransactionService.Withdraw |
| `/api/transactions/transfer` | POST | โอนเงิน → validate → TransactionService.Transfer |
| `/api/transactions?accountId=x&page=1&pageSize=20` | GET | ดูประวัติธุรกรรม (pagination) |

### 8.5 AdminController

| Endpoint | Method | การทำงาน |
|---|---|---|
| `/api/admin/dashboard` | GET | สถิติระบบ (จำนวน users, accounts, balance รวม, etc.) |
| `/api/admin/accounts/{id}/freeze` | POST | อายัดบัญชี (set status = Frozen) |
| `/api/admin/accounts/{id}/unfreeze` | POST | ปลดอายัดบัญชี (set status = Active) |
| `/api/admin/users/{id}/unlock` | POST | ปลดล็อก user (reset IsLocked + FailedLoginAttempts) |
| `/api/admin/user/{id}/reset-pin-lock` | POST | Reset PIN lock + clear PinHash (ต้องตั้ง PIN ใหม่) |

---

## 9. Middleware Pipeline

ลำดับ middleware สำคัญมาก — ทำงานจากบนลงล่าง

### 9.1 ExceptionMiddleware (อันดับ 1)
- ครอบทุก request ด้วย try-catch
- Map domain exceptions → HTTP status code:
  - `NotFoundException` → 404
  - `InsufficientFundsException` → 400
  - `AccountFrozenException` → 403
  - `AccountLockedException` → 403
  - `DuplicateException` → 409
  - `ArgumentException` → 400
  - อื่นๆ → 500 (log error)
- Return JSON format: `{ success, message, statusCode }`

### 9.2 TokenBlacklistMiddleware (อันดับ 5)
- อ่าน JWT จาก `Authorization: Bearer ...` header
- Parse token → ดึง JTI (JWT ID)
- เช็ค Redis: `blacklist:{jti}` exists?
- ถ้า blacklisted → return 401 "Token has been revoked"

### 9.3 RateLimitMiddleware (อันดับ 6)
- ข้าม Swagger + Health endpoints
- สร้าง key จาก: userId (ถ้า login แล้ว) หรือ IP + endpoint path
- เรียก `CheckRateLimitAsync` (Redis INCR)
- Config: default 10 requests / 60 seconds
- เกิน limit → return 429 Too Many Requests

### 9.4 IdempotencyMiddleware (อันดับ 7)
- ทำงานเฉพาะ POST/PUT/PATCH
- อ่าน header `X-Idempotency-Key`
- ถ้ามี key → เช็ค Redis:
  - **Cache Hit**: return cached response ทันที (ไม่ process ซ้ำ)
  - **Cache Miss**: process request → cache response 24 ชม.
- ป้องกัน: double-click, network retry, ส่ง request ซ้ำ

### 9.5 AdminIpWhitelistMiddleware (อันดับ 8)
- ทำงานเฉพาะ path `/api/admin`
- อ่าน allowed IPs จาก config `Security:AdminAllowedIps`
- เช็ค `RemoteIpAddress`
- ไม่อยู่ใน whitelist → return 403

### 9.6 AuditMiddleware (อันดับ 10)
- ข้าม GET/OPTIONS/HEAD + Swagger + Health
- หลัง process request → บันทึก audit log:
  - userId, HTTP method + path, status code, query string, IP, User-Agent
- **ไม่ block request ถ้า audit ล้มเหลว** (try-catch swallow error)

---

## 10. Real-time Communication (SignalR)

### NotificationHub

**Authorization**: ต้องมี JWT token

**Connection Flow:**
1. Client connect → `OnConnectedAsync`:
   - ดึง userId จาก JWT claims
   - Add connection เข้า group `user:{userId}`
2. Client disconnect → `OnDisconnectedAsync`:
   - Remove connection จาก group

**Custom Methods:**
- `JoinAccountGroup(accountId)` — subscribe การเปลี่ยนแปลงของบัญชีเฉพาะ
- `LeaveAccountGroup(accountId)` — unsubscribe

**Events ที่ส่งจาก Server:**
| Event | Data | ส่งเมื่อ |
|---|---|---|
| `BalanceUpdated` | { AccountId, Balance, AvailableBalance, UpdatedAt } | ยอดเงินเปลี่ยน |
| `TransactionCompleted` | { Type, Amount, ReferenceNumber, CreatedAt } | ธุรกรรมสำเร็จ |

**SignalR + Redis Backplane:**
- ใช้ `AddStackExchangeRedis` เป็น backplane
- Channel prefix: `banking-signalr:`
- ทำให้ทุก API instance แชร์ SignalR connections ได้ (scale horizontally)

### Frontend Connection (WebSocket)
- JWT ส่งผ่าน query string: `/hubs/notifications?access_token=...`
- เหตุผล: WebSocket ไม่มี HTTP header → ต้องส่งผ่าน query string
- Program.cs มี `OnMessageReceived` event handler ที่อ่าน token จาก query string

---

## 11. Caching & Distributed Lock (Redis)

### Cache Strategy: Cache-Aside (Lazy Loading)

```
Client → API → เช็ค Redis Cache
                  │
                  ├── Cache Hit → return cached data (เร็วมาก)
                  │
                  └── Cache Miss → query DB → set cache → return data
```

**ใช้กับ:** Balance query (`/api/accounts/{id}/balance`)

### Distributed Lock Pattern

```
Request A ─────► AcquireLock("account:123") → SUCCESS
                      │
                      ├── Process transaction
                      │
                      └── ReleaseLock("account:123")

Request B ─────► AcquireLock("account:123") → FAIL (locked)
                      │
                      └── Return "Account is being processed"
```

**เทคนิค:**
- **SET NX EX** (atomic operation) — set key ก็ต่อเมื่อยังไม่มี
- **Lua Script สำหรับ Release** — เช็ค value ตรงก่อนลบ (ป้องกันปลด lock คนอื่น)
- **TTL 10 วินาที** — auto-release ถ้า process ค้าง

### Transfer: Lock 2 บัญชีพร้อมกัน

```
ป้องกัน Deadlock:
  A โอนให้ B → lock(A) → lock(B) → process
  B โอนให้ A → lock(A) → lock(B) → process  ← lock ตามลำดับ ID เสมอ!

ถ้าไม่ lock ตามลำดับ:
  A โอนให้ B → lock(A) → รอ lock(B)...
  B โอนให้ A → lock(B) → รอ lock(A)... → DEADLOCK!
```

---

## 12. Docker & Infrastructure

### Docker Compose Architecture

```
┌─────────────────────────────────────────────────────┐
│                    Docker Network                    │
│                                                     │
│  ┌──────────┐  ┌──────┐  ┌──────┐  ┌──────┐       │
│  │  Nginx   │──│API-1 │  │API-2 │  │API-3 │       │
│  │ :80/:443 │  │:8080 │  │:8080 │  │:8080 │       │
│  └──────────┘  └──┬───┘  └──┬───┘  └──┬───┘       │
│       │            │         │         │            │
│       │     ┌──────┴─────────┴─────────┴──────┐    │
│       │     │                                  │    │
│  ┌────┴─────┴──┐  ┌────────────┐              │    │
│  │  PostgreSQL │  │   Redis    │              │    │
│  │  Primary    │  │   :6379   │              │    │
│  │  :5432      │  └────────────┘              │    │
│  └──────┬──────┘                              │    │
│         │                                     │    │
│  ┌──────┴──────┐  ┌────────────┐  ┌────────┐ │    │
│  │  PostgreSQL │  │ Prometheus │  │Grafana │ │    │
│  │  Replica    │  │   :9090   │  │ :3001  │ │    │
│  │  :5433      │  └────────────┘  └────────┘ │    │
│  └─────────────┘                              │    │
└─────────────────────────────────────────────────────┘
```

### Services

| Service | Image | Port | หน้าที่ |
|---|---|---|---|
| postgres-primary | postgres:16-alpine | 5432 | Database หลัก (Read + Write) |
| postgres-replica | postgres:16-alpine | 5433 | Read replica |
| redis | redis:7-alpine | 6379 | Cache, Lock, Rate Limit, Pub/Sub |
| api-1, api-2, api-3 | Custom build | 8080 | API instances (3 ตัว) |
| nginx | nginx:alpine | 80, 443 | Load balancer + reverse proxy |
| prometheus | prom/prometheus | 9090 | Metrics collection |
| grafana | grafana/grafana | 3001 | Dashboard |

### Nginx Configuration

**Load Balancing:**
- Algorithm: `least_conn` — ส่ง request ไป server ที่มี connection น้อยที่สุด
- Health check: `max_fails=3 fail_timeout=30s`
- Keepalive: 32 connections

**Rate Limiting (Layer 1):**
- API: 10 req/s per IP (burst 20)
- Auth: 3 req/s per IP (burst 5) — เข้มกว่า

**Security Headers:**
- `X-Frame-Options: SAMEORIGIN`
- `X-Content-Type-Options: nosniff`
- `X-XSS-Protection: 1; mode=block`
- `Referrer-Policy: strict-origin-when-cross-origin`
- `Content-Security-Policy: default-src 'self'`

**WebSocket Support:**
- `/hubs/` path → Upgrade header + timeout 3600s

### Dockerfile (Multi-stage Build)

| Stage | Base Image | หน้าที่ |
|---|---|---|
| build | sdk:10.0 | Restore → Build → Publish |
| runtime | aspnet:10.0 | Run เฉพาะ compiled output (image เล็กกว่า) |

**Security:** สร้าง non-root user `appuser` สำหรับ runtime

---

## 13. CI/CD Pipeline

### GitHub Actions Workflows

#### `backend-ci.yml` — Build & Test
**Trigger:** Push to main/develop + PR to main (เฉพาะ `BankingSystem/` เปลี่ยน)

**Steps:**
1. Checkout code
2. Setup .NET 10 SDK
3. Cache NuGet packages
4. Restore dependencies
5. Build solution (Release)
6. Run Unit Tests + Code Coverage
7. Run Integration Tests (ใช้ PostgreSQL + Redis services)
8. Upload test results + coverage report

#### `docker-build.yml` — Build & Push Docker Image
**Trigger:** Push to main

**Steps:**
1. Setup Docker Buildx
2. Login to GitHub Container Registry (GHCR)
3. Extract metadata (tags: sha, latest, semver)
4. Build & Push ด้วย layer cache (GitHub Actions cache)

#### `deploy-staging.yml` — Deploy to Staging
**Trigger:** หลัง Backend CI + Docker Build สำเร็จ

**Steps:**
1. Deploy backend → Railway
2. Health check (retry 10 ครั้ง)
3. Smoke tests
4. Deploy frontend → Vercel (Preview)
5. Slack notification

#### Workflows อื่นๆ
- `frontend-ci.yml` — Build & Test frontend
- `db-migration.yml` — Database migration
- `deploy-production.yml` — Deploy production
- `security-scan.yml` — Security scanning
- `slack-notification.yml` — Reusable Slack notification

---

## 14. Frontend (Next.js)

### Architecture

```
banking-frontend/
├── app/                         # App Router (Pages)
│   ├── (auth)/login/           # Login page
│   ├── (dashboard)/            # Dashboard layout
│   │   ├── dashboard/          # Main dashboard
│   │   └── deposit/            # Deposit page
│   ├── layout.tsx              # Root layout
│   └── page.tsx                # Landing page
│
├── components/
│   ├── layout/                 # Header, Sidebar
│   └── ui/                     # shadcn/ui components
│
├── lib/
│   ├── api/                    # API client functions
│   │   ├── client.ts           # Base Axios/Fetch client
│   │   ├── auth.ts             # Auth API calls
│   │   ├── accounts.ts         # Account API calls
│   │   └── transactions.ts     # Transaction API calls
│   ├── hooks/                  # React hooks
│   │   ├── use-auth.ts         # Auth hook (TanStack Query)
│   │   ├── use-accounts.ts     # Account hook
│   │   ├── use-transactions.ts # Transaction hook
│   │   └── use-signalr.ts      # SignalR connection hook
│   ├── stores/
│   │   └── auth-store.ts       # Zustand auth store
│   ├── types/                  # TypeScript types
│   └── validations/            # Zod schemas
│
├── providers/
│   ├── query-provider.tsx      # TanStack Query provider
│   └── signalr-provider.tsx    # SignalR connection provider
│
└── middleware.ts                # Next.js middleware (auth redirect)
```

### Key Patterns:
- **Server Components** สำหรับ static content
- **Client Components** สำหรับ interactive UI
- **TanStack Query** สำหรับ server state management + caching
- **Zustand** สำหรับ client state (auth token)
- **SignalR Provider** สำหรับ real-time WebSocket connection

---

## 15. Testing

### Unit Tests (`Banking.Tests.Unit`)

ใช้ mock dependencies ผ่าน interfaces

| Test File | ทดสอบ |
|---|---|
| `AuthServiceTests.cs` | Register, Login, GetProfile, lock mechanism |
| `PinServiceTests.cs` | SetPin, ChangePin, VerifyPin, lock mechanism |
| `TransactionServiceTests.cs` | Deposit, Withdraw, Transfer, edge cases |

### Integration Tests (`Banking.Tests.Integration`)

ใช้ `WebApplicationFactory` สร้าง in-memory server + test กับ real DB/Redis

| Test File | ทดสอบ |
|---|---|
| `BankingApiFactory.cs` | Setup test server + override connection strings |
| `AuthControllerTests.cs` | Register/Login HTTP endpoints end-to-end |

---

## 16. Security Features

### Authentication & Authorization
- **JWT Bearer Token** — stateless, มี expiry 15 นาที
- **Token Blacklist** — Logout invalidate token ผ่าน Redis
- **BCrypt Password Hashing** — ป้องกัน rainbow table
- **BCrypt PIN Hashing** — PIN เก็บเป็น hash เท่านั้น

### Brute Force Protection
- **Login**: 5 attempts → lock account
- **PIN**: 3 attempts → lock transactions
- **Rate Limiting**: 2 layers (Nginx + Redis)

### Data Protection
- **Data Masking** — ซ่อน account number, email, phone ใน logs
- **Soft Delete** — ไม่ลบข้อมูลจริง
- **Audit Log** — บันทึกทุก action
- **CORS** — จำกัด origin ที่เข้าถึงได้

### Transaction Safety
- **Database Transaction** — ACID compliance
- **Row-level Lock** (`FOR UPDATE`) — ป้องกัน race condition
- **Distributed Lock** (Redis) — ป้องกัน concurrent access ข้าม instances
- **Idempotency Key** — ป้องกัน duplicate request
- **Daily Withdrawal Limit** — จำกัดความเสียหาย

### Network Security
- **Admin IP Whitelist** — เฉพาะ IP ที่อนุญาต
- **Security Headers** (Nginx) — XSS, Clickjacking, MIME sniffing protection
- **Non-root Docker user** — ลด attack surface
- **HTTPS Redirect** — บังคับ encrypted connection

---

## 17. การ Setup โปรเจค

### Prerequisites
- .NET 10 SDK
- Node.js 22+
- Docker Desktop
- PostgreSQL 16 (หรือใช้ Docker)
- Redis 7 (หรือใช้ Docker)

### Quick Start (Docker)

```bash
cd BankingSystem
docker compose up -d
```

จะได้:
- API: http://localhost:80 (ผ่าน Nginx)
- Swagger: http://localhost:80/swagger
- Grafana: http://localhost:3001 (admin/admin123)
- Prometheus: http://localhost:9090

### Local Development

```bash
# Backend
cd BankingSystem/Banking.Api
dotnet run --environment Debug

# Frontend
cd banking-frontend
npm install
npm run dev
```

### Environment Variables

| Variable | Default | คำอธิบาย |
|---|---|---|
| `ConnectionStrings__DefaultConnection` | (appsettings) | PostgreSQL connection string |
| `ConnectionStrings__Redis` | localhost:6379 | Redis connection string |
| `Jwt__Key` | (appsettings) | JWT signing key (>= 32 chars) |
| `Jwt__Issuer` | banking-api | JWT issuer |
| `Jwt__Audience` | banking-frontend | JWT audience |
| `Frontend__Url` | http://localhost:3000 | CORS allowed origin |
| `Swagger__Enabled` | false | เปิด/ปิด Swagger UI |

---

> เอกสารนี้สร้างจากการวิเคราะห์ source code ทั้งหมดของโปรเจค — อัปเดตล่าสุด: 1 เมษายน 2569
