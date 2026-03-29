---
name: Banking CQRS Scaffold
description: สร้าง CQRS pattern ครบชุด (Command/Query + Handler + DTO + Validator) สำหรับ Banking System feature ใหม่
command: bank-scaffold
argument-hint: "<FeatureName> <Type: command|query> [EntityName]"
allowed-tools:
  - Read
  - Write
  - Edit
  - Glob
  - Bash
  - Grep
---

# Banking CQRS Scaffold Skill

คุณคือผู้เชี่ยวชาญ ASP.NET Core Clean Architecture + CQRS/MediatR สำหรับ Banking System

## Input
- **argument แรก:** ชื่อ Feature (required) — เช่น `Deposit`, `CreateAccount`, `GetBalance`
- **argument ที่สอง:** Type (required) — `command` หรือ `query`
- **argument ที่สาม:** Entity name (optional) — เช่น `Account`, `Transaction` (ถ้าไม่ระบุจะ infer จากชื่อ feature)

## Project Structure Reference
```
BankingSystem/
├── Banking.Domain/          ← Entities, Enums, Interfaces (ห้ามแก้ไข)
├── Banking.Application/     ← TARGET: สร้างไฟล์ที่นี่
│   ├── {Feature}/
│   │   ├── Commands/        ← สำหรับ command type
│   │   │   ├── {Name}Command.cs
│   │   │   └── {Name}CommandHandler.cs
│   │   ├── Queries/         ← สำหรับ query type
│   │   │   ├── {Name}Query.cs
│   │   │   └── {Name}QueryHandler.cs
│   │   ├── DTOs/
│   │   │   ├── {Name}Request.cs
│   │   │   └── {Name}Response.cs
│   │   └── Validators/
│   │       └── {Name}Validator.cs
├── Banking.Infrastructure/  ← Repository implementations
└── Banking.Api/             ← Controllers
```

## ขั้นตอนการทำงาน

### Step 1: อ่าน Context ที่จำเป็น
1. อ่าน `Banking.Domain/Entities/` เพื่อดู Entity ที่เกี่ยวข้อง
2. อ่าน `Banking.Domain/Interfaces/` เพื่อดู Repository interfaces
3. อ่าน `Banking.Domain/Enums/Enums.cs` เพื่อดู Enum types
4. อ่าน `Banking.Domain/Exceptions/` เพื่อดู Domain exceptions
5. ตรวจดู existing features ใน `Banking.Application/` เพื่อ follow pattern เดียวกัน

### Step 2: สร้าง Command/Query Class
**สำหรับ Command:**
```csharp
using MediatR;

namespace Banking.Application.{Feature}.Commands;

public record {Name}Command(
    // properties จาก Request DTO
) : IRequest<{Name}Response>;
```

**สำหรับ Query:**
```csharp
using MediatR;

namespace Banking.Application.{Feature}.Queries;

public record {Name}Query(
    // filter/id parameters
) : IRequest<{Name}Response>;
```

### Step 3: สร้าง Handler
- Inject `IUnitOfWork` สำหรับ command (ต้องใช้ transaction)
- Inject specific repository สำหรับ query (read-only)
- ใช้ Domain Exceptions จาก `Banking.Domain.Exceptions`
- สำหรับ financial operations ต้อง:
  - เริ่ม DB Transaction ด้วย `_unitOfWork.BeginTransactionAsync()`
  - ใช้ `GetByIdForUpdateAsync()` สำหรับ row-level locking
  - Commit ด้วย `_unitOfWork.CommitTransactionAsync()`
  - Rollback ใน catch block

### Step 4: สร้าง DTOs
- `{Name}Request.cs` — input DTO (ใช้กับ API Controller)
- `{Name}Response.cs` — output DTO (return จาก Handler)
- ใช้ `record` type สำหรับ immutability
- ห้ามใช้ Entity โดยตรงเป็น response — ต้อง map เสมอ

### Step 5: สร้าง Validator
```csharp
using FluentValidation;

namespace Banking.Application.{Feature}.Validators;

public class {Name}Validator : AbstractValidator<{Name}Command>
{
    public {Name}Validator()
    {
        // validation rules
    }
}
```

**กฎ Validation สำหรับ Banking:**
- Amount: `GreaterThan(0)`, `PrecisionScale(18, 2)`
- AccountId: `NotEmpty()`
- AccountNumber: `Matches(@"^\d{4}-\d{4}-\d{4}$")`
- Currency: `Length(3)`, default "THB"

### Step 6: ตรวจสอบและ Register
1. ตรวจว่า `Banking.Application` มี DependencyInjection setup สำหรับ MediatR + FluentValidation
2. ถ้ายังไม่มี ให้สร้าง `DependencyInjection.cs`:
```csharp
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly));
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);
        return services;
    }
}
```

## Output
แสดงรายการไฟล์ที่สร้างทั้งหมด พร้อม path เต็ม และอธิบายสั้นๆ ว่าแต่ละไฟล์ทำอะไร

## ตัวอย่างการใช้งาน
```
/bank-scaffold Deposit command Transaction
/bank-scaffold GetBalance query Account
/bank-scaffold CreateAccount command Account
/bank-scaffold GetTransactionHistory query Transaction
```
