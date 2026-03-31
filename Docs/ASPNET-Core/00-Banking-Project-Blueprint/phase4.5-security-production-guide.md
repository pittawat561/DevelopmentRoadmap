# Phase 4.5: Security & Production Hardening — คู่มือทำทีละขั้นตอน

> PIN Verification + Fraud Detection + Audit Logging + Idempotency + Security Hardening + Unit/Integration Tests
> ทุกขั้นตอนอธิบายว่า "ทำไม" ต้องทำ + สร้างเพื่ออะไร

---

## สิ่งที่ต้องเสร็จก่อน (จาก Phase 3 + 4)

```
☑ Backend API ทำงานครบ: Auth, Accounts, Transactions, Admin
☑ JWT Authentication + Token Blacklist (Redis)
☑ Redis: Balance Cache, Distributed Lock, Rate Limiting
☑ SignalR Hub: real-time notifications
☑ ExceptionMiddleware + RateLimitMiddleware + TokenBlacklistMiddleware
☑ FluentValidation validators ครบ
☑ dotnet build ผ่าน 0 errors
```

---

## ภาพรวม Phase 4.5 — ทำไมต้อง Harden?

```
ปัญหาปัจจุบัน (Phase 1-3):

1. ไม่มี PIN — ใครมี token ก็โอนเงินได้เลย
   → ถ้า token หลุด ก็ถอน/โอนหมดบัญชี!

2. ไม่มี Fraud Detection — bot โอนเงิน 1,000 ครั้ง/วันได้
   → ไม่มีการตรวจจับธุรกรรมผิดปกติ

3. ไม่มี Audit Log — ไม่รู้ว่าใครทำอะไรเมื่อไหร่
   → ธนาคารจริงต้องเก็บ audit trail ทุก action (กฎหมาย)

4. ไม่มี Idempotency — client ส่ง request ซ้ำ → ทำรายการซ้ำ
   → กดปุ่ม 2 ครั้ง → ฝากเงิน 2 ครั้ง!

5. ไม่มี Tests — ไม่มั่นใจว่า refactor แล้วยังทำงานถูก
   → แก้โค้ดแล้วพัง ไม่รู้ตัว

Phase 4.5 แก้ทั้ง 5 ปัญหา:
┌──────────────────────────────────────────────┐
│ 1. PIN Verification  → ยืนยันก่อนทุกธุรกรรม   │
│ 2. Fraud Detection   → ตรวจจับรูปแบบผิดปกติ   │
│ 3. Audit Logging     → บันทึกทุก action         │
│ 4. Idempotency       → ป้องกัน request ซ้ำ      │
│ 5. Unit + Int Tests  → มั่นใจได้ว่าระบบถูกต้อง │
└──────────────────────────────────────────────┘
```

---

## ขั้นตอนที่ 1: PIN Verification

### 1.1 เพิ่ม PIN field ใน User Entity

```
📁 แก้ไข Banking.Domain/Entities/User.cs

ทำไม: ทุก financial transaction ต้องยืนยัน PIN 6 หลัก
แม้ token หลุด — ไม่มี PIN ก็โอนเงินไม่ได้

PIN vs Password:
  Password → ใช้ตอน Login (เข้าระบบ)
  PIN → ใช้ตอนทำธุรกรรม (ฝาก/ถอน/โอน)
  แยกกันเพราะ: ถ้า password โดนขโมย → ยังต้องรู้ PIN ถึงจะเอาเงินได้
```

```csharp
// เพิ่มใน User.cs

/// <summary>
/// PIN 6 หลัก — Hash ด้วย BCrypt เหมือน password
/// ห้ามเก็บ plaintext!
/// null = ยังไม่ตั้ง PIN (บังคับตั้งก่อนทำธุรกรรมแรก)
/// </summary>
public string? PinHash { get; set; }

/// <summary>
/// นับจำนวน PIN ผิดติดต่อกัน
/// ถึง 3 ครั้ง → ล็อกธุรกรรม (ต้อง reset PIN)
/// </summary>
public int FailedPinAttempts { get; set; } = 0;

/// <summary>
/// ธุรกรรมถูกล็อก (PIN ผิดเกิน)
/// ต่างจาก IsLocked (login ถูกล็อก)
/// </summary>
public bool IsTransactionLocked { get; set; } = false;
```

### 1.2 สร้าง PIN DTOs

```csharp
// Banking.Application/DTOs/PinDtos.cs

namespace Banking.Application.DTOs;

/// <summary>
/// ตั้ง PIN ครั้งแรก หรือเปลี่ยน PIN
/// </summary>
public record SetPinRequest(
    string Pin,
    string ConfirmPin
);

/// <summary>
/// เปลี่ยน PIN — ต้องใส่ PIN เก่าด้วย
/// </summary>
public record ChangePinRequest(
    string CurrentPin,
    string NewPin,
    string ConfirmNewPin
);

/// <summary>
/// ยืนยัน PIN ก่อนทำธุรกรรม — เพิ่มใน request เดิม
/// </summary>
public record DepositWithPinRequest(
    Guid AccountId,
    decimal Amount,
    string? Description,
    string Pin
);

public record WithdrawWithPinRequest(
    Guid AccountId,
    decimal Amount,
    string? Description,
    string Pin
);

public record TransferWithPinRequest(
    Guid FromAccountId,
    Guid ToAccountId,
    decimal Amount,
    string? Description,
    string Pin
);
```

### 1.3 PIN Validators

```csharp
// Banking.Application/Validators/PinValidators.cs

using Banking.Application.DTOs;
using FluentValidation;

namespace Banking.Application.Validators;

public class SetPinRequestValidator : AbstractValidator<SetPinRequest>
{
    public SetPinRequestValidator()
    {
        RuleFor(x => x.Pin)
            .NotEmpty().WithMessage("PIN is required.")
            .Length(6).WithMessage("PIN must be exactly 6 digits.")
            .Matches(@"^\d{6}$").WithMessage("PIN must contain only digits.");

        RuleFor(x => x.ConfirmPin)
            .Equal(x => x.Pin).WithMessage("PINs do not match.");
    }
}

public class ChangePinRequestValidator : AbstractValidator<ChangePinRequest>
{
    public ChangePinRequestValidator()
    {
        RuleFor(x => x.CurrentPin)
            .NotEmpty().WithMessage("Current PIN is required.")
            .Length(6);

        RuleFor(x => x.NewPin)
            .NotEmpty().WithMessage("New PIN is required.")
            .Length(6).WithMessage("PIN must be exactly 6 digits.")
            .Matches(@"^\d{6}$").WithMessage("PIN must contain only digits.");

        RuleFor(x => x.ConfirmNewPin)
            .Equal(x => x.NewPin).WithMessage("PINs do not match.");

        RuleFor(x => x)
            .Must(x => x.CurrentPin != x.NewPin)
            .WithMessage("New PIN must be different from current PIN.");
    }
}
```

### 1.4 PIN Service

```csharp
// Banking.Application/Services/PinService.cs

using Banking.Application.DTOs;
using Banking.Domain.Exceptions;
using Banking.Domain.Interfaces;

namespace Banking.Application.Services;

/// <summary>
/// PIN Service — จัดการ PIN สำหรับ transaction verification
///
/// Flow การใช้ PIN:
///   1. Register → ยังไม่มี PIN
///   2. เข้า Dashboard → ระบบบังคับตั้ง PIN (SetPin)
///   3. ทำธุรกรรม → ต้องใส่ PIN ทุกครั้ง (VerifyPin)
///   4. PIN ผิด 3 ครั้ง → ล็อกธุรกรรม → ต้อง Reset PIN ผ่าน Admin/OTP
/// </summary>
public class PinService
{
    private readonly IUnitOfWork _unitOfWork;
    private const int MaxPinAttempts = 3;

    public PinService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    /// <summary>
    /// ตั้ง PIN ครั้งแรก
    /// </summary>
    public async Task SetPinAsync(Guid userId, SetPinRequest request, CancellationToken ct = default)
    {
        var user = await _unitOfWork.Users.GetByIdAsync(userId, ct)
            ?? throw new NotFoundException("User", userId);

        if (user.PinHash is not null)
            throw new InvalidOperationException("PIN is already set. Use change PIN instead.");

        user.PinHash = BCrypt.Net.BCrypt.HashPassword(request.Pin);
        _unitOfWork.Users.Update(user);
        await _unitOfWork.SaveChangesAsync(ct);
    }

    /// <summary>
    /// เปลี่ยน PIN — ต้องยืนยัน PIN เก่าก่อน
    /// </summary>
    public async Task ChangePinAsync(Guid userId, ChangePinRequest request, CancellationToken ct = default)
    {
        var user = await _unitOfWork.Users.GetByIdAsync(userId, ct)
            ?? throw new NotFoundException("User", userId);

        if (user.PinHash is null)
            throw new InvalidOperationException("PIN is not set yet. Use set PIN first.");

        if (!BCrypt.Net.BCrypt.Verify(request.CurrentPin, user.PinHash))
            throw new ArgumentException("Current PIN is incorrect.");

        user.PinHash = BCrypt.Net.BCrypt.HashPassword(request.NewPin);
        user.FailedPinAttempts = 0;
        _unitOfWork.Users.Update(user);
        await _unitOfWork.SaveChangesAsync(ct);
    }

    /// <summary>
    /// ⚠️ CRITICAL: ตรวจ PIN ก่อนทำธุรกรรม
    ///
    /// เรียกจาก TransactionService ก่อนทุก deposit/withdraw/transfer
    ///
    /// Flow:
    ///   1. เช็คว่ามี PIN ไหม (ยังไม่ตั้ง → error)
    ///   2. เช็คว่าถูกล็อกธุรกรรมไหม (PIN ผิดเกิน → error)
    ///   3. Verify PIN
    ///      - ถูก → reset counter, return true
    ///      - ผิด → เพิ่ม counter (ถึง 3 → ล็อก)
    /// </summary>
    public async Task VerifyPinAsync(Guid userId, string pin, CancellationToken ct = default)
    {
        var user = await _unitOfWork.Users.GetByIdAsync(userId, ct)
            ?? throw new NotFoundException("User", userId);

        if (user.PinHash is null)
            throw new InvalidOperationException(
                "PIN is not set. Please set your PIN before making transactions.");

        if (user.IsTransactionLocked)
            throw new InvalidOperationException(
                "Transactions are locked due to too many failed PIN attempts. Contact support.");

        if (!BCrypt.Net.BCrypt.Verify(pin, user.PinHash))
        {
            user.FailedPinAttempts++;

            if (user.FailedPinAttempts >= MaxPinAttempts)
            {
                user.IsTransactionLocked = true;
            }

            _unitOfWork.Users.Update(user);
            await _unitOfWork.SaveChangesAsync(ct);

            var remaining = MaxPinAttempts - user.FailedPinAttempts;
            if (remaining > 0)
                throw new ArgumentException(
                    $"Incorrect PIN. {remaining} attempt(s) remaining.");
            else
                throw new InvalidOperationException(
                    "Incorrect PIN. Transactions have been locked. Contact support.");
        }

        // PIN ถูก → reset counter
        if (user.FailedPinAttempts > 0)
        {
            user.FailedPinAttempts = 0;
            _unitOfWork.Users.Update(user);
            await _unitOfWork.SaveChangesAsync(ct);
        }
    }
}
```

### 1.5 เพิ่ม PIN Endpoints ใน AuthController

```csharp
// เพิ่มใน Banking.Api/Controllers/AuthController.cs

/// <summary>
/// ตั้ง PIN ครั้งแรก — POST /api/auth/pin/set
/// </summary>
[HttpPost("pin/set")]
[Authorize]
public async Task<IActionResult> SetPin(
    [FromBody] SetPinRequest request, CancellationToken ct)
{
    var userId = GetUserId();
    if (userId is null) return Unauthorized();

    await _pinService.SetPinAsync(userId.Value, request, ct);
    return Ok(new ApiResponse<object>(true, "PIN set successfully."));
}

/// <summary>
/// เปลี่ยน PIN — POST /api/auth/pin/change
/// </summary>
[HttpPost("pin/change")]
[Authorize]
public async Task<IActionResult> ChangePin(
    [FromBody] ChangePinRequest request, CancellationToken ct)
{
    var userId = GetUserId();
    if (userId is null) return Unauthorized();

    await _pinService.ChangePinAsync(userId.Value, request, ct);
    return Ok(new ApiResponse<object>(true, "PIN changed successfully."));
}

// Helper: ดึง userId จาก JWT
private Guid? GetUserId()
{
    var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    return claim is not null ? Guid.Parse(claim) : null;
}
```

### 1.6 อัปเดต TransactionService — บังคับ PIN

```csharp
// เพิ่มใน TransactionService constructor

private readonly PinService _pinService;

public TransactionService(
    IUnitOfWork unitOfWork,
    IRedisCacheService cache,
    INotificationService notification,
    PinService pinService)
{
    _unitOfWork = unitOfWork;
    _cache = cache;
    _notification = notification;
    _pinService = pinService;
}
```

```csharp
// แก้ไข method signatures — เพิ่ม userId + pin

public async Task<TransactionResponse> DepositAsync(
    DepositRequest request,
    Guid userId,          // ← เพิ่ม
    string pin,           // ← เพิ่ม
    string? ipAddress = null,
    CancellationToken ct = default)
{
    if (request.Amount <= 0)
        throw new ArgumentException("Amount must be greater than 0.");

    // ⚠️ ตรวจ PIN ก่อนทำอะไรทั้งนั้น
    await _pinService.VerifyPinAsync(userId, pin, ct);

    // ... ส่วนที่เหลือเหมือนเดิม (lock → transaction → cache → notify)
}
```

```
ทำเหมือนกันสำหรับ WithdrawAsync และ TransferAsync
เพิ่ม parameter userId + pin → เรียก _pinService.VerifyPinAsync() ก่อน
```

### 1.7 Admin: Reset Transaction Lock

```csharp
// เพิ่มใน AdminController

/// <summary>
/// ปลด Transaction Lock (PIN ผิดเกิน) — POST /api/admin/users/{id}/reset-pin-lock
/// </summary>
[HttpPost("users/{id:guid}/reset-pin-lock")]
public async Task<IActionResult> ResetPinLock(Guid id, CancellationToken ct)
{
    var user = await _unitOfWork.Users.GetByIdAsync(id, ct);
    if (user is null)
        return NotFound(new ApiResponse<object>(false, "User not found."));

    user.IsTransactionLocked = false;
    user.FailedPinAttempts = 0;
    user.PinHash = null; // บังคับตั้ง PIN ใหม่
    _unitOfWork.Users.Update(user);
    await _unitOfWork.SaveChangesAsync(ct);

    return Ok(new ApiResponse<object>(true,
        $"Transaction lock reset for {user.FullName}. User must set a new PIN."));
}
```

### 1.8 Migration

```bash
dotnet ef migrations add AddPinFields \
    --project Banking.Infrastructure \
    --startup-project Banking.Api
```

---

## ขั้นตอนที่ 2: Idempotency Key

```
📁 ที่ต้องสร้าง:
  Banking.Api/Middleware/IdempotencyMiddleware.cs

ทำไม: ป้องกัน request ซ้ำ (duplicate transactions)

ปัญหา:
  User กดปุ่ม "โอนเงิน" → network ช้า → กดอีกครั้ง
  Server ได้ 2 requests → โอนเงิน 2 ครั้ง!

วิธีแก้: Idempotency Key
  Client ส่ง header: X-Idempotency-Key: unique-uuid
  Server: เคยเห็น key นี้ไหม?
    - ไม่เคย → ทำ request + เก็บ key ใน Redis
    - เคย → return response เดิม (ไม่ทำซ้ำ)
```

```csharp
// Banking.Api/Middleware/IdempotencyMiddleware.cs

using Banking.Application.Services;
using System.Text.Json;

namespace Banking.Api.Middleware;

/// <summary>
/// Idempotency Middleware — ป้องกัน duplicate requests
///
/// ทำงานเฉพาะ POST/PUT/PATCH (write operations)
/// ไม่ทำงานกับ GET/DELETE
///
/// Flow:
///   1. อ่าน header X-Idempotency-Key
///   2. ถ้าไม่มี header → ปล่อยผ่าน (backwards compatible)
///   3. ถ้ามี → เช็ค Redis ว่า key นี้มี response แล้วไหม
///   4. ถ้ามี → return response เดิม (ไม่ process ซ้ำ)
///   5. ถ้าไม่มี → process request → เก็บ response ใน Redis
///
/// TTL: 24 ชั่วโมง — หลังจากนั้น key หมดอายุ (ส่ง request ซ้ำได้)
/// </summary>
public class IdempotencyMiddleware
{
    private readonly RequestDelegate _next;
    private static readonly TimeSpan IdempotencyTtl = TimeSpan.FromHours(24);

    public IdempotencyMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IRedisCacheService cache)
    {
        // เฉพาะ write operations
        var method = context.Request.Method;
        if (method != "POST" && method != "PUT" && method != "PATCH")
        {
            await _next(context);
            return;
        }

        // อ่าน idempotency key จาก header
        var idempotencyKey = context.Request.Headers["X-Idempotency-Key"].FirstOrDefault();
        if (string.IsNullOrEmpty(idempotencyKey))
        {
            await _next(context);
            return;
        }

        var cacheKey = $"idempotency:{idempotencyKey}";

        // เช็ค Redis: เคย process request นี้แล้วไหม?
        var cachedResponse = await cache.GetAsync<IdempotencyResponse>(cacheKey);
        if (cachedResponse is not null)
        {
            // Return response เดิม — ไม่ process ซ้ำ
            context.Response.StatusCode = cachedResponse.StatusCode;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(cachedResponse.Body);
            return;
        }

        // ดักจับ response เพื่อเก็บ cache
        var originalBodyStream = context.Response.Body;
        using var responseBody = new MemoryStream();
        context.Response.Body = responseBody;

        await _next(context);

        // อ่าน response ที่ได้
        responseBody.Seek(0, SeekOrigin.Begin);
        var body = await new StreamReader(responseBody).ReadToEndAsync();

        // เก็บ response ใน Redis (เฉพาะ 2xx)
        if (context.Response.StatusCode >= 200 && context.Response.StatusCode < 300)
        {
            var idempotencyResponse = new IdempotencyResponse(
                context.Response.StatusCode, body);

            await cache.SetAsync(cacheKey, idempotencyResponse, IdempotencyTtl);
        }

        // เขียน response กลับให้ client
        responseBody.Seek(0, SeekOrigin.Begin);
        await responseBody.CopyToAsync(originalBodyStream);
    }

    private record IdempotencyResponse(int StatusCode, string Body);
}
```

```
Client ใช้งาน:

// ทุก write request ควรส่ง X-Idempotency-Key
fetch('/api/transactions/transfer', {
  method: 'POST',
  headers: {
    'Content-Type': 'application/json',
    'Authorization': 'Bearer xxx',
    'X-Idempotency-Key': crypto.randomUUID()  // ← unique per request
  },
  body: JSON.stringify({ ... })
});

// ถ้า network timeout → retry ด้วย key เดิม → ไม่ทำซ้ำ
```

---

## ขั้นตอนที่ 3: Audit Logging Middleware

```
📁 ที่ต้องสร้าง:
  Banking.Api/Middleware/AuditMiddleware.cs
  Banking.Application/Services/IAuditService.cs
  Banking.Infrastructure/Services/AuditService.cs

ทำไม: บันทึกทุก action ลง AuditLog table
ธนาคารจริงต้องเก็บ audit trail (กฎหมาย พ.ร.บ. คอมพิวเตอร์)
ต้องเก็บ: ใคร ทำอะไร เมื่อไหร่ จาก IP ไหน ข้อมูลเปลี่ยนอย่างไร

AuditLog entity มีอยู่แล้ว (Phase 1) — ยังไม่มี service/middleware ที่ใช้งาน
```

### 3.1 Audit Service Interface

```csharp
// Banking.Application/Services/IAuditService.cs

namespace Banking.Application.Services;

/// <summary>
/// Audit Service Interface — บันทึกทุก action
/// </summary>
public interface IAuditService
{
    /// <summary>
    /// บันทึก audit log
    /// </summary>
    Task LogAsync(
        Guid? userId,
        string action,
        string entityType,
        string? entityId = null,
        object? oldValues = null,
        object? newValues = null,
        string? ipAddress = null,
        string? userAgent = null,
        CancellationToken ct = default);
}
```

### 3.2 Audit Service Implementation

```csharp
// Banking.Infrastructure/Services/AuditService.cs

using Banking.Application.Services;
using Banking.Domain.Entities;
using Banking.Infrastructure.Data;
using System.Text.Json;

namespace Banking.Infrastructure.Services;

/// <summary>
/// Audit Service — เขียน AuditLog ลง database โดยตรง
///
/// ทำไมไม่ใช้ UnitOfWork:
///   Audit log ต้องเขียนแยกจาก business transaction
///   ถ้า transaction rollback → audit log ไม่ควร rollback ด้วย
///   ต้องบันทึกแม้ว่า action จะ fail (เพื่อ security tracking)
///
/// ใช้ DbContext ตัวใหม่ (ไม่ใช่ตัวเดียวกับ UnitOfWork)
/// → เขียน audit log ได้แม้ main transaction rollback
/// </summary>
public class AuditService : IAuditService
{
    private readonly IServiceScopeFactory _scopeFactory;

    public AuditService(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public async Task LogAsync(
        Guid? userId,
        string action,
        string entityType,
        string? entityId = null,
        object? oldValues = null,
        object? newValues = null,
        string? ipAddress = null,
        string? userAgent = null,
        CancellationToken ct = default)
    {
        // สร้าง scope ใหม่ → DbContext ใหม่ → แยกจาก main transaction
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var auditLog = new AuditLog
        {
            UserId = userId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            OldValues = oldValues is not null
                ? JsonSerializer.Serialize(oldValues) : null,
            NewValues = newValues is not null
                ? JsonSerializer.Serialize(newValues) : null,
            IpAddress = ipAddress,
            UserAgent = userAgent
        };

        context.Set<AuditLog>().Add(auditLog);
        await context.SaveChangesAsync(ct);
    }
}
```

### 3.3 Audit Middleware (API-level logging)

```csharp
// Banking.Api/Middleware/AuditMiddleware.cs

using Banking.Application.Services;
using System.Security.Claims;

namespace Banking.Api.Middleware;

/// <summary>
/// Audit Middleware — บันทึก HTTP request/response สำหรับ write operations
///
/// บันทึกเฉพาะ:
///   - POST, PUT, PATCH, DELETE (write operations)
///   - ข้าม GET (read-only ไม่ต้องบันทึก)
///   - ข้าม Swagger, health check
///
/// ข้อมูลที่บันทึก:
///   - UserId (จาก JWT)
///   - Action (HTTP method + path)
///   - IP Address
///   - User Agent (browser/app)
///   - Status Code
/// </summary>
public class AuditMiddleware
{
    private readonly RequestDelegate _next;

    public AuditMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IAuditService auditService)
    {
        // เฉพาะ write operations
        var method = context.Request.Method;
        if (method == "GET" || method == "OPTIONS" || method == "HEAD")
        {
            await _next(context);
            return;
        }

        // ข้าม swagger, health
        var path = context.Request.Path.Value?.ToLower() ?? "";
        if (path.Contains("swagger") || path.Contains("health"))
        {
            await _next(context);
            return;
        }

        await _next(context);

        // บันทึกหลัง request เสร็จ (ได้ status code)
        var userId = context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var parsedUserId = userId is not null ? Guid.Parse(userId) : (Guid?)null;

        try
        {
            await auditService.LogAsync(
                userId: parsedUserId,
                action: $"{method} {context.Request.Path}",
                entityType: "HttpRequest",
                entityId: null,
                oldValues: null,
                newValues: new
                {
                    StatusCode = context.Response.StatusCode,
                    QueryString = context.Request.QueryString.Value
                },
                ipAddress: context.Connection.RemoteIpAddress?.ToString(),
                userAgent: context.Request.Headers.UserAgent.FirstOrDefault()
            );
        }
        catch
        {
            // Audit log failure ไม่ควรทำให้ request fail
            // Log warning แต่ไม่ throw
        }
    }
}
```

### 3.4 เพิ่ม Audit ใน TransactionService

```csharp
// เพิ่มใน TransactionService — หลังทุก transaction สำเร็จ

// ตัวอย่างใน DepositAsync (หลัง commit):

await _auditService.LogAsync(
    userId: userId,
    action: "Deposit",
    entityType: "Transaction",
    entityId: transaction.Id.ToString(),
    oldValues: new { BalanceBefore = balanceBefore },
    newValues: new { BalanceAfter = account.Balance, Amount = request.Amount },
    ipAddress: ipAddress
);
```

```
ทำเหมือนกันสำหรับ:
  WithdrawAsync → action: "Withdrawal"
  TransferAsync → action: "Transfer"
  LoginAsync    → action: "Login" / "LoginFailed"
  RegisterAsync → action: "Register"
```

---

## ขั้นตอนที่ 4: Fraud Detection

```
📁 ที่ต้องสร้าง:
  Banking.Application/Services/IFraudDetectionService.cs
  Banking.Infrastructure/Services/FraudDetectionService.cs

ทำไม: ตรวจจับธุรกรรมที่ผิดปกติก่อนดำเนินการ
ธนาคารจริงมีทีม fraud detection + ML models
เราทำแบบ rule-based (เช็คตาม rules ง่ายๆ)
```

### 4.1 Fraud Detection Interface

```csharp
// Banking.Application/Services/IFraudDetectionService.cs

namespace Banking.Application.Services;

/// <summary>
/// Fraud Detection Service — ตรวจจับธุรกรรมผิดปกติ
///
/// Rule-based detection (ไม่ใช่ ML):
///   1. จำนวนเงินผิดปกติ (สูงกว่าปกติมาก)
///   2. ความถี่ผิดปกติ (หลายครั้งใน time window สั้นๆ)
///   3. เวลาผิดปกติ (ดึกมาก/เช้ามาก)
///   4. บัญชีปลายทางใหม่ + จำนวนเงินสูง
///
/// เรียกก่อนทุก transaction → ถ้า suspicious → flag + alert
/// ไม่ block ทันที (อาจ false positive) → แค่ flag + notify admin
/// </summary>
public interface IFraudDetectionService
{
    /// <summary>
    /// ตรวจสอบธุรกรรม — return FraudCheckResult
    /// </summary>
    Task<FraudCheckResult> CheckTransactionAsync(
        Guid accountId,
        decimal amount,
        string transactionType,
        CancellationToken ct = default);
}

/// <summary>
/// ผลการตรวจ fraud
/// </summary>
public record FraudCheckResult(
    bool IsSuspicious,
    string? Reason,
    FraudRiskLevel RiskLevel
);

public enum FraudRiskLevel
{
    Low,       // ปกติ
    Medium,    // ต้องจับตา (flag + log)
    High,      // ต้อง review (flag + notify admin)
    Critical   // block ทันที (ไม่ดำเนินการ)
}
```

### 4.2 Fraud Detection Implementation

```csharp
// Banking.Infrastructure/Services/FraudDetectionService.cs

using Banking.Application.Services;
using Banking.Domain.Enums;
using Banking.Domain.Interfaces;

namespace Banking.Infrastructure.Services;

/// <summary>
/// Fraud Detection — Rule-based
///
/// Rules:
///   1. Large Transaction: ยอดเงินสูงกว่า threshold (100,000+)
///   2. High Frequency: มากกว่า 5 transactions ใน 10 นาที
///   3. Unusual Hours: ธุรกรรมตี 1 - ตี 5
///   4. Near Daily Limit: ยอดถอนวันนี้เกิน 80% ของ daily limit
/// </summary>
public class FraudDetectionService : IFraudDetectionService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IRedisCacheService _cache;
    private readonly IAuditService _auditService;

    // Configurable thresholds
    private const decimal LargeTransactionThreshold = 100_000;
    private const int MaxTransactionsPerWindow = 5;
    private static readonly TimeSpan FrequencyWindow = TimeSpan.FromMinutes(10);
    private const decimal DailyLimitWarningPercent = 0.8m; // 80%

    public FraudDetectionService(
        IUnitOfWork unitOfWork,
        IRedisCacheService cache,
        IAuditService auditService)
    {
        _unitOfWork = unitOfWork;
        _cache = cache;
        _auditService = auditService;
    }

    public async Task<FraudCheckResult> CheckTransactionAsync(
        Guid accountId, decimal amount, string transactionType, CancellationToken ct = default)
    {
        var reasons = new List<string>();
        var riskLevel = FraudRiskLevel.Low;

        // === Rule 1: Large Transaction ===
        if (amount >= LargeTransactionThreshold)
        {
            reasons.Add($"Large transaction: {amount:N2} THB (threshold: {LargeTransactionThreshold:N2})");
            riskLevel = FraudRiskLevel.Medium;
        }

        // === Rule 2: High Frequency (Redis counter) ===
        var frequencyKey = $"fraud:frequency:{accountId}";
        var allowed = await _cache.CheckRateLimitAsync(
            frequencyKey, MaxTransactionsPerWindow, FrequencyWindow);

        if (!allowed)
        {
            reasons.Add($"High frequency: >{MaxTransactionsPerWindow} transactions in {FrequencyWindow.TotalMinutes} minutes");
            riskLevel = FraudRiskLevel.High;
        }

        // === Rule 3: Unusual Hours (01:00 - 05:00 local time) ===
        var localHour = DateTime.UtcNow.AddHours(7).Hour; // UTC+7 (Thailand)
        if (localHour >= 1 && localHour < 5)
        {
            reasons.Add($"Unusual hour: {localHour}:00 (Bangkok time)");
            riskLevel = (FraudRiskLevel)Math.Max((int)riskLevel, (int)FraudRiskLevel.Medium);
        }

        // === Rule 4: Near Daily Limit (สำหรับ withdrawal/transfer) ===
        if (transactionType is "Withdrawal" or "Transfer" or "TransferOut")
        {
            var account = await _unitOfWork.Accounts.GetByIdAsync(accountId, ct);
            if (account is not null)
            {
                var todayTotal = await _unitOfWork.Transactions
                    .GetTodayWithdrawalTotalAsync(accountId, ct);

                var usedPercent = (todayTotal + amount) / account.DailyWithdrawalLimit;
                if (usedPercent >= DailyLimitWarningPercent)
                {
                    reasons.Add($"Near daily limit: {usedPercent:P0} used ({todayTotal + amount:N2} / {account.DailyWithdrawalLimit:N2})");
                    riskLevel = (FraudRiskLevel)Math.Max((int)riskLevel, (int)FraudRiskLevel.Medium);
                }
            }
        }

        var isSuspicious = riskLevel > FraudRiskLevel.Low;
        var reason = reasons.Count > 0 ? string.Join("; ", reasons) : null;

        // Log suspicious activity
        if (isSuspicious)
        {
            await _auditService.LogAsync(
                userId: null,
                action: "FraudAlert",
                entityType: "Account",
                entityId: accountId.ToString(),
                newValues: new
                {
                    TransactionType = transactionType,
                    Amount = amount,
                    RiskLevel = riskLevel.ToString(),
                    Reasons = reasons
                }
            );
        }

        return new FraudCheckResult(isSuspicious, reason, riskLevel);
    }
}
```

### 4.3 เพิ่ม Fraud Check ใน TransactionService

```csharp
// เพิ่มใน TransactionService — ก่อน lock + DB transaction

// ตัวอย่างใน WithdrawAsync:

// === Fraud Check ===
var fraudResult = await _fraudDetection.CheckTransactionAsync(
    request.AccountId, request.Amount, "Withdrawal", ct);

if (fraudResult.RiskLevel == FraudRiskLevel.Critical)
    throw new InvalidOperationException(
        $"Transaction blocked by fraud detection: {fraudResult.Reason}");

// ถ้า Medium/High → ทำต่อได้ แต่ flag ไว้ (admin จะ review)
```

```
ลำดับการเรียกใน TransactionService:

1. Validate input (FluentValidation)
2. Verify PIN (PinService)
3. Fraud Detection check (FraudDetectionService)
4. Acquire Distributed Lock (Redis)
5. Begin DB Transaction
6. Row Lock (FOR UPDATE)
7. Business logic (balance check, daily limit)
8. Update balance + create records
9. Commit
10. Update cache + Notify (SignalR)
11. Release Lock
12. Audit Log
```

---

## ขั้นตอนที่ 5: Security Hardening

### 5.1 IP Whitelisting สำหรับ Admin

```csharp
// Banking.Api/Middleware/AdminIpWhitelistMiddleware.cs

namespace Banking.Api.Middleware;

/// <summary>
/// Admin IP Whitelist — อนุญาตเฉพาะ IP ที่กำหนด
/// ใช้เฉพาะ /api/admin/* endpoints
/// </summary>
public class AdminIpWhitelistMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IConfiguration _config;

    public AdminIpWhitelistMiddleware(RequestDelegate next, IConfiguration config)
    {
        _next = next;
        _config = config;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value?.ToLower() ?? "";

        if (!path.StartsWith("/api/admin"))
        {
            await _next(context);
            return;
        }

        var allowedIps = _config.GetSection("Security:AdminAllowedIps")
            .Get<string[]>() ?? ["127.0.0.1", "::1"];

        var remoteIp = context.Connection.RemoteIpAddress?.ToString();

        if (remoteIp is null || !allowedIps.Contains(remoteIp))
        {
            context.Response.StatusCode = 403;
            await context.Response.WriteAsJsonAsync(new
            {
                success = false,
                message = "Access denied. IP not authorized for admin endpoints.",
                statusCode = 403
            });
            return;
        }

        await _next(context);
    }
}
```

```json
// เพิ่มใน appsettings.json

"Security": {
  "AdminAllowedIps": ["127.0.0.1", "::1", "192.168.1.0/24"]
}
```

### 5.2 Mask Account Numbers ใน Logs

```csharp
// Banking.Application/Services/DataMasking.cs

namespace Banking.Application.Services;

/// <summary>
/// Data Masking — ซ่อนข้อมูลสำคัญใน logs
///
/// "1234-5678-9012" → "****-****-9012" (เห็นแค่ 4 ตัวท้าย)
/// "admin@bank.com" → "a****@bank.com"
/// </summary>
public static class DataMasking
{
    public static string MaskAccountNumber(string accountNumber)
    {
        if (string.IsNullOrEmpty(accountNumber) || accountNumber.Length < 4)
            return "****";

        return $"****-****-{accountNumber[^4..]}";
    }

    public static string MaskEmail(string email)
    {
        var parts = email.Split('@');
        if (parts.Length != 2) return "****";

        var name = parts[0];
        var masked = name.Length <= 1
            ? "*" : $"{name[0]}{"".PadLeft(name.Length - 1, '*')}";

        return $"{masked}@{parts[1]}";
    }
}
```

### 5.3 Health Check Endpoint

```csharp
// เพิ่มใน Program.cs — ก่อน app.Run()

// Health Check: /health
app.MapGet("/health", async (AppDbContext db, IConnectionMultiplexer redis) =>
{
    var checks = new Dictionary<string, string>();

    // Database
    try
    {
        await db.Database.CanConnectAsync();
        checks["database"] = "healthy";
    }
    catch { checks["database"] = "unhealthy"; }

    // Redis
    try
    {
        var pong = await redis.GetDatabase().PingAsync();
        checks["redis"] = pong.TotalMilliseconds < 100 ? "healthy" : "degraded";
    }
    catch { checks["redis"] = "unhealthy"; }

    var isHealthy = checks.Values.All(v => v == "healthy");
    return Results.Json(new
    {
        status = isHealthy ? "healthy" : "unhealthy",
        checks,
        timestamp = DateTime.UtcNow
    }, statusCode: isHealthy ? 200 : 503);
}).ExcludeFromDescription(); // ไม่แสดงใน Swagger
```

---

## ขั้นตอนที่ 6: Unit Tests

```
📁 Banking.Tests.Unit/

ทำไม: ทดสอบ business logic โดยไม่ต้องมี database จริง
Mock dependencies → ทดสอบเฉพาะ logic ของ service
เร็วมาก (< 1 วินาที) → รันได้ทุกครั้งที่แก้โค้ด

Tech Stack:
  xUnit = test framework
  Moq = mock dependencies (repositories, services)
  FluentAssertions = อ่าน assertion ง่ายกว่า Assert.Equal
  Bogus = สร้าง fake data (ชื่อ, email, เบอร์โทร ฯลฯ)
```

### 6.1 Transaction Service Tests

```csharp
// Banking.Tests.Unit/Services/TransactionServiceTests.cs

using Banking.Application.DTOs;
using Banking.Application.Services;
using Banking.Domain.Entities;
using Banking.Domain.Enums;
using Banking.Domain.Exceptions;
using Banking.Domain.Interfaces;
using FluentAssertions;
using Moq;

namespace Banking.Tests.Unit.Services;

public class TransactionServiceTests
{
    private readonly Mock<IUnitOfWork> _unitOfWork;
    private readonly Mock<IRedisCacheService> _cache;
    private readonly Mock<INotificationService> _notification;
    private readonly Mock<PinService> _pinService;
    private readonly TransactionService _sut; // System Under Test

    public TransactionServiceTests()
    {
        _unitOfWork = new Mock<IUnitOfWork>();
        _cache = new Mock<IRedisCacheService>();
        _notification = new Mock<INotificationService>();
        _pinService = new Mock<PinService>(MockBehavior.Loose,
            _unitOfWork.Object);

        _sut = new TransactionService(
            _unitOfWork.Object,
            _cache.Object,
            _notification.Object,
            _pinService.Object);
    }

    // =====================================================
    // Deposit Tests
    // =====================================================

    [Fact]
    public async Task DepositAsync_ValidAmount_IncreasesBalance()
    {
        // Arrange
        var account = CreateTestAccount(balance: 10_000);
        SetupAccountMock(account);
        SetupLockMock();

        var request = new DepositRequest(account.Id, 5_000, "Test deposit");

        // Act
        var result = await _sut.DepositAsync(request, ipAddress: "127.0.0.1");

        // Assert
        result.Should().NotBeNull();
        result.Type.Should().Be("Deposit");
        result.Amount.Should().Be(5_000);
        result.BalanceBefore.Should().Be(10_000);
        result.BalanceAfter.Should().Be(15_000);
        result.Status.Should().Be("Completed");
    }

    [Fact]
    public async Task DepositAsync_ZeroAmount_ThrowsArgumentException()
    {
        // Arrange
        var request = new DepositRequest(Guid.NewGuid(), 0, null);

        // Act
        var act = () => _sut.DepositAsync(request);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("Amount must be greater than 0.");
    }

    [Fact]
    public async Task DepositAsync_FrozenAccount_ThrowsAccountFrozenException()
    {
        // Arrange
        var account = CreateTestAccount(status: AccountStatus.Frozen);
        SetupAccountMock(account);
        SetupLockMock();

        var request = new DepositRequest(account.Id, 1_000, null);

        // Act
        var act = () => _sut.DepositAsync(request);

        // Assert
        await act.Should().ThrowAsync<AccountFrozenException>();
    }

    [Fact]
    public async Task DepositAsync_AccountNotFound_ThrowsNotFoundException()
    {
        // Arrange
        _unitOfWork.Setup(x => x.Accounts.GetByIdForUpdateAsync(
            It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Account?)null);
        SetupLockMock();

        var request = new DepositRequest(Guid.NewGuid(), 1_000, null);

        // Act
        var act = () => _sut.DepositAsync(request);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    // =====================================================
    // Withdraw Tests
    // =====================================================

    [Fact]
    public async Task WithdrawAsync_SufficientFunds_DecreasesBalance()
    {
        // Arrange
        var account = CreateTestAccount(balance: 10_000);
        SetupAccountMock(account);
        SetupLockMock();
        SetupDailyLimitMock(0);

        var request = new WithdrawRequest(account.Id, 3_000, null);

        // Act
        var result = await _sut.WithdrawAsync(request);

        // Assert
        result.BalanceBefore.Should().Be(10_000);
        result.BalanceAfter.Should().Be(7_000);
        result.Type.Should().Be("Withdrawal");
    }

    [Fact]
    public async Task WithdrawAsync_InsufficientFunds_ThrowsException()
    {
        // Arrange
        var account = CreateTestAccount(balance: 1_000);
        SetupAccountMock(account);
        SetupLockMock();

        var request = new WithdrawRequest(account.Id, 5_000, null);

        // Act
        var act = () => _sut.WithdrawAsync(request);

        // Assert
        await act.Should().ThrowAsync<InsufficientFundsException>();
    }

    [Fact]
    public async Task WithdrawAsync_ExceedsDailyLimit_ThrowsException()
    {
        // Arrange
        var account = CreateTestAccount(balance: 100_000);
        SetupAccountMock(account);
        SetupLockMock();
        SetupDailyLimitMock(45_000); // ถอนไปแล้ว 45,000 วันนี้

        var request = new WithdrawRequest(account.Id, 10_000, null);
        // 45,000 + 10,000 = 55,000 > daily limit 50,000

        // Act
        var act = () => _sut.WithdrawAsync(request);

        // Assert
        await act.Should().ThrowAsync<DailyLimitExceededException>();
    }

    // =====================================================
    // Transfer Tests
    // =====================================================

    [Fact]
    public async Task TransferAsync_SameAccount_ThrowsArgumentException()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var request = new TransferRequest(accountId, accountId, 1_000, null);

        // Act
        var act = () => _sut.TransferAsync(request);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("Cannot transfer to the same account.");
    }

    // =====================================================
    // Helpers
    // =====================================================

    private static Account CreateTestAccount(
        decimal balance = 10_000,
        AccountStatus status = AccountStatus.Active)
    {
        return new Account
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            AccountNumber = "1234-5678-9012",
            Type = AccountType.Savings,
            Currency = "THB",
            Balance = balance,
            AvailableBalance = balance,
            DailyWithdrawalLimit = 50_000,
            Status = status
        };
    }

    private void SetupAccountMock(Account account)
    {
        _unitOfWork.Setup(x => x.Accounts.GetByIdForUpdateAsync(
            account.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        _unitOfWork.Setup(x => x.Accounts.Update(It.IsAny<Account>()));
        _unitOfWork.Setup(x => x.Transactions.AddAsync(
            It.IsAny<Transaction>(), It.IsAny<CancellationToken>()));
        _unitOfWork.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
    }

    private void SetupLockMock()
    {
        _cache.Setup(x => x.AcquireLockAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>()))
            .ReturnsAsync(true);

        _cache.Setup(x => x.ReleaseLockAsync(
            It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(true);
    }

    private void SetupDailyLimitMock(decimal todayTotal)
    {
        _unitOfWork.Setup(x => x.Transactions.GetTodayWithdrawalTotalAsync(
            It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(todayTotal);
    }
}
```

### 6.2 Auth Service Tests

```csharp
// Banking.Tests.Unit/Services/AuthServiceTests.cs

using Banking.Application.DTOs;
using Banking.Application.Services;
using Banking.Domain.Entities;
using Banking.Domain.Exceptions;
using Banking.Domain.Interfaces;
using FluentAssertions;
using Moq;

namespace Banking.Tests.Unit.Services;

public class AuthServiceTests
{
    private readonly Mock<IUnitOfWork> _unitOfWork;
    private readonly Mock<IJwtService> _jwtService;
    private readonly AuthService _sut;

    public AuthServiceTests()
    {
        _unitOfWork = new Mock<IUnitOfWork>();
        _jwtService = new Mock<IJwtService>();
        _sut = new AuthService(_unitOfWork.Object, _jwtService.Object);
    }

    [Fact]
    public async Task RegisterAsync_DuplicateEmail_ThrowsDuplicateException()
    {
        // Arrange
        _unitOfWork.Setup(x => x.Users.EmailExistsAsync(
            It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var request = new RegisterRequest(
            "John", "Doe", "existing@test.com",
            "0812345678", "Password1", "Password1");

        // Act
        var act = () => _sut.RegisterAsync(request);

        // Assert
        await act.Should().ThrowAsync<DuplicateException>()
            .WithMessage("Email already registered.");
    }

    [Fact]
    public async Task RegisterAsync_PasswordMismatch_ThrowsArgumentException()
    {
        // Arrange
        var request = new RegisterRequest(
            "John", "Doe", "test@test.com",
            "0812345678", "Password1", "DifferentPassword");

        // Act
        var act = () => _sut.RegisterAsync(request);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("Passwords do not match.");
    }

    [Fact]
    public async Task LoginAsync_WrongPassword_IncrementsFailedAttempts()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "test@test.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("CorrectPassword"),
            IsLocked = false,
            FailedLoginAttempts = 0
        };

        _unitOfWork.Setup(x => x.Users.GetByEmailAsync(
            It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var request = new LoginRequest("test@test.com", "WrongPassword");

        // Act
        var act = () => _sut.LoginAsync(request);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
        user.FailedLoginAttempts.Should().Be(1);
    }

    [Fact]
    public async Task LoginAsync_LockedAccount_ThrowsAccountLockedException()
    {
        // Arrange
        var user = new User
        {
            Email = "test@test.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("password"),
            IsLocked = true
        };

        _unitOfWork.Setup(x => x.Users.GetByEmailAsync(
            It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var request = new LoginRequest("test@test.com", "password");

        // Act
        var act = () => _sut.LoginAsync(request);

        // Assert
        await act.Should().ThrowAsync<AccountLockedException>();
    }
}
```

---

## ขั้นตอนที่ 7: Integration Tests

```
📁 Banking.Tests.Integration/

ทำไม: ทดสอบ API จริง (HTTP → Controller → Service → Database)
ใช้ WebApplicationFactory สร้าง in-memory test server
ใช้ Respawn reset database ก่อนทุก test

ต่างจาก Unit Test:
  Unit Test: mock ทุกอย่าง → ทดสอบ logic เท่านั้น
  Integration Test: ใช้ database + Redis จริง → ทดสอบ end-to-end
```

### 7.1 Test Infrastructure

```csharp
// Banking.Tests.Integration/BankingApiFactory.cs

using Banking.Infrastructure.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Banking.Tests.Integration;

/// <summary>
/// Custom WebApplicationFactory — สร้าง test server ที่ใช้ test database
///
/// ทำไมต้อง custom:
///   Default factory ใช้ database เดียวกับ development
///   เราต้องการ database แยก (banking_test) ที่ reset ได้
/// </summary>
public class BankingApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            // ลบ DbContext registration เดิม
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            if (descriptor is not null) services.Remove(descriptor);

            // ใช้ test database
            services.AddDbContext<AppDbContext>(options =>
                options.UseNpgsql(
                    "Host=localhost;Database=banking_test;Username=postgres;Password=root1234"));
        });
    }
}
```

### 7.2 API Integration Tests

```csharp
// Banking.Tests.Integration/Controllers/AuthControllerTests.cs

using System.Net;
using System.Net.Http.Json;
using Banking.Application.DTOs;
using FluentAssertions;

namespace Banking.Tests.Integration.Controllers;

public class AuthControllerTests : IClassFixture<BankingApiFactory>
{
    private readonly HttpClient _client;

    public AuthControllerTests(BankingApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Register_ValidData_Returns201WithToken()
    {
        // Arrange
        var request = new RegisterRequest(
            "Test", "User",
            $"test-{Guid.NewGuid():N}@test.com",  // unique email
            $"08{Random.Shared.Next(10000000, 99999999)}",
            "Password1", "Password1");

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/register", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var result = await response.Content
            .ReadFromJsonAsync<ApiResponse<AuthResponse>>();

        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Data!.AccessToken.Should().NotBeNullOrEmpty();
        result.Data.UserId.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Login_WrongPassword_Returns400()
    {
        // Arrange
        var request = new LoginRequest("nonexistent@test.com", "wrong");

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/login", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Profile_NoToken_Returns401()
    {
        // Act
        var response = await _client.GetAsync("/api/auth/profile");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
```

---

## ขั้นตอนที่ 8: ลงทะเบียน Services ใน Program.cs

```csharp
// เพิ่มใน Program.cs

// ===== Security Services =====
builder.Services.AddScoped<PinService>();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<IFraudDetectionService, FraudDetectionService>();

// ===== Middleware Pipeline (เพิ่ม) =====
app.UseMiddleware<ExceptionMiddleware>();
app.UseHttpsRedirection();
app.UseCors("AllowFrontend");
app.UseAuthentication();
app.UseMiddleware<TokenBlacklistMiddleware>();
app.UseMiddleware<RateLimitMiddleware>();
app.UseMiddleware<IdempotencyMiddleware>();     // ← เพิ่ม: ป้องกัน duplicate
app.UseMiddleware<AdminIpWhitelistMiddleware>(); // ← เพิ่ม: IP whitelist
app.UseAuthorization();
app.UseMiddleware<AuditMiddleware>();           // ← เพิ่ม: หลัง auth (ได้ userId)
app.MapControllers();
app.MapHub<NotificationHub>("/hubs/notifications");
```

---

## Checklist — สิ่งที่ต้องเสร็จก่อนไป Phase 5

```
PIN Verification:
☐ User entity: เพิ่ม PinHash, FailedPinAttempts, IsTransactionLocked
☐ PinDtos: SetPinRequest, ChangePinRequest, XxxWithPinRequest
☐ PinValidators: SetPinRequestValidator, ChangePinRequestValidator
☐ PinService: SetPin, ChangePin, VerifyPin (3 attempts → lock)
☐ AuthController: POST pin/set, POST pin/change
☐ AdminController: POST users/{id}/reset-pin-lock
☐ TransactionService: เรียก VerifyPin ก่อนทุกธุรกรรม
☐ Migration: AddPinFields

Idempotency:
☐ IdempotencyMiddleware: X-Idempotency-Key header → Redis cache
☐ Response caching สำหรับ duplicate requests (TTL 24h)
☐ เฉพาะ POST/PUT/PATCH (ไม่ทำ GET/DELETE)

Audit Logging:
☐ IAuditService interface (Application layer)
☐ AuditService implementation — ใช้ DbContext แยก (ไม่ rollback กับ main txn)
☐ AuditMiddleware: บันทึก HTTP write operations
☐ TransactionService: บันทึก Deposit, Withdraw, Transfer
☐ AuthService: บันทึก Login, LoginFailed, Register

Fraud Detection:
☐ IFraudDetectionService interface + FraudCheckResult record
☐ FraudDetectionService: Large amount, High frequency, Unusual hours, Near daily limit
☐ TransactionService: เรียก Fraud check ก่อนทำธุรกรรม
☐ Critical risk → block, Medium/High → flag + audit

Security Hardening:
☐ AdminIpWhitelistMiddleware สำหรับ /api/admin/*
☐ DataMasking: MaskAccountNumber, MaskEmail
☐ Health Check endpoint: /health (DB + Redis status)
☐ appsettings: Security.AdminAllowedIps

Unit Tests:
☐ TransactionServiceTests: Deposit (valid, zero, frozen, not found)
☐ TransactionServiceTests: Withdraw (valid, insufficient, daily limit)
☐ TransactionServiceTests: Transfer (same account)
☐ AuthServiceTests: Register (duplicate email, password mismatch)
☐ AuthServiceTests: Login (wrong password, locked account)

Integration Tests:
☐ BankingApiFactory: custom WebApplicationFactory + test database
☐ AuthControllerTests: Register 201, Login 400, Profile 401
☐ dotnet test ผ่าน 0 failures

Infrastructure:
☐ Program.cs: ลงทะเบียน PinService, AuditService, FraudDetectionService
☐ Middleware pipeline: เพิ่ม IdempotencyMiddleware, AdminIpWhitelistMiddleware, AuditMiddleware
☐ Build ผ่าน 0 errors

เมื่อ checklist ครบ → พร้อมไป Phase 5: Docker + Load Balancing + Scaling
```

---

## Troubleshooting

### "PIN is not set" ตอนทำธุรกรรม
```
User ยังไม่ตั้ง PIN:
  POST /api/auth/pin/set
  Body: { "pin": "123456", "confirmPin": "123456" }
```

### "Transactions are locked" หลัง PIN ผิดเกิน
```
Admin reset:
  POST /api/admin/users/{id}/reset-pin-lock
  → user ต้องตั้ง PIN ใหม่
```

### Idempotency key ไม่ทำงาน
```
1. ตรวจว่าส่ง header: X-Idempotency-Key: unique-value
2. เฉพาะ POST/PUT/PATCH
3. เก็บ cache เฉพาะ 2xx responses
4. ดู Redis: redis-cli GET "banking:idempotency:{key}"
```

### Audit logs ไม่ถูกบันทึก
```
1. AuditService ใช้ IServiceScopeFactory → สร้าง DbContext ใหม่
2. ตรวจว่า AuditMiddleware อยู่หลัง Authorization (ต้องได้ userId)
3. Audit failure ไม่ throw → ตรวจ logs สำหรับ warnings
```

### Unit test mock ไม่ทำงาน
```
1. ตรวจว่า Setup ก่อน Act
2. Moq: It.IsAny<T>() matches ทุก value
3. ReturnsAsync() สำหรับ async methods
4. FluentAssertions: .Should().ThrowAsync<T>() สำหรับ async
```

### Integration test database error
```
1. สร้าง test database: CREATE DATABASE banking_test
2. Connection string ใน BankingApiFactory ต้องถูก
3. ใช้ unique email ในทุก test (ป้องกัน conflict)
```
