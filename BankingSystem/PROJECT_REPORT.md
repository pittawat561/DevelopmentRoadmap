# Banking System - Project Report

## Overview

Banking System เป็น **RESTful Web API** สำหรับระบบธนาคาร พัฒนาด้วย **.NET 10.0 + ASP.NET Core** ใช้สถาปัตยกรรม **Clean Architecture** รองรับการจัดการบัญชี ธุรกรรมฝาก-ถอน-โอนเงิน ระบบยืนยันตัวตน (JWT) และ Audit Trail สำหรับการตรวจสอบ

---

## Tech Stack

| Category | Technology |
|---|---|
| **Runtime** | .NET 10.0 |
| **Web Framework** | ASP.NET Core (Controllers) |
| **Database** | PostgreSQL 5432 |
| **ORM** | Entity Framework Core 10.0.5 (Npgsql) |
| **Authentication** | JWT Bearer (HMAC SHA-256) |
| **Password Hashing** | BCrypt.Net-Next 4.1.0 |
| **Validation** | FluentValidation 12.1.1 |
| **Mediator** | MediatR 14.1.0 (พร้อมสำหรับ CQRS) |
| **Logging** | Serilog (Console + File sinks) |
| **API Docs** | Swashbuckle / Swagger |
| **Caching (Phase 3)** | StackExchange.Redis 2.12.8 |

---

## Project Structure

```
BankingSystem/
├── Banking.Api/                     # Presentation Layer
│   ├── Controllers/
│   │   ├── AuthController.cs        # /api/auth - Register, Login, Profile, Logout
│   │   ├── AccountsController.cs    # /api/accounts - CRUD บัญชี
│   │   ├── TransactionsController.cs# /api/transactions - ฝาก, ถอน, โอน
│   │   └── AdminController.cs       # /api/admin - Dashboard, Freeze, Unlock
│   ├── Middleware/
│   │   └── ExceptionMiddleware.cs   # Global error handling
│   └── Program.cs                   # DI, Auth, Pipeline configuration
│
├── Banking.Application/             # Application/Business Logic Layer
│   ├── DTOs/                        # Request/Response models
│   ├── Interfaces/                  # Service contracts
│   ├── Services/
│   │   ├── AuthService.cs           # สมัครสมาชิก, เข้าสู่ระบบ
│   │   └── TransactionService.cs    # ฝาก, ถอน, โอน (ACID)
│   ├── Validators/                  # FluentValidation rules
│   ├── Helpers/                     # AccountNumber/ReferenceNumber generators
│   └── Exceptions/                  # Custom domain exceptions
│
├── Banking.Domain/                  # Domain Layer (ไม่มี dependency ภายนอก)
│   ├── Entities/
│   │   ├── BaseEntity.cs            # Id, CreatedAt, UpdatedAt, IsDeleted
│   │   ├── User.cs                  # ผู้ใช้งาน
│   │   ├── Account.cs               # บัญชีธนาคาร
│   │   ├── Transaction.cs           # รายการธุรกรรม
│   │   ├── Transfer.cs              # การโอนเงิน
│   │   └── AuditLog.cs              # บันทึกการตรวจสอบ
│   ├── Enums/                       # AccountType, TransactionType, etc.
│   └── Interfaces/                  # Repository contracts
│
├── Banking.Infrastructure/          # Infrastructure Layer
│   ├── Data/
│   │   ├── AppDbContext.cs           # EF Core DbContext
│   │   └── Configurations/          # Entity type configurations
│   ├── Repositories/
│   │   ├── Repository.cs            # Generic CRUD
│   │   ├── UserRepository.cs
│   │   ├── AccountRepository.cs
│   │   ├── TransactionRepository.cs
│   │   └── UnitOfWork.cs            # Transaction management
│   ├── Services/
│   │   └── JwtService.cs            # Token generation/validation
│   └── Migrations/                  # EF Core migrations
│
├── Banking.Tests.Unit/              # Unit Tests
└── Banking.Tests.Integration/       # Integration Tests
```

---

## Architecture

ใช้ **Clean Architecture** แบ่ง 4 layers ชัดเจน โดย dependency ไหลจากนอกเข้าใน

```
┌─────────────────────────────────────────────┐
│          Banking.Api (Presentation)         │  Controllers, Middleware, DI Setup
├─────────────────────────────────────────────┤
│       Banking.Application (Business)        │  Services, DTOs, Validators, Exceptions
├─────────────────────────────────────────────┤
│          Banking.Domain (Core)              │  Entities, Enums, Interfaces (ไม่มี dependency)
├─────────────────────────────────────────────┤
│     Banking.Infrastructure (Data Access)    │  EF Core, Repositories, JWT Service
└─────────────────────────────────────────────┘
```

### Design Patterns ที่ใช้

- **Repository Pattern** - แยก data access logic ออกจาก business logic
- **Unit of Work** - จัดการ database transaction ข้าม repositories
- **Generic Repository** - `Repository<T>` base class สำหรับ CRUD ทั่วไป
- **DTO Pattern** - แยก domain entities จาก API request/response
- **Soft Delete** - ใช้ `IsDeleted` flag แทนการลบจริง พร้อม Global Query Filters
- **Exception Middleware** - จัดการ error แบบ centralized

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

---

## API Endpoints

### Authentication (`/api/auth`)

| Method | Endpoint | Description | Auth |
|---|---|---|---|
| POST | `/register` | สมัครสมาชิก + สร้างบัญชี Savings อัตโนมัติ | No |
| POST | `/login` | เข้าสู่ระบบ → JWT + Refresh Token | No |
| GET | `/profile` | ดูข้อมูลส่วนตัว | Yes |
| POST | `/logout` | ออกจากระบบ (Phase 3: Redis blacklist) | Yes |

### Accounts (`/api/accounts`)

| Method | Endpoint | Description | Auth |
|---|---|---|---|
| GET | `/` | ดูบัญชีทั้งหมดของผู้ใช้ | No* |
| GET | `/{id}` | ดูรายละเอียดบัญชี | No* |
| GET | `/{id}/balance` | ดูยอดเงิน | No* |
| POST | `/` | เปิดบัญชีใหม่ | No* |

### Transactions (`/api/transactions`)

| Method | Endpoint | Description | Auth |
|---|---|---|---|
| POST | `/deposit` | ฝากเงิน | No* |
| POST | `/withdraw` | ถอนเงิน (ตรวจยอดเงิน + วงเงิน/วัน) | No* |
| POST | `/transfer` | โอนเงินระหว่างบัญชี (Atomic) | No* |
| GET | `/` | ดูประวัติธุรกรรม (Pagination) | No* |

### Admin (`/api/admin`)

| Method | Endpoint | Description | Auth |
|---|---|---|---|
| GET | `/dashboard` | สถิติระบบ (Users, Accounts, Balance) | Yes |
| POST | `/accounts/{id}/freeze` | อายัดบัญชี | Yes |
| POST | `/accounts/{id}/unfreeze` | ปลดอายัดบัญชี | Yes |
| POST | `/users/{id}/unlock` | ปลดล็อกผู้ใช้ | Yes |

---

## การทำงานหลัก (Core Workflows)

### 1. สมัครสมาชิก (Register)

```
Client → POST /api/auth/register
  ├── FluentValidation ตรวจ input
  ├── ตรวจ email/phone ซ้ำ
  ├── Hash password ด้วย BCrypt
  ├── สร้าง User entity
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

### 3. ฝากเงิน (Deposit)

```
Client → POST /api/transactions/deposit
  ├── Begin Database Transaction
  ├── SELECT account FOR UPDATE (Row Lock)
  ├── ตรวจสอบ Account Status (Active?)
  ├── บันทึก BalanceBefore
  ├── เพิ่ม Balance + AvailableBalance
  ├── สร้าง Transaction (Type: Deposit, Status: Completed)
  ├── Commit Transaction
  └── Return TransactionResponse
```

### 4. ถอนเงิน (Withdraw)

```
Client → POST /api/transactions/withdraw
  ├── Begin Database Transaction
  ├── SELECT account FOR UPDATE (Row Lock)
  ├── ตรวจสอบ Account Status (Active?)
  ├── ตรวจ AvailableBalance >= Amount
  ├── คำนวณยอดถอนวันนี้ + ตรวจ DailyWithdrawalLimit
  ├── หัก Balance + AvailableBalance
  ├── สร้าง Transaction (Type: Withdrawal, Status: Completed)
  ├── Commit Transaction
  └── Return TransactionResponse
```

### 5. โอนเงิน (Transfer) - Atomic Operation

```
Client → POST /api/transactions/transfer
  ├── Begin Database Transaction
  ├── ตรวจ FromAccountId ≠ ToAccountId
  ├── SELECT ทั้ง 2 บัญชี FOR UPDATE (Row Lock)
  ├── ตรวจทั้ง 2 บัญชี Active
  ├── ตรวจ AvailableBalance >= Amount
  ├── ตรวจ DailyWithdrawalLimit (บัญชีต้นทาง)
  ├── หักเงินบัญชีต้นทาง
  ├── เพิ่มเงินบัญชีปลายทาง
  ├── สร้าง 2 Transactions:
  │   ├── TransferOut (บัญชีต้นทาง)
  │   └── TransferIn (บัญชีปลายทาง)
  │   └── เชื่อมกันด้วย RelatedTransactionId
  ├── สร้าง Transfer record
  ├── Commit Transaction
  └── Return TransactionResponse
```

---

## Security

### Authentication & Authorization
- **JWT Bearer Token** - Access Token หมดอายุ 15 นาที
- **Refresh Token** - หมดอายุ 7 วัน (Phase 3: เก็บใน Redis)
- **HMAC SHA-256** signing algorithm
- **ClockSkew = 0** - ไม่มี tolerance สำหรับ token หมดอายุ

### Password Protection
- **BCrypt** salted hashing (ไม่เก็บ plaintext)
- **Password Rules**: 8+ ตัวอักษร, ตัวพิมพ์ใหญ่, ตัวพิมพ์เล็ก, ตัวเลข

### Brute Force Protection
- ล็อกบัญชีหลัง login ผิด 5 ครั้งติดต่อกัน
- Admin ปลดล็อกผ่าน `/api/admin/users/{id}/unlock`

### Data Integrity
- **Row-level locking** (`SELECT ... FOR UPDATE`) ป้องกัน race conditions
- **Database Transaction** ทุกการเงินเป็น atomic
- **Check Constraint** `Balance >= 0` ป้องกันยอดติดลบ
- **Daily Withdrawal Limit** จำกัดวงเงินถอน 50,000 THB/วัน

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
| Unhandled | 500 | Internal Server Error |

---

## Database

- **Provider**: PostgreSQL (localhost:5432)
- **Database Name**: `banking_dev`
- **ORM**: Entity Framework Core 10.0.5 (Npgsql)
- **Migration**: Auto-migrate on startup (Debug/Development)
- **Retry Policy**: 3 attempts สำหรับ transient errors
- **Command Timeout**: 30 วินาที
- **Soft Delete**: Global Query Filters กรอง `IsDeleted = true` อัตโนมัติ

---

## Environment Profiles

| Profile | HTTP | HTTPS | Use Case |
|---|---|---|---|
| Debug | localhost:5287 | - | Development |
| Development | localhost:5287 | localhost:7297 | Development + HTTPS |
| UAT | localhost:5287 | localhost:7297 | User Acceptance Testing |
| Production | - | localhost:7297 | Production (HTTPS only) |

---

## Phase Roadmap

| Phase | Status | Features |
|---|---|---|
| **Phase 1** | Done | Domain entities, EF Core, Repository pattern |
| **Phase 2** | Done | API controllers, Authentication, Transaction services |
| **Phase 3** | Planned | Redis caching, Refresh token blacklist, Rate limiting |
