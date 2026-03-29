---
name: Banking API Endpoint
description: สร้าง ASP.NET Core API endpoint ครบวงจร พร้อม Controller, Route, Authorization, Swagger docs
command: bank-endpoint
argument-hint: "<HTTP_METHOD> <route> [description]"
allowed-tools:
  - Read
  - Write
  - Edit
  - Glob
  - Bash
  - Grep
---

# Banking API Endpoint Skill

คุณคือผู้เชี่ยวชาญ ASP.NET Core Minimal/Controller API สำหรับ Banking System

## Input
- **argument แรก:** HTTP Method (required) — `GET`, `POST`, `PUT`, `DELETE`
- **argument ที่สอง:** Route (required) — เช่น `/api/transactions/deposit`
- **argument ที่สาม:** Description (optional) — อธิบายสั้นๆ ว่า endpoint ทำอะไร

## ขั้นตอนการทำงาน

### Step 1: อ่าน Context
1. อ่าน `Banking.Api/Controllers/` เพื่อดู existing controllers และ pattern
2. อ่าน `Banking.Api/Program.cs` เพื่อดู middleware และ service registration
3. อ่าน `Banking.Application/` เพื่อดู existing Commands/Queries ที่เกี่ยวข้อง
4. อ่าน `Banking.Domain/Entities/` เพื่อเข้าใจ data model

### Step 2: สร้าง/อัปเดต Controller
- ใช้ pattern: `[ApiController]` + `[Route("api/[controller]")]`
- Inject `ISender` (MediatR) ผ่าน constructor
- ทุก action method ต้องมี:
  - `[HttpGet/Post/Put/Delete]` attribute พร้อม route
  - `[ProducesResponseType]` สำหรับทุก possible status code
  - `[Authorize]` ถ้าต้อง authentication (default: ทุก financial endpoint)
  - XML comment สำหรับ Swagger documentation

### Step 3: Controller Pattern
```csharp
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class {Name}Controller : ControllerBase
{
    private readonly ISender _sender;

    public {Name}Controller(ISender sender) => _sender = sender;

    /// <summary>
    /// {Description}
    /// </summary>
    [Http{Method}("{route}")]
    [ProducesResponseType(typeof({Response}), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> {ActionName}([FromBody] {Request} request)
    {
        var command = new {Command}(/* map from request */);
        var result = await _sender.Send(command);
        return Ok(result);
    }
}
```

### Step 4: API Conventions สำหรับ Banking
- **POST** (create/action): return `201 Created` หรือ `200 OK`
- **GET** (read): return `200 OK` + pagination headers ถ้าเป็น list
- Financial endpoints ต้อง: `[Authorize]` + rate limiting attribute
- ทุก endpoint ต้อง log via audit middleware
- Error responses ใช้ `ProblemDetails` format

### Step 5: Endpoint Reference จาก Blueprint
```
Auth:
POST   /api/auth/register          ← [AllowAnonymous]
POST   /api/auth/login             ← [AllowAnonymous]
POST   /api/auth/refresh           ← [AllowAnonymous]
POST   /api/auth/logout            ← [Authorize]

Accounts:
GET    /api/accounts               ← [Authorize] user's accounts
GET    /api/accounts/{id}          ← [Authorize]
GET    /api/accounts/{id}/balance  ← [Authorize] real-time from Redis
GET    /api/accounts/{id}/statement ← [Authorize] with date range

Transactions:
POST   /api/transactions/deposit   ← [Authorize] + rate limit
POST   /api/transactions/withdraw  ← [Authorize] + rate limit
POST   /api/transactions/transfer  ← [Authorize] + rate limit
GET    /api/transactions/{id}      ← [Authorize]
GET    /api/transactions           ← [Authorize] + pagination

Admin:
GET    /api/admin/dashboard        ← [Authorize(Roles = "Admin")]
POST   /api/admin/accounts/{id}/freeze ← [Authorize(Roles = "Admin")]
```

### Step 6: ตรวจสอบ
1. Build project: `dotnet build`
2. ตรวจว่า MediatR handler มีอยู่สำหรับ Command/Query ที่ใช้
3. ถ้ายังไม่มี handler → แนะนำให้ใช้ `/bank-scaffold` ก่อน

## ตัวอย่างการใช้งาน
```
/bank-endpoint POST /api/transactions/deposit ฝากเงินเข้าบัญชี
/bank-endpoint GET /api/accounts/{id}/balance ดูยอดเงินแบบ real-time
/bank-endpoint POST /api/auth/login เข้าสู่ระบบด้วย email + password
```
