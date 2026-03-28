# Phase 1: Database Setup — คู่มือทำทีละขั้นตอน (.NET 10 + Visual Studio 2026)

> ทุกขั้นตอนอธิบายว่า "ทำไมต้องสร้าง" และ "สร้างเพื่ออะไร"

---

## ภาพรวม: ทำไมถึงเลือก Stack นี้

```
┌─────────────────────────────────────────────────────────────────────┐
│  เทคโนโลยี        │ ทำไมเลือก                                       │
├───────────────────┼─────────────────────────────────────────────────┤
│ .NET 10 Preview   │ LTS ตัวถัดไป (ออก พ.ย. 2026) support 3 ปี       │
│                   │ มี AOT, performance ดีกว่า .NET 8/9 มาก          │
├───────────────────┼─────────────────────────────────────────────────┤
│ PostgreSQL 16     │ ฟรี, enterprise-grade, รองรับ JSON, concurrency  │
│                   │ ระบบธนาคารต้องการ ACID transaction ที่เข้มงวด     │
│                   │ row-level locking ดีกว่า MySQL สำหรับงาน finance  │
├───────────────────┼─────────────────────────────────────────────────┤
│ EF Core           │ ORM ของ .NET — เขียน C# แทน SQL ได้             │
│                   │ migration จัดการ schema เปลี่ยนแปลงง่าย           │
├───────────────────┼─────────────────────────────────────────────────┤
│ Clean Architecture│ แยก layer ชัดเจน — เปลี่ยน DB ได้โดยไม่กระทบ logic│
│                   │ ระบบธนาคารต้อง maintainable + testable           │
└─────────────────────────────────────────────────────────────────────┘
```

---

## ขั้นตอนที่ 1: ติดตั้ง PostgreSQL

### 🎯 ทำไมต้องสร้าง: Database คือที่เก็บข้อมูลหลัก — ยอดเงิน, ประวัติธุรกรรม, ข้อมูลผู้ใช้ ถ้าไม่มี DB ก็เก็บอะไรไม่ได้

### 1.1 ดาวน์โหลดและติดตั้ง

```
1. เปิด https://www.postgresql.org/download/windows/
2. คลิก "Download the installer"
3. เลือก Version 16.x (64-bit)
4. ดาวน์โหลด → รัน installer

ระหว่างติดตั้ง:
┌─────────────────────────────────────────────────┐
│ ☑ PostgreSQL Server   ← ตัว database เอง        │
│ ☑ pgAdmin 4           ← GUI จัดการ DB (ดูตาราง)  │
│ ☑ Command Line Tools  ← ใช้ psql ใน terminal     │
│                                                  │
│ Password: postgres123 (dev เท่านั้น!)             │
│ Port: 5432 (default)                             │
└─────────────────────────────────────────────────┘
```

### 1.2 สร้าง Database ผ่าน pgAdmin

```
1. เปิด pgAdmin 4 (Start Menu → pgAdmin 4)
2. ฝั่งซ้าย: Servers → PostgreSQL 16 → คลิก → ใส่ password
3. คลิกขวา Databases → Create → Database
   - Database: banking_db
   - Owner: postgres
   - คลิก Save

4. ทำซ้ำสร้าง banking_test (สำหรับ test อีก DB)

ทำไมต้อง 2 databases:
- banking_db    → ใช้ตอน develop (ข้อมูลจริง)
- banking_test  → ใช้ตอนรัน test (ลบสร้างใหม่ได้เลย ไม่กระทบข้อมูล develop)
```

### 1.3 ทดสอบ

```
ใน pgAdmin: คลิกขวา banking_db → Query Tool → พิมพ์:

SELECT NOW();

กด F5 → เห็นวันเวลา = ใช้งานได้!
```

---

## ขั้นตอนที่ 2: สร้าง Solution ใน Visual Studio 2026

### 🎯 ทำไมต้องสร้าง: Solution คือ "กล่องใหญ่" ที่รวม projects ย่อยทั้งหมดไว้ด้วยกัน เปิด 1 Solution = เปิดทั้งระบบ

### 2.1 สร้าง Solution เปล่า

```
1. เปิด Visual Studio 2026
2. Create a new project
3. ค้นหา: "Blank Solution"
4. เลือก: Blank Solution
5. ตั้งค่า:
   - Solution name: BankingSystem
   - Location: D:\Development\DevelopmentRoadmap\
6. คลิก Create
```

### 2.2 สร้าง Projects ย่อย (4 + 2 test)

```
ระบบใช้ Clean Architecture — แยกโค้ดเป็น 4 ชั้น:

┌─────────────────────────────────────────────────────────────┐
│                      Banking.Api                             │
│  ชั้นนอกสุด: รับ request จาก client                          │
│  📌 ทำไมต้องมี: เป็น "ประตูหน้า" ที่ client เรียกเข้ามา       │
│     → Controllers, Middleware, Authentication                │
├─────────────────────────────────────────────────────────────┤
│                  Banking.Infrastructure                      │
│  ชั้นที่ 3: เชื่อมต่อกับ "ของจริง" ข้างนอก                     │
│  📌 ทำไมต้องมี: code ที่คุยกับ Database, Redis, Email         │
│     → DbContext, Repositories, External Services             │
├─────────────────────────────────────────────────────────────┤
│                   Banking.Application                        │
│  ชั้นที่ 2: Business Logic / Use Cases                       │
│  📌 ทำไมต้องมี: เขียนกฎธุรกิจ "ฝากได้ไม่เกิน X" "ถอนต้องมีเงินพอ"│
│     → Commands, Queries, Validators, DTOs                    │
├─────────────────────────────────────────────────────────────┤
│                     Banking.Domain                           │
│  ชั้นในสุด: หัวใจของระบบ — ไม่พึ่งพาใคร                      │
│  📌 ทำไมต้องมี: กำหนด "ข้อมูลหน้าตาเป็นยังไง"                │
│     → Entities, Enums, Interfaces, Exceptions                │
└─────────────────────────────────────────────────────────────┘

ทำไมต้องแยก 4 ชั้น?
→ เปลี่ยน Database จาก PostgreSQL เป็น SQL Server?
  แก้แค่ Infrastructure ชั้นเดียว ไม่กระทบ Business Logic!
→ เขียน Unit Test? Mock แค่ Interface ได้เลย ไม่ต้องต่อ DB จริง!
→ ระบบธนาคารต้อง maintainable 5-10 ปี ต้องแยกชัดเจน!
```

#### สร้าง Banking.Domain (Class Library)

```
1. Solution Explorer → คลิกขวา Solution 'BankingSystem'
2. Add → New Project
3. ค้นหา: "Class Library"
4. เลือก: Class Library (C#)
5. ตั้งค่า:
   - Project name: Banking.Domain
   - Location: (ใช้ default — อยู่ใน BankingSystem folder)
6. Framework: .NET 10.0 (Preview)
7. คลิก Create
8. ลบไฟล์ Class1.cs ที่สร้างมาให้ (คลิกขวา → Delete)
```

**📌 สร้างเพื่ออะไร:** เป็น "พิมพ์เขียว" ของข้อมูล — User หน้าตาเป็นยังไง, Account มี field อะไร, Transaction เก็บค่าอะไรบ้าง

#### สร้าง Banking.Application (Class Library)

```
ทำเหมือน Domain:
Add → New Project → Class Library → ชื่อ: Banking.Application → .NET 10.0
ลบ Class1.cs
```

**📌 สร้างเพื่ออะไร:** เขียน "กฎธุรกิจ" ทั้งหมด เช่น ถอนเงินต้องเช็คยอดก่อน, โอนเงินต้องหักต้นทาง+เพิ่มปลายทาง, สมัครสมาชิกต้อง validate email

#### สร้าง Banking.Infrastructure (Class Library)

```
Add → New Project → Class Library → ชื่อ: Banking.Infrastructure → .NET 10.0
ลบ Class1.cs
```

**📌 สร้างเพื่ออะไร:** เชื่อมต่อกับ "โลกภายนอก" — PostgreSQL database, Redis cache, Email service, SMS service ทุกอย่างที่ต้องคุยกับระบบอื่นอยู่ที่นี่

#### สร้าง Banking.Api (ASP.NET Core Web API)

```
1. Add → New Project
2. ค้นหา: "ASP.NET Core Web API"
3. เลือก: ASP.NET Core Web API (C#)
4. ตั้งค่า:
   - Project name: Banking.Api
   - Framework: .NET 10.0 (Preview)
   - Authentication type: None (เราจะทำ JWT เอง)
   - ☑ Configure for HTTPS
   - ☑ Enable OpenAPI support
   - ☑ Use controllers
   - ☐ Enable container support (ยังไม่ต้อง)
5. คลิก Create
```

**📌 สร้างเพื่ออะไร:** เป็น "ประตูหน้า" — client (Next.js) จะเรียก API endpoints ที่นี่ เช่น POST /api/deposit, POST /api/withdraw

#### สร้าง Test Projects

```
สร้าง 2 โปรเจกต์:

1. Add → New Project → xUnit Test Project → ชื่อ: Banking.Tests.Unit → .NET 10.0
2. Add → New Project → xUnit Test Project → ชื่อ: Banking.Tests.Integration → .NET 10.0
```

**📌 ทำไมต้องมี Test 2 แบบ:**
- **Unit Test** = ทดสอบ logic เดี่ยวๆ (เช่น "ฟังก์ชันคำนวณดอกเบี้ยถูกไหม") — เร็ว, ไม่ต้องต่อ DB
- **Integration Test** = ทดสอบว่าทุกส่วนทำงานด้วยกันได้ (เช่น "API ฝากเงินจริง → เงินเข้า DB จริง") — ช้ากว่า, ต้องต่อ DB

### 2.3 ตั้ง Project References

```
📌 ทำไมต้องมี: บอกว่า project ไหน "เห็น" project ไหนได้
   Domain ไม่เห็นใคร (อิสระที่สุด)
   Application เห็น Domain (ใช้ Entity ที่ Domain สร้าง)
   Infrastructure เห็น Application + Domain (implement interfaces)
   Api เห็น Application + Infrastructure (ประกอบทุกอย่างเข้าด้วยกัน)

วิธีทำใน Visual Studio:

1. Banking.Application:
   คลิกขวา Dependencies → Add Project Reference → ☑ Banking.Domain → OK

2. Banking.Infrastructure:
   คลิกขวา Dependencies → Add Project Reference
   → ☑ Banking.Domain
   → ☑ Banking.Application
   → OK

3. Banking.Api:
   คลิกขวา Dependencies → Add Project Reference
   → ☑ Banking.Application
   → ☑ Banking.Infrastructure
   → OK

4. Banking.Tests.Unit:
   คลิกขวา Dependencies → Add Project Reference
   → ☑ Banking.Domain
   → ☑ Banking.Application
   → OK

5. Banking.Tests.Integration:
   คลิกขวา Dependencies → Add Project Reference
   → ☑ Banking.Api
   → OK
```

### 2.4 ติดตั้ง NuGet Packages

```
📌 ทำไมต้องมี NuGet: เหมือน npm ของ Node.js — ดาวน์โหลด libraries
   ที่คนอื่นเขียนไว้แล้ว ไม่ต้องเขียนเองทุกอย่าง

วิธีติดตั้งใน Visual Studio:
  คลิกขวา Project → Manage NuGet Packages → Browse → ค้นหาชื่อ → Install
```

#### Banking.Domain — ไม่ต้องติดตั้งอะไร

```
📌 ทำไม: Domain เป็น pure C# — ไม่พึ่งพา library ภายนอก
   ถ้า Domain ติดตั้ง library = ผูกมัดกับ library นั้น = ผิดหลัก Clean Architecture
```

#### Banking.Application

```
ติดตั้ง:
1. MediatR                          → จัดการ Command/Query pattern (ส่งคำสั่ง "ฝากเงิน" ไปหา handler ถูกตัว)
2. FluentValidation                 → ตรวจสอบข้อมูล (email ถูกรูปแบบไหม, จำนวนเงิน > 0 ไหม)
3. Microsoft.EntityFrameworkCore    → ใช้ interface ของ EF Core (ยังไม่ได้ต่อ DB จริง)

📌 ทำไมใช้ MediatR: แทนที่จะเรียก service ตรงๆ → ส่ง "คำสั่ง" (Command) แล้ว MediatR หา handler ให้
   ข้อดี: Controller ไม่ต้องรู้ว่า logic อยู่ที่ไหน → แยกส่วนชัดเจน
```

#### Banking.Infrastructure

```
ติดตั้ง:
1. Npgsql.EntityFrameworkCore.PostgreSQL  → ให้ EF Core คุยกับ PostgreSQL ได้
2. Microsoft.EntityFrameworkCore.Design   → สร้าง migration ได้
3. Microsoft.EntityFrameworkCore.Tools    → เครื่องมือ EF Core ใน VS
4. BCrypt.Net-Next                        → เข้ารหัส password (ห้ามเก็บ password เปล่า!)
5. StackExchange.Redis                    → เชื่อมต่อ Redis (ใช้ Phase ถัดไป)

📌 ทำไมใช้ BCrypt: เข้ารหัสแบบ one-way + salt
   ถ้า hacker ได้ database → เห็นแค่ hash → ถอดรหัสกลับไม่ได้!
```

#### Banking.Api

```
ติดตั้ง:
1. Serilog.AspNetCore                                → logging ดีกว่า built-in (เก็บ log เป็นไฟล์, ส่งไป Seq)
2. Serilog.Sinks.Console                             → แสดง log ใน console
3. Serilog.Sinks.File                                → เขียน log ลงไฟล์
4. Microsoft.AspNetCore.Authentication.JwtBearer     → ตรวจสอบ JWT token
5. FluentValidation.AspNetCore                       → auto-validate request ที่เข้ามา
6. Swashbuckle.AspNetCore                            → สร้าง Swagger UI (หน้าทดสอบ API)

📌 ทำไมใช้ Serilog: ระบบธนาคารต้อง log ทุกอย่าง — ใครทำอะไร เมื่อไหร่
   เวลามีปัญหา "เงินหาย" → ดู log ตามรอยได้ทุก transaction
```

#### Banking.Tests.Unit

```
ติดตั้ง:
1. Moq                → จำลอง (mock) database, services สำหรับ test
2. FluentAssertions   → เขียน assert อ่านง่าย: result.Should().Be(100)
3. Bogus              → สร้างข้อมูลปลอมสำหรับ test (ชื่อ, email, เลขบัญชี)

📌 ทำไมใช้ Moq: Unit test ไม่ควรต่อ DB จริง
   Moq จำลอง DB ขึ้นมา → test เร็ว + ไม่กระทบข้อมูลจริง
```

#### Banking.Tests.Integration

```
ติดตั้ง:
1. Microsoft.AspNetCore.Mvc.Testing  → รัน API จริงใน memory (ไม่ต้อง start server)
2. FluentAssertions                  → assert อ่านง่าย
3. Respawn                           → reset database กลับสู่สถานะเริ่มต้นก่อนทุก test

📌 ทำไมใช้ Respawn: แต่ละ test ต้องเริ่มจาก DB เปล่า
   ถ้า test A ใส่ข้อมูล → test B อาจพังเพราะข้อมูลของ A
   Respawn ลบข้อมูลทั้งหมดระหว่าง test → แต่ละ test เป็นอิสระ
```

#### ติดตั้ง EF Core CLI Tool

```
📌 ทำไมต้องมี: ใช้สร้าง migration (แปลง C# Entity → SQL สร้างตาราง)

ใน Visual Studio:
  Tools → NuGet Package Manager → Package Manager Console

พิมพ์:
  dotnet tool install --global dotnet-ef
```

---

## ขั้นตอนที่ 3: สร้าง Domain Layer (Entities)

### 🎯 ทำไมต้องสร้าง: กำหนด "โครงสร้างข้อมูล" — เหมือนออกแบบฟอร์มก่อนใช้จริง ถ้าไม่มี Entity ก็ไม่รู้ว่าจะเก็บข้อมูลอะไรบ้าง

### 3.1 สร้าง Folder Structure

```
คลิกขวา Banking.Domain → Add → New Folder สร้างทีละ folder:

Banking.Domain/
├── Entities/       ← โครงสร้างข้อมูล (User, Account, Transaction)
├── Enums/          ← ค่าคงที่ (ประเภทบัญชี, สถานะธุรกรรม)
├── Exceptions/     ← ข้อผิดพลาดเฉพาะธุรกิจ (เงินไม่พอ, บัญชีถูกล็อค)
└── Interfaces/     ← สัญญา (contract) ว่า repository ต้องทำอะไรได้
```

### 3.2 BaseEntity — แม่แบบของทุก Entity

```
📌 สร้างเพื่ออะไร: ทุกตารางในระบบต้องมี Id, CreatedAt, UpdatedAt
   แทนที่จะเขียนซ้ำทุก Entity → เขียนครั้งเดียวที่ BaseEntity แล้ว inherit

คลิกขวา Entities folder → Add → Class → ชื่อ: BaseEntity.cs
```

```csharp
namespace Banking.Domain.Entities;

// abstract = สร้าง instance ตรงๆ ไม่ได้ ต้อง inherit ไปใช้
public abstract class BaseEntity
{
    // Id: ใช้ Guid แทน int เพราะ:
    // - สร้างฝั่ง client ได้ (ไม่ต้องรอ DB generate)
    // - ไม่ชนกันเมื่อรวม database หลายตัว
    // - ไม่บอก hacker ว่ามี record กี่อัน (int บอกได้: user/5 = มี 5+ users)
    public Guid Id { get; set; } = Guid.NewGuid();

    // CreatedAt: บันทึกว่าสร้างเมื่อไหร่ (ใช้ UTC เสมอ — เทียบเวลาได้ทุก timezone)
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // UpdatedAt: บันทึกว่าแก้ไขล่าสุดเมื่อไหร่ (null = ยังไม่เคยแก้)
    public DateTime? UpdatedAt { get; set; }

    // IsDeleted: Soft Delete — ไม่ลบจริง แค่ซ่อน
    // ระบบธนาคารห้ามลบข้อมูลจริง! ต้องเก็บไว้ตรวจสอบ audit
    public bool IsDeleted { get; set; } = false;
}
```

### 3.3 Enums — ค่าคงที่ของระบบ

```
📌 สร้างเพื่ออะไร: แทนที่จะเก็บ string "savings", "checking" (พิมพ์ผิดได้!)
   ใช้ enum บังคับให้เลือกจากตัวเลือกที่กำหนดเท่านั้น → ปลอดภัย ไม่ผิดพลาด

คลิกขวา Enums folder → Add → Class → ชื่อ: Enums.cs
```

```csharp
namespace Banking.Domain.Enums;

// ประเภทบัญชี — ลูกค้าเปิดบัญชีอะไรได้บ้าง
public enum AccountType
{
    Savings,        // ออมทรัพย์ — ดอกเบี้ยสูง, ถอนจำกัด
    Checking,       // กระแสรายวัน — ไม่มีดอกเบี้ย, ถอนไม่จำกัด
    FixedDeposit    // ฝากประจำ — ดอกเบี้ยสูงสุด, ถอนก่อนกำหนดเสียค่าปรับ
}

// สถานะบัญชี
public enum AccountStatus
{
    Active,   // ใช้งานปกติ
    Frozen,   // ถูกระงับ (ต้องสอบ, หรือมีปัญหาทางกฎหมาย)
    Closed    // ปิดบัญชีแล้ว
}

// ประเภทธุรกรรม — ทุกการเคลื่อนไหวของเงิน
public enum TransactionType
{
    Deposit,        // ฝากเงิน (เงินเข้า)
    Withdrawal,     // ถอนเงิน (เงินออก)
    TransferIn,     // รับโอน (เงินเข้าจากบัญชีอื่น)
    TransferOut,    // โอนออก (เงินออกไปบัญชีอื่น)
    Fee,            // ค่าธรรมเนียม (ถูกหัก)
    Interest        // ดอกเบี้ย (ได้รับ)
}

// สถานะธุรกรรม — ติดตามว่าธุรกรรมอยู่ step ไหน
public enum TransactionStatus
{
    Pending,      // รอดำเนินการ (เพิ่งสร้าง)
    Processing,   // กำลังประมวลผล (อยู่ระหว่างทำ)
    Completed,    // สำเร็จ ✅
    Failed,       // ล้มเหลว ❌ (เงินไม่พอ, ระบบ error)
    Reversed      // ถูกย้อนกลับ (ยกเลิกธุรกรรม)
}

// สถานะยืนยันตัวตน (KYC = Know Your Customer)
// ระบบธนาคารจริงต้องยืนยันตัวตนก่อนใช้งาน (กฎหมาย!)
public enum KycStatus
{
    Pending,    // รอตรวจสอบ
    Verified,   // ผ่านแล้ว ✅
    Rejected    // ไม่ผ่าน ❌
}
```

### 3.4 User Entity — ข้อมูลผู้ใช้

```
📌 สร้างเพื่ออะไร: เก็บข้อมูลลูกค้า — ชื่อ, email, รหัสผ่าน, สถานะ KYC
   User 1 คน มีได้หลาย Account (1:N)

คลิกขวา Entities → Add → Class → ชื่อ: User.cs
```

```csharp
using Banking.Domain.Enums;

namespace Banking.Domain.Entities;

public class User : BaseEntity  // ← inherit จาก BaseEntity (ได้ Id, CreatedAt, etc.)
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;

    // เก็บ hash ของ password (ไม่เก็บ password จริง!)
    // ทำไม: ถ้า hacker ได้ DB → เห็นแค่ hash → ถอดกลับไม่ได้
    public string PasswordHash { get; set; } = string.Empty;

    // เลขบัตรประชาชน (เก็บเป็น hash เช่นกัน — ข้อมูลอ่อนไหวมาก!)
    public string? NationalIdHash { get; set; }

    // KYC: ธนาคารต้องยืนยันตัวตนลูกค้าตามกฎหมาย
    public KycStatus KycStatus { get; set; } = KycStatus.Pending;

    public bool IsActive { get; set; } = true;

    // ระบบล็อค: ป้องกัน brute force (พยายาม login ผิดซ้ำๆ)
    public bool IsLocked { get; set; } = false;
    public int FailedLoginAttempts { get; set; } = 0;   // นับครั้งที่ login ผิด
    public DateTime? LastLoginAt { get; set; }

    // Navigation Property: 1 User มีหลาย Accounts
    // ทำไมต้องมี: ให้ EF Core สร้าง relationship ใน DB อัตโนมัติ
    public ICollection<Account> Accounts { get; set; } = new List<Account>();

    public string FullName => $"{FirstName} {LastName}";
}
```

### 3.5 Account Entity — บัญชีธนาคาร

```
📌 สร้างเพื่ออะไร: แต่ละบัญชีมียอดเงิน (Balance), ประเภท, วงเงินถอน
   Account 1 บัญชี มีหลาย Transactions (1:N)

Entities → Add → Class → ชื่อ: Account.cs
```

```csharp
using Banking.Domain.Enums;
// 🔴 ห้ามใส่ using System.Transactions; — จะไปดึง Transaction ผิดตัว!

namespace Banking.Domain.Entities;

public class Account : BaseEntity
{
    // FK: บัญชีนี้เป็นของ User ไหน
    public Guid UserId { get; set; }

    // เลขบัญชี (unique) — format: 1234-5678-9012
    public string AccountNumber { get; set; } = string.Empty;

    public AccountType Type { get; set; } = AccountType.Savings;
    public string Currency { get; set; } = "THB";  // 🔴 ชื่อ property คือ Currency (ไม่ใช่ Credits)

    // Balance: ยอดเงินจริงในบัญชี
    // AvailableBalance: ยอดที่ถอนได้ (อาจน้อยกว่า Balance ถ้ามียอดค้าง)
    // ทำไมต้องแยก: เวลาโอนเงิน → หักจาก Available ก่อน (hold) → พอสำเร็จค่อยหัก Balance
    public decimal Balance { get; set; } = 0;              // 🔴 ห้ามลืม! ยอดเงินจริง
    public decimal AvailableBalance { get; set; } = 0;

    // วงเงินถอนต่อวัน — ป้องกันถอนเงินจำนวนมากผิดปกติ
    public decimal DailyWithdrawalLimit { get; set; } = 50_000;  // 🔴 ห้ามลืม! วงเงินถอนต่อวัน

    public AccountStatus Status { get; set; } = AccountStatus.Active;

    // Navigation Properties
    public User User { get; set; } = null!;
    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
}
```

### 3.6 Transaction Entity — ประวัติธุรกรรม

```
📌 สร้างเพื่ออะไร: บันทึก "ทุกการเคลื่อนไหว" ของเงิน
   ระบบธนาคารต้องเก็บ audit trail — ใครทำอะไร เมื่อไหร่ เท่าไหร่
   ห้ามลบ ห้ามแก้ — เก็บตลอดไป!

Entities → Add → Class → ชื่อ: Transaction.cs
```

```csharp
using Banking.Domain.Enums;

namespace Banking.Domain.Entities;

public class Transaction : BaseEntity
{
    // เลขอ้างอิง (unique) — ใช้ติดตามธุรกรรม เช่น TXN-20260328-ABC123
    public string ReferenceNumber { get; set; } = string.Empty;

    public Guid AccountId { get; set; }
    public TransactionType Type { get; set; }
    public decimal Amount { get; set; }

    // บันทึกยอดก่อน-หลังทำธุรกรรม — สำคัญมากสำหรับ audit!
    // ทำไม: ถ้ามีปัญหา → ดูว่ายอดเปลี่ยนจากเท่าไหร่เป็นเท่าไหร่ได้ทันที
    public decimal BalanceBefore { get; set; }
    public decimal BalanceAfter { get; set; }

    public TransactionStatus Status { get; set; } = TransactionStatus.Pending;
    public string? Description { get; set; }

    // เชื่อมกับธุรกรรมคู่ (เช่น โอนเงิน: ฝั่งหัก ↔ ฝั่งเข้า)
    public Guid? RelatedTransactionId { get; set; }

    // Metadata: ข้อมูลเพิ่มเติม เก็บเป็น JSON (ยืดหยุ่น)
    public string? Metadata { get; set; }

    // บันทึก IP ที่ทำธุรกรรม — security + fraud detection
    public string? IpAddress { get; set; }

    // Navigation Properties
    public Account Account { get; set; } = null!;
    public Transaction? RelatedTransaction { get; set; }
}
```

### 3.7 Transfer Entity — การโอนเงิน

```
📌 สร้างเพื่ออะไร: โอนเงินซับซ้อนกว่าฝาก/ถอน — มี 2 ฝั่ง (ต้นทาง + ปลายทาง)
   ต้องทำ 2 transactions (หัก + เพิ่ม) ที่ผูกกัน → ถ้าอันหนึ่งพัง ต้องย้อนกลับทั้งคู่!

Entities → Add → Class → ชื่อ: Transfer.cs
```

```csharp
using Banking.Domain.Enums;

namespace Banking.Domain.Entities;

public class Transfer : BaseEntity
{
    public Guid FromAccountId { get; set; }       // บัญชีต้นทาง
    public Guid ToAccountId { get; set; }         // บัญชีปลายทาง
    public decimal Amount { get; set; }
    public decimal Fee { get; set; } = 0;         // ค่าธรรมเนียม (ข้ามธนาคาร = มี fee)
    public TransactionStatus Status { get; set; } = TransactionStatus.Pending;

    // เชื่อมกับ transactions ทั้ง 2 ฝั่ง
    public Guid? DebitTransactionId { get; set; }   // ธุรกรรมหัก (ต้นทาง)
    public Guid? CreditTransactionId { get; set; }  // ธุรกรรมเข้า (ปลายทาง)

    // Navigation Properties
    public Account FromAccount { get; set; } = null!;
    public Account ToAccount { get; set; } = null!;
    public Transaction? DebitTransaction { get; set; }
    public Transaction? CreditTransaction { get; set; }
}
```

### 3.8 AuditLog Entity — บันทึกการกระทำทั้งหมด

```
📌 สร้างเพื่ออะไร: เก็บ "ใครทำอะไร" ทุกอย่างในระบบ
   ระบบธนาคารจริงต้องมี audit log ตามกฎหมาย (พ.ร.บ.คุ้มครองข้อมูลส่วนบุคคล / PDPA)
   - admin เปลี่ยนสถานะบัญชี → บันทึก
   - user เปลี่ยน password → บันทึก
   - ทุกการแก้ไขข้อมูล → บันทึก

Entities → Add → Class → ชื่อ: AuditLog.cs
```

```csharp
namespace Banking.Domain.Entities;

public class AuditLog
{
    public long Id { get; set; }                  // ใช้ long เพราะจะมีเยอะมาก
    public Guid? UserId { get; set; }             // ใครทำ (null = system)
    public string Action { get; set; } = string.Empty;    // ทำอะไร: "Create", "Update", "Delete"
    public string EntityType { get; set; } = string.Empty; // ทำกับอะไร: "Account", "User"
    public string? EntityId { get; set; }         // ID ของสิ่งที่ถูกทำ
    public string? OldValues { get; set; }        // ค่าเก่า (JSON) — เปรียบเทียบได้
    public string? NewValues { get; set; }        // ค่าใหม่ (JSON)
    public string? IpAddress { get; set; }        // IP ที่ทำ
    public string? UserAgent { get; set; }        // Browser/App ที่ใช้
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
```

### 3.9 Custom Exceptions — ข้อผิดพลาดเฉพาะธุรกิจ

```
📌 สร้างเพื่ออะไร: แทนที่จะ throw Exception กว้างๆ → สร้าง exception เฉพาะ
   ทำให้ API ส่ง error message ที่ชัดเจนกลับไปหา client
   เช่น: InsufficientFundsException → API return "เงินไม่พอ" แทน "Internal Server Error"

Exceptions → Add → Class → ชื่อ: DomainExceptions.cs
```

```csharp
namespace Banking.Domain.Exceptions;

// หาไม่เจอ (User, Account ที่ระบุ id ไม่มีในระบบ)
public class NotFoundException : Exception
{
    public NotFoundException(string entity, object id)
        : base($"{entity} with id '{id}' was not found.") { }
}

// เงินไม่พอ (ถอน/โอนเกินยอดที่มี)
public class InsufficientFundsException : Exception
{
    public InsufficientFundsException(decimal balance, decimal amount)
        : base($"Insufficient funds. Balance: {balance:N2}, Requested: {amount:N2}") { }
}

// บัญชีถูกระงับ (frozen — ห้ามทำธุรกรรม)
public class AccountFrozenException : Exception
{
    public AccountFrozenException(string accountNumber)
        : base($"Account '{accountNumber}' is frozen.") { }
}

// ถอนเกินวงเงินต่อวัน
public class DailyLimitExceededException : Exception
{
    public DailyLimitExceededException(decimal limit, decimal todayTotal, decimal requested)
        : base($"Daily limit exceeded. Limit: {limit:N2}, Today: {todayTotal:N2}, Requested: {requested:N2}") { }
}

// ข้อมูลซ้ำ (email ซ้ำ, เลขบัญชีซ้ำ)
public class DuplicateException : Exception
{
    public DuplicateException(string message) : base(message) { }
}

// บัญชีถูกล็อค (login ผิดหลายครั้ง)
public class AccountLockedException : Exception
{
    public AccountLockedException() : base("Account is locked due to too many failed attempts.") { }
}
```

### 3.10 Repository Interfaces — สัญญาว่า "ต้องทำอะไรได้"

```
📌 สร้างเพื่ออะไร: กำหนด "สัญญา" (contract) ว่า database layer ต้องทำอะไรได้บ้าง
   Application layer ใช้ interface นี้ — ไม่สนว่าใช้ PostgreSQL, SQL Server, หรือ MongoDB

   ทำไมสำคัญ: ถ้าวันหนึ่งเปลี่ยน database → แก้แค่ Infrastructure ที่ implement interface
   Application layer ไม่ต้องแก้เลย!

Interfaces → Add → Class → ชื่อ: IRepositories.cs
```

```csharp
using Banking.Domain.Entities;

namespace Banking.Domain.Interfaces;

// Generic Repository — ทุก Entity ต้องทำ CRUD ได้
public interface IRepository<T> where T : BaseEntity
{
    Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<T>> GetAllAsync(CancellationToken ct = default);
    Task AddAsync(T entity, CancellationToken ct = default);
    void Update(T entity);
    void Remove(T entity);  // soft delete
}

// User-specific: เพิ่ม method ที่ User ต้องการเฉพาะ
public interface IUserRepository : IRepository<User>
{
    Task<User?> GetByEmailAsync(string email, CancellationToken ct = default);
    Task<bool> EmailExistsAsync(string email, CancellationToken ct = default);
    Task<bool> PhoneExistsAsync(string phone, CancellationToken ct = default);
}

// Account-specific
public interface IAccountRepository : IRepository<Account>
{
    Task<Account?> GetByAccountNumberAsync(string accountNumber, CancellationToken ct = default);
    Task<List<Account>> GetByUserIdAsync(Guid userId, CancellationToken ct = default);
    // GetForUpdate: ล็อค row ตอนอ่าน — ป้องกัน 2 คนถอนเงินพร้อมกัน!
    Task<Account?> GetByIdForUpdateAsync(Guid id, CancellationToken ct = default);
    Task<bool> AccountNumberExistsAsync(string accountNumber, CancellationToken ct = default);
}

// Transaction-specific
public interface ITransactionRepository : IRepository<Transaction>
{
    Task<List<Transaction>> GetByAccountIdAsync(Guid accountId, int page, int pageSize, CancellationToken ct = default);
    Task<int> GetCountByAccountIdAsync(Guid accountId, CancellationToken ct = default);
    // รวมยอดถอนวันนี้ — เช็ควงเงินต่อวัน
    Task<decimal> GetTodayWithdrawalTotalAsync(Guid accountId, CancellationToken ct = default);
}

// Unit of Work: รวม repositories + จัดการ transaction
// ทำไมต้องมี: โอนเงินต้องหัก Account A + เพิ่ม Account B ใน transaction เดียวกัน
// ถ้าอันหนึ่งพัง → rollback ทั้งคู่ (ไม่มีกรณีหักแล้วไม่เข้า!)
public interface IUnitOfWork
{
    IUserRepository Users { get; }
    IAccountRepository Accounts { get; }
    ITransactionRepository Transactions { get; }
    Task<int> SaveChangesAsync(CancellationToken ct = default);
    Task BeginTransactionAsync(CancellationToken ct = default);
    Task CommitTransactionAsync(CancellationToken ct = default);
    Task RollbackTransactionAsync(CancellationToken ct = default);
}
```

---

## ขั้นตอนที่ 4: สร้าง Infrastructure Layer (Database Config)

### 🎯 ทำไมต้องสร้าง: บอก EF Core ว่า Entity แต่ละตัวจะสร้างตารางยังไง — ชื่อ column, ขนาด, index, constraint

### 4.1 สร้าง Folder Structure

```
Banking.Infrastructure/
├── Data/
│   ├── AppDbContext.cs              ← "ประตู" เข้า database
│   ├── Configurations/              ← บอกว่าตารางหน้าตาเป็นยังไง
│   │   ├── UserConfiguration.cs
│   │   ├── AccountConfiguration.cs
│   │   ├── TransactionConfiguration.cs
│   │   ├── TransferConfiguration.cs
│   │   └── AuditLogConfiguration.cs
│   └── Seeds/
│       └── DataSeeder.cs            ← ข้อมูลเริ่มต้น
```

### 4.2 AppDbContext

```
📌 สร้างเพื่ออะไร: DbContext คือ "ประตูหลัก" ที่ C# คุยกับ database
   ทุกครั้งที่อ่าน/เขียน database ต้องผ่าน DbContext
   + Auto-update timestamps (CreatedAt, UpdatedAt)

Data → Add → Class → ชื่อ: AppDbContext.cs
```

```csharp
using Banking.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Banking.Infrastructure.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    // DbSet = ตาราง 1 ตัว → แต่ละ DbSet จะกลายเป็น 1 table ใน DB
    public DbSet<User> Users => Set<User>();
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<Transfer> Transfers => Set<Transfer>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // โหลด Configuration ทั้งหมดอัตโนมัติ (ไม่ต้องเพิ่มทีละตัว)
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }

    // Override SaveChanges เพื่อ auto-update timestamps
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = DateTime.UtcNow;
                    break;
                case EntityState.Modified:
                    entry.Entity.UpdatedAt = DateTime.UtcNow;
                    break;
            }
        }
        return await base.SaveChangesAsync(cancellationToken);
    }
}
```

### 4.3 Entity Configurations (Fluent API)

```
📌 สร้างเพื่ออะไร: กำหนดรายละเอียดของตาราง — column size, unique index,
   foreign key, check constraint ทั้งหมดกำหนดที่นี่
   ทำไมไม่ใช้ Data Annotations ([Required], [MaxLength])?
   → Fluent API ยืดหยุ่นกว่า + Domain ไม่ต้องพึ่ง EF Core library
```

#### UserConfiguration.cs

```
Configurations → Add → Class → ชื่อ: UserConfiguration.cs
```

```csharp
using Banking.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Banking.Infrastructure.Data.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");   // ชื่อตาราง (lowercase — convention ของ PostgreSQL)
        builder.HasKey(u => u.Id);

        builder.Property(u => u.FirstName).HasMaxLength(100).IsRequired();
        builder.Property(u => u.LastName).HasMaxLength(100).IsRequired();

        // Email ต้อง unique — สมัครซ้ำไม่ได้!
        builder.Property(u => u.Email).HasMaxLength(255).IsRequired();
        builder.HasIndex(u => u.Email).IsUnique();

        // Phone ต้อง unique
        builder.Property(u => u.Phone).HasMaxLength(20).IsRequired();
        builder.HasIndex(u => u.Phone).IsUnique();

        builder.Property(u => u.PasswordHash).HasMaxLength(255).IsRequired();
        builder.Property(u => u.NationalIdHash).HasMaxLength(255);

        // เก็บ enum เป็น string ใน DB (อ่านง่ายกว่าเลข)
        builder.Property(u => u.KycStatus).HasConversion<string>().HasMaxLength(20);

        // Soft delete: query ปกติจะไม่เห็น record ที่ IsDeleted=true
        builder.HasQueryFilter(u => !u.IsDeleted);
    }
}
```

#### AccountConfiguration.cs

```csharp
using Banking.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Banking.Infrastructure.Data.Configurations;

public class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> builder)
    {
        builder.ToTable("accounts");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.AccountNumber).HasMaxLength(20).IsRequired();
        builder.HasIndex(a => a.AccountNumber).IsUnique();

        builder.Property(a => a.Type).HasConversion<string>().HasMaxLength(20);
        builder.Property(a => a.Currency).HasMaxLength(3).HasDefaultValue("THB");

        // decimal precision: 18 หลัก, 2 ทศนิยม → สูงสุด 9,999,999,999,999,999.99
        builder.Property(a => a.Balance).HasPrecision(18, 2).HasDefaultValue(0);
        builder.Property(a => a.AvailableBalance).HasPrecision(18, 2).HasDefaultValue(0);
        builder.Property(a => a.DailyWithdrawalLimit).HasPrecision(18, 2).HasDefaultValue(50000);

        builder.Property(a => a.Status).HasConversion<string>().HasMaxLength(20);

        // CHECK CONSTRAINT: ยอดเงินห้ามติดลบ! (ป้องกันที่ DB level)
        builder.ToTable(t => t.HasCheckConstraint("CK_accounts_balance_positive", "\"Balance\" >= 0"));

        // Relationship: Account → User (Many:1)
        builder.HasOne(a => a.User)
            .WithMany(u => u.Accounts)
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.Restrict);  // ห้ามลบ User ถ้ายังมีบัญชีอยู่!

        builder.HasQueryFilter(a => !a.IsDeleted);
        builder.HasIndex(a => a.UserId);
        builder.HasIndex(a => a.Status);
    }
}
```

#### TransactionConfiguration.cs

```csharp
using Banking.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Banking.Infrastructure.Data.Configurations;

public class TransactionConfiguration : IEntityTypeConfiguration<Transaction>
{
    public void Configure(EntityTypeBuilder<Transaction> builder)
    {
        builder.ToTable("transactions");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.ReferenceNumber).HasMaxLength(50).IsRequired();
        builder.HasIndex(t => t.ReferenceNumber).IsUnique();

        builder.Property(t => t.Type).HasConversion<string>().HasMaxLength(20);
        builder.Property(t => t.Amount).HasPrecision(18, 2);
        builder.Property(t => t.BalanceBefore).HasPrecision(18, 2);
        builder.Property(t => t.BalanceAfter).HasPrecision(18, 2);
        builder.Property(t => t.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(t => t.Description).HasMaxLength(500);
        builder.Property(t => t.IpAddress).HasMaxLength(45);

        builder.HasOne(t => t.Account)
            .WithMany(a => a.Transactions)
            .HasForeignKey(t => t.AccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.RelatedTransaction)
            .WithOne()
            .HasForeignKey<Transaction>(t => t.RelatedTransactionId)
            .OnDelete(DeleteBehavior.SetNull);

        // Composite Index: ค้นหา transactions ของบัญชี + ช่วงเวลา (ใช้บ่อยมาก!)
        builder.HasIndex(t => new { t.AccountId, t.CreatedAt });
        builder.HasIndex(t => t.AccountId);
        builder.HasIndex(t => t.CreatedAt);

        builder.HasQueryFilter(t => !t.IsDeleted);
    }
}
```

#### TransferConfiguration.cs

```csharp
using Banking.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Banking.Infrastructure.Data.Configurations;

public class TransferConfiguration : IEntityTypeConfiguration<Transfer>
{
    public void Configure(EntityTypeBuilder<Transfer> builder)
    {
        builder.ToTable("transfers");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Amount).HasPrecision(18, 2);
        builder.Property(t => t.Fee).HasPrecision(18, 2).HasDefaultValue(0);
        builder.Property(t => t.Status).HasConversion<string>().HasMaxLength(20);

        builder.HasOne(t => t.FromAccount).WithMany()
            .HasForeignKey(t => t.FromAccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(t => t.ToAccount).WithMany()
            .HasForeignKey(t => t.ToAccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(t => t.DebitTransaction).WithOne()
            .HasForeignKey<Transfer>(t => t.DebitTransactionId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(t => t.CreditTransaction).WithOne()
            .HasForeignKey<Transfer>(t => t.CreditTransactionId).OnDelete(DeleteBehavior.SetNull);

        builder.HasQueryFilter(t => !t.IsDeleted);
    }
}
```

#### AuditLogConfiguration.cs

```csharp
using Banking.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Banking.Infrastructure.Data.Configurations;

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("audit_logs");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).UseIdentityAlwaysColumn(); // auto-increment

        builder.Property(a => a.Action).HasMaxLength(50).IsRequired();
        builder.Property(a => a.EntityType).HasMaxLength(100).IsRequired();
        builder.Property(a => a.EntityId).HasMaxLength(100);
        builder.Property(a => a.IpAddress).HasMaxLength(45);
        builder.Property(a => a.UserAgent).HasMaxLength(500);

        // JSONB: PostgreSQL เก็บ JSON แบบ binary (ค้นหาข้างใน JSON ได้เร็ว!)
        builder.Property(a => a.OldValues).HasColumnType("jsonb");
        builder.Property(a => a.NewValues).HasColumnType("jsonb");

        builder.HasIndex(a => a.UserId);
        builder.HasIndex(a => a.CreatedAt);
        builder.HasIndex(a => new { a.EntityType, a.EntityId });
    }
}
```

---

## ขั้นตอนที่ 5: Connection String + Migration

### 🎯 ทำไมต้องสร้าง: บอก App ว่าต่อ database ที่ไหน + แปลง C# Entities เป็นตารางจริงใน PostgreSQL

### 5.1 ตั้ง Connection String

```
แก้ไฟล์: Banking.Api → appsettings.Development.json (ดับเบิลคลิก)
```

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=banking_db;Username=postgres;Password=postgres123"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.EntityFrameworkCore.Database.Command": "Information"
    }
  }
}
```

### 5.2 ลงทะเบียน DbContext ใน Program.cs

```
แก้ไฟล์: Banking.Api → Program.cs
```

```csharp
using Banking.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ===== Database: บอก App ว่าใช้ PostgreSQL =====
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        npgsqlOptions =>
        {
            npgsqlOptions.MigrationsAssembly("Banking.Infrastructure");
            npgsqlOptions.EnableRetryOnFailure(3);  // ถ้า DB ตัดสาย → retry 3 ครั้ง
            npgsqlOptions.CommandTimeout(30);        // timeout 30 วินาที
        }));

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.Run();
```

### 5.3 สร้าง Migration

```
📌 Migration คืออะไร: แปลง C# Entity → SQL สร้างตาราง
   เหมือน "version control ของ database" — ย้อนกลับได้ ดูประวัติเปลี่ยนแปลงได้

ใน Visual Studio:
  Tools → NuGet Package Manager → Package Manager Console

เลือก Default project: Banking.Infrastructure

พิมพ์:
  Add-Migration InitialCreate -StartupProject Banking.Api

ถ้าสำเร็จ: เห็น folder Migrations ใน Banking.Infrastructure

จากนั้นสร้างตารางจริง:
  Update-Database -StartupProject Banking.Api

ตรวจสอบ: เปิด pgAdmin → banking_db → Schemas → Tables
จะเห็น 5 ตาราง: users, accounts, transactions, transfers, audit_logs ✅
```

---

## ขั้นตอนที่ 6: Seed Data

### 🎯 ทำไมต้องสร้าง: ใส่ข้อมูลเริ่มต้นสำหรับ development — ไม่ต้องสร้าง user ใหม่ทุกครั้งที่ reset DB

```
Seeds → Add → Class → ชื่อ: DataSeeder.cs
```

```csharp
using Banking.Domain.Entities;
using Banking.Domain.Enums;

namespace Banking.Infrastructure.Data.Seeds;

public static class DataSeeder
{
    public static async Task SeedAsync(AppDbContext context)
    {
        if (context.Users.Any()) return;  // มีข้อมูลแล้ว → ข้าม

        // Demo user — ใช้ทดสอบ
        var demoUser = new User
        {
            Id = Guid.NewGuid(),
            FirstName = "สมชาย",
            LastName = "ใจดี",
            Email = "somchai@demo.com",
            Phone = "0812345678",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Demo123!"),
            KycStatus = KycStatus.Verified,
            IsActive = true
        };

        context.Users.Add(demoUser);
        await context.SaveChangesAsync();

        // บัญชีออมทรัพย์ — เริ่มต้น 100,000 บาท
        var savings = new Account
        {
            UserId = demoUser.Id,
            AccountNumber = "1234-5678-9012",
            Type = AccountType.Savings,
            Balance = 100_000,
            AvailableBalance = 100_000,
            Status = AccountStatus.Active
        };

        context.Accounts.Add(savings);
        await context.SaveChangesAsync();
    }
}
```

### เรียก Seeder ใน Program.cs

```csharp
// เพิ่มก่อน app.Run(); ใน Program.cs

if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await context.Database.MigrateAsync();        // auto-migrate
    await Banking.Infrastructure.Data.Seeds.DataSeeder.SeedAsync(context);
}

app.Run();
```

---

## ขั้นตอนที่ 7: ทดสอบ — รัน!

### 🎯 ทดสอบว่าทุกอย่างทำงานด้วยกันได้

```
1. กด F5 ใน Visual Studio (หรือ Ctrl+F5 ไม่ต้อง debug)
2. Browser จะเปิด Swagger UI ที่ https://localhost:xxxx/swagger
3. เปิด pgAdmin → banking_db → ดูว่ามีข้อมูล seed

ถ้าเห็น Swagger UI + มีข้อมูลใน DB = Phase 1 เสร็จสมบูรณ์! ✅
```

---

## Checklist ก่อนไป Phase 2

```
☐ PostgreSQL ติดตั้ง + banking_db สร้างแล้ว
☐ Solution มี 6 projects (Domain, Application, Infrastructure, Api, Tests×2)
☐ Project References ถูกต้อง
☐ NuGet Packages ติดตั้งครบ
☐ Domain: Entities 6 ตัว + Enums + Exceptions + Interfaces
☐ Infrastructure: DbContext + Configurations 5 ไฟล์
☐ Migration สร้างสำเร็จ + 5 ตารางใน DB
☐ Seed Data ทำงานได้
☐ API รันได้ + Swagger UI เปิดได้
☐ Build สำเร็จไม่มี error

✅ ครบ = พร้อมไป Phase 2: Core Banking Logic (ฝาก/ถอน/โอน)!
```
