# Phase 3: Redis Token Management & Rate Limiting — Learning Guide

## สิ่งที่จะได้เรียนรู้ใน Phase นี้

- การใช้ **Redis** เป็น In-Memory Data Store สำหรับ Token Management
- การทำ **Refresh Token Flow** แบบ Secure (เก็บ/ตรวจสอบ/หมุนเวียน)
- การทำ **JWT Blacklist** เพื่อให้ Logout ทำงานจริง
- การใช้ **ASP.NET Core Rate Limiting** ป้องกัน Brute Force / Abuse

---

## สถานะปัจจุบัน (Phase 2 ที่ทำเสร็จแล้ว)

### สิ่งที่มีอยู่แล้ว

| สิ่งที่มี | ที่อยู่ | สถานะ |
|---|---|---|
| `StackExchange.Redis 2.12.8` | `Banking.Infrastructure.csproj` | ลง package แล้ว แต่ยังไม่ใช้ |
| `RefreshTokenRequest` DTO | `Banking.Application/DTOs/AuthDtos.cs` | สร้างแล้ว แต่ยังไม่มี endpoint ใช้ |
| `GetUserIdFromExpiredToken()` | `JwtService.cs` | ทำงานได้ — validate token โดยไม่ตรวจ lifetime |
| JTI claim ใน access token | `JwtService.cs:35` | `Guid.NewGuid().ToString()` ใส่ไว้แล้ว |
| Logout endpoint | `AuthController.cs:86-91` | Placeholder — ยังไม่ทำอะไร |

### TODO Comments ที่ต้องแก้

```csharp
// AuthService.cs:81
// TODO: เก็บ refreshToken ใน Redis (Phase 3)

// AuthController.cs:88
// TODO Phase 3: เพิ่ม JTI ลง Redis blacklist
```

---

## ภาพรวม Phase 3

```
┌──────────────────────────────────────────────────────────┐
│                    Phase 3 Features                       │
├──────────────────────────────────────────────────────────┤
│                                                          │
│  Step 1: Redis Foundation                                │
│    └─ IRedisService, RedisService, Config, DI            │
│                                                          │
│  Step 2: Refresh Token Storage                           │
│    └─ เก็บ refresh token ใน Redis ตอน register/login     │
│                                                          │
│  Step 3: Refresh Token Endpoint                          │
│    └─ POST /api/auth/refresh                             │
│                                                          │
│  Step 4: JWT Blacklist (Logout)                          │
│    └─ Blacklist middleware + LogoutAsync                  │
│                                                          │
│  Step 5: Rate Limiting                                   │
│    └─ 3 policies (fixed, auth, transaction)              │
│                                                          │
└──────────────────────────────────────────────────────────┘
```

---

## Redis Key Schema (สรุปรวม)

```
┌──────────────────────┬──────────┬─────────┬──────────────┐
│ Key Pattern          │ Value    │ TTL     │ ใช้ทำอะไร     │
├──────────────────────┼──────────┼─────────┼──────────────┤
│ refresh:{userId}     │ Base64   │ 7 days  │ Refresh token│
│ blacklist:{jti}      │ "1"     │ ≤15 min │ Revoked JWT  │
└──────────────────────┴──────────┴─────────┴──────────────┘
```

---

## ลำดับไฟล์ที่ต้องสร้าง/แก้

### ไฟล์ใหม่ 3 ไฟล์

| # | ไฟล์ | Layer | Step |
|---|---|---|---|
| 1 | `Banking.Application/Services/IRedisService.cs` | Application | 1 |
| 2 | `Banking.Infrastructure/Services/RedisService.cs` | Infrastructure | 1 |
| 3 | `Banking.Api/Middleware/JwtBlacklistMiddleware.cs` | Api | 4 |

### ไฟล์แก้ไข 7 ไฟล์

| # | ไฟล์ | แก้อะไร | Step |
|---|---|---|---|
| 1 | `appsettings.json` | เพิ่ม Redis config | 1 |
| 2 | `appsettings.Development.json` | เพิ่ม Redis config | 1 |
| 3 | `Program.cs` | Redis DI, Middleware, Rate Limiting | 1,4,5 |
| 4 | `IJwtService.cs` | เพิ่ม GetJtiFromToken | 4 |
| 5 | `JwtService.cs` | Implement GetJtiFromToken | 4 |
| 6 | `AuthService.cs` | Redis dependency, Store token, RefreshAsync, LogoutAsync | 2,3,4 |
| 7 | `AuthController.cs` | Refresh endpoint, Logout update, Rate limit | 3,4,5 |

---

## Middleware Pipeline สุดท้าย

```
Request
  │
  ▼
ExceptionMiddleware          ← จับ exception ทั้งหมด
  │
  ▼
UseHttpsRedirection          ← redirect HTTP → HTTPS
  │
  ▼
UseAuthentication            ← parse JWT → populate User claims
  │
  ▼
JwtBlacklistMiddleware       ← ★ NEW: ตรวจ JTI กับ Redis
  │
  ▼
UseAuthorization             ← ตรวจ [Authorize] attribute
  │
  ▼
UseRateLimiter               ← ★ NEW: ตรวจ rate limit
  │
  ▼
MapControllers               ← route ไปหา controller
```

---

## Prerequisites: ติดตั้ง Redis

**Docker (แนะนำ):**
```bash
docker run -d --name redis -p 6379:6379 redis:alpine
```

**ตรวจสอบ:**
```bash
redis-cli ping
# ตอบ PONG = ใช้ได้
```

**Redis CLI ที่ใช้บ่อย:**
```bash
KEYS *                        # ดู key ทั้งหมด
GET refresh:{userId}          # ดูค่า refresh token
TTL refresh:{userId}          # ดู TTL เหลือกี่วินาที
EXISTS blacklist:{jti}        # เช็คว่าถูก blacklist ไหม
DEL refresh:{userId}          # ลบ key
FLUSHALL                      # ลบทุก key (ระวัง!)
```

---

# Step 1: Redis Foundation

## เป้าหมาย
สร้าง Redis service layer ที่ทุก feature ใน Phase 3 จะใช้ร่วมกัน

## ความรู้พื้นฐาน

**Redis คืออะไร?**
- In-Memory Data Store — เก็บข้อมูลใน RAM ทำให้ read/write เร็วมาก (microsecond level)
- รองรับ TTL (Time To Live) — ข้อมูลหมดอายุอัตโนมัติ เหมาะกับ token ที่มีอายุจำกัด
- ต่างจาก Database ตรงที่ ข้อมูลจะหายเมื่อ restart (ถ้าไม่เปิด persistence)

**ทำไมใช้ Redis แทน Database?**
- Token ตรวจสอบ **ทุก request** — ถ้าใช้ DB จะเป็น bottleneck
- Token มีอายุจำกัด — Redis TTL ลบอัตโนมัติ ไม่ต้อง cleanup เอง
- ไม่ต้องการ relational data — แค่ key-value pair

**StackExchange.Redis**
- เป็น .NET Redis client ที่นิยมที่สุด
- ใช้ `IConnectionMultiplexer` เป็น connection pool (ต้องเป็น **Singleton**)
- `IDatabase` ใช้ execute commands (ได้จาก `multiplexer.GetDatabase()`)

---

## 1.1 — เพิ่ม Redis Connection String

### ไฟล์: `Banking.Api/appsettings.json`

**ของเดิม:**
```json
{
  "Jwt": { ... },
  "Logging": { ... },
  "AllowedHosts": "*"
}
```

**แก้เป็น — เพิ่ม `Redis` section:**
```json
{
  "Jwt": { ... },
  "Logging": { ... },
  "AllowedHosts": "*",
  "Redis": {
    "ConnectionString": "localhost:6379"
  }
}
```

### ไฟล์: `Banking.Api/appsettings.Development.json`

**ของเดิม:**
```json
{
  "ConnectionStrings": { ... },
  "Logging": { ... },
  "Swagger": {
    "Enabled": true
  }
}
```

**แก้เป็น — เพิ่ม `Redis` section:**
```json
{
  "ConnectionStrings": { ... },
  "Logging": { ... },
  "Swagger": {
    "Enabled": true
  },
  "Redis": {
    "ConnectionString": "localhost:6379"
  }
}
```

---

## 1.2 — สร้าง IRedisService Interface

### ไฟล์ใหม่: `Banking.Application/Services/IRedisService.cs`

```csharp
namespace Banking.Application.Services;

public interface IRedisService
{
    /// <summary>
    /// เก็บค่า + ตั้ง TTL (ถ้าระบุ)
    /// ใช้ตอน: เก็บ refresh token, เพิ่ม blacklist
    /// </summary>
    Task SetAsync(string key, string value, TimeSpan? expiry = null);

    /// <summary>
    /// ดึงค่า — return null ถ้าไม่มีหรือหมดอายุ
    /// ใช้ตอน: ตรวจสอบ refresh token
    /// </summary>
    Task<string?> GetAsync(string key);

    /// <summary>
    /// ลบ key
    /// ใช้ตอน: logout (ลบ refresh token)
    /// </summary>
    Task<bool> DeleteAsync(string key);

    /// <summary>
    /// เช็คว่า key มีอยู่ไหม
    /// ใช้ตอน: ตรวจ blacklist
    /// </summary>
    Task<bool> ExistsAsync(string key);
}
```

> **ทำไม Interface อยู่ใน Application layer?**
> ตาม Clean Architecture — Business logic (Application) **กำหนดสัญญา** (interface)
> ส่วน Infrastructure **ทำตามสัญญา** (implementation)
> เหมือนกับ `IJwtService` ที่อยู่ใน `Banking.Application/Services/` อยู่แล้ว

---

## 1.3 — สร้าง RedisService Implementation

### ไฟล์ใหม่: `Banking.Infrastructure/Services/RedisService.cs`

```csharp
using Banking.Application.Services;
using StackExchange.Redis;

namespace Banking.Infrastructure.Services;

/// <summary>
/// Redis Service Implementation
///
/// IConnectionMultiplexer:
///   - เป็น connection pool ไปยัง Redis server
///   - ต้องเป็น Singleton (สร้างครั้งเดียว ใช้ร่วมกันทั้ง app)
///   - เรียก GetDatabase() เพื่อได้ IDatabase สำหรับ execute commands
///
/// IDatabase:
///   - StringSetAsync = SET key value [EX seconds]
///   - StringGetAsync = GET key
///   - KeyDeleteAsync = DEL key
///   - KeyExistsAsync = EXISTS key
/// </summary>
public class RedisService : IRedisService
{
    private readonly IDatabase _db;

    public RedisService(IConnectionMultiplexer redis)
    {
        _db = redis.GetDatabase();
    }

    public async Task SetAsync(string key, string value, TimeSpan? expiry = null)
    {
        // ⚠️ StackExchange.Redis 2.12.x ใช้ Expiration type แทน TimeSpan
        // ถ้า version เก่ากว่า อาจส่ง TimeSpan ตรงๆ ได้
        if (expiry.HasValue)
            await _db.StringSetAsync(key, value, new Expiration(expiry.Value));
        else
            await _db.StringSetAsync(key, value);
    }

    public async Task<string?> GetAsync(string key)
    {
        var value = await _db.StringGetAsync(key);
        // RedisValue.HasValue = false เมื่อ key ไม่มีหรือหมดอายุ
        return value.HasValue ? value.ToString() : null;
    }

    public async Task<bool> DeleteAsync(string key)
    {
        return await _db.KeyDeleteAsync(key);
    }

    public async Task<bool> ExistsAsync(string key)
    {
        return await _db.KeyExistsAsync(key);
    }
}
```

> **ข้อควรระวัง `Expiration` vs `TimeSpan`:**
> StackExchange.Redis 2.12.x เปลี่ยน API — parameter ที่ 3 ของ `StringSetAsync`
> เป็น type `Expiration` ไม่ใช่ `TimeSpan` โดยตรงอีกแล้ว
> ถ้า compile error `CS1503: cannot convert from 'TimeSpan?' to 'Expiration'`
> ให้ใช้ `new Expiration(timeSpan)` ครอบ ดังโค้ดด้านบน

---

## 1.4 — Register ใน DI Container

### ไฟล์: `Banking.Api/Program.cs`

**ของเดิม (บรรทัด 1-11):**
```csharp
using Banking.Application.Services;
using Banking.Application.Services;
using Banking.Domain.Interfaces;
using Banking.Infrastructure.Data;
using Banking.Infrastructure.Repositories;
using Banking.Infrastructure.Services;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
```

**แก้เป็น — เพิ่ม using ที่ต้องใช้ (ลบ duplicate ด้วย):**
```csharp
using Banking.Application.Services;
using Banking.Domain.Interfaces;
using Banking.Infrastructure.Data;
using Banking.Infrastructure.Repositories;
using Banking.Infrastructure.Services;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;          // ★ NEW (Step 5)
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using StackExchange.Redis;                         // ★ NEW (Step 1)
using System.Security.Claims;                      // ★ NEW (Step 5)
using System.Text;
using System.Text.Json;                            // ★ NEW (Step 5)
using System.Threading.RateLimiting;               // ★ NEW (Step 5)
```

**ของเดิม (บรรทัด 25-26):**
```csharp
// ===== JWT Authentication =====
builder.Services.AddAuthentication(...)
```

**แก้เป็น — เพิ่ม Redis section ก่อน JWT:**
```csharp
// ===== Redis =====
// IConnectionMultiplexer เป็น Singleton เพราะ:
//   - มันจัดการ connection pool ภายในตัว
//   - สร้างครั้งเดียว ใช้ร่วมกันทั้ง app (Best Practice จาก StackExchange docs)
//   - ต่างจาก DbContext ที่ต้อง Scoped เพราะ EF Core track changes per request
builder.Services.AddSingleton<IConnectionMultiplexer>(
    ConnectionMultiplexer.Connect(builder.Configuration["Redis:ConnectionString"]!));
builder.Services.AddScoped<IRedisService, RedisService>();

// ===== JWT Authentication =====
builder.Services.AddAuthentication(...)
```

> **ทำไม IConnectionMultiplexer เป็น Singleton แต่ IRedisService เป็น Scoped?**
> `IConnectionMultiplexer` = connection pool ตัวเดียวทั้ง app (StackExchange.Redis best practice)
> `IRedisService` = Scoped เพื่อให้ตรงกับ lifetime ของ services อื่นๆ ที่ inject มัน (AuthService เป็น Scoped)

### ทดสอบ Step 1
```bash
dotnet build    # ต้อง 0 errors
```

---

# Step 2: Refresh Token Storage

## เป้าหมาย
เก็บ Refresh Token ใน Redis ทุกครั้งที่ register/login

## ความรู้พื้นฐาน

**ทำไมต้องเก็บ Refresh Token?**

ปัจจุบัน refresh token ถูก generate แล้วส่งให้ client ทันที **โดยไม่ได้เก็บไว้ฝั่ง server**
- ไม่สามารถ **ตรวจสอบ** ว่า refresh token ที่ส่งมาถูกต้องหรือไม่
- ไม่สามารถ **เพิกถอน** ได้ (ถ้า token ถูกขโมย ก็ใช้ได้ตลอด 7 วัน)
- **Logout ไม่มีผล** — token ยังใช้ได้อยู่

**Redis Key Design:**
```
Key:   refresh:{userId}       ← 1 user = 1 refresh token
Value: Base64 refresh token   ← ค่าจริงที่ generate
TTL:   7 วัน                  ← ตรงกับอายุ refresh token
```

> **ทำไม 1 user = 1 token?**
> login ใหม่ = replace token เก่าอัตโนมัติ (เหมือน "force logout อุปกรณ์อื่น")
> ถ้าต้องการ multi-device ให้ใช้ key `refresh:{userId}:{deviceId}` แทน (นอก scope Phase 3)

---

## 2.1 — แก้ไข AuthService

### ไฟล์: `Banking.Application/Services/AuthService.cs`

**ของเดิม (บรรทัด 11-19) — constructor:**
```csharp
public class AuthService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IJwtService _jwtService;

    public AuthService(IUnitOfWork unitOfWork, IJwtService jwtService)
    {
        _unitOfWork = unitOfWork;
        _jwtService = jwtService;
    }
```

**แก้เป็น — เพิ่ม IRedisService:**
```csharp
public class AuthService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IJwtService _jwtService;
    private readonly IRedisService _redisService;  // ★ NEW

    public AuthService(IUnitOfWork unitOfWork, IJwtService jwtService, IRedisService redisService)  // ★ เพิ่ม parameter
    {
        _unitOfWork = unitOfWork;
        _jwtService = jwtService;
        _redisService = redisService;  // ★ NEW
    }
```

> **เรียนรู้:** ASP.NET Core DI จะ resolve IRedisService ให้อัตโนมัติ
> เพราะเรา register ไว้ใน Program.cs แล้ว (Step 1.4)
> ไม่ต้องแก้อะไรเพิ่มใน Program.cs

---

**ของเดิม (บรรทัด 78-91) — RegisterAsync ส่วนสร้าง token:**
```csharp
        var accessToken = _jwtService.GenerateAccessToken(user);
        var refreshToken = _jwtService.GenerateRefreshToken();

        // TODO: เก็บ refreshToken ใน Redis (Phase 3)

        return new AuthResponse(
            UserId: user.Id,
            FullName: user.FullName,
            Email: user.Email,
            AccessToken: accessToken,
            RefreshToken: refreshToken,
            AccessTokenExpiry: DateTime.UtcNow.AddMinutes(15)
        );
```

**แก้เป็น — แทน TODO ด้วย Redis storage:**
```csharp
        var accessToken = _jwtService.GenerateAccessToken(user);
        var refreshToken = _jwtService.GenerateRefreshToken();

        // ★ เก็บ refresh token ใน Redis (แทนที่ TODO)
        await _redisService.SetAsync(
            $"refresh:{user.Id}",       // key: refresh:xxxxxxxx-xxxx-xxxx-xxxx
            refreshToken,                // value: Base64 string ยาวๆ
            TimeSpan.FromDays(7)         // TTL: 7 วัน แล้วหายเอง
        );

        return new AuthResponse(
            UserId: user.Id,
            FullName: user.FullName,
            Email: user.Email,
            AccessToken: accessToken,
            RefreshToken: refreshToken,
            AccessTokenExpiry: DateTime.UtcNow.AddMinutes(15)
        );
```

---

**ของเดิม (บรรทัด 137-148) — LoginAsync ส่วนสร้าง token:**
```csharp
        var accessToken = _jwtService.GenerateAccessToken(user);
        var refreshToken = _jwtService.GenerateRefreshToken();

        return new AuthResponse(
            UserId: user.Id,
            FullName: user.FullName,
            Email: user.Email,
            AccessToken: accessToken,
            RefreshToken: refreshToken,
            AccessTokenExpiry: DateTime.UtcNow.AddMinutes(15)
        );
    }
```

**แก้เป็น — เพิ่ม Redis storage เหมือน register:**
```csharp
        var accessToken = _jwtService.GenerateAccessToken(user);
        var refreshToken = _jwtService.GenerateRefreshToken();

        // ★ เก็บ refresh token ใน Redis
        // ถ้า user login ใหม่ token เก่าจะถูก overwrite ทันที
        // = อุปกรณ์อื่นที่ถือ token เก่าจะ refresh ไม่ได้
        await _redisService.SetAsync(
            $"refresh:{user.Id}",
            refreshToken,
            TimeSpan.FromDays(7)
        );

        return new AuthResponse(
            UserId: user.Id,
            FullName: user.FullName,
            Email: user.Email,
            AccessToken: accessToken,
            RefreshToken: refreshToken,
            AccessTokenExpiry: DateTime.UtcNow.AddMinutes(15)
        );
    }
```

### ทดสอบ Step 2
```bash
dotnet build                              # ต้อง 0 errors
redis-server                              # เปิด Redis
dotnet run --project Banking.Api          # รัน API

# ลอง Register แล้วตรวจ Redis:
redis-cli GET "refresh:{userId ที่ได้จาก response}"
# ควรเห็น Base64 string

redis-cli TTL "refresh:{userId}"
# ควรเห็น ~604800 (7 วัน ในหน่วยวินาที)
```

---

# Step 3: Refresh Token Endpoint

## เป้าหมาย
สร้าง `POST /api/auth/refresh` ให้ client ใช้ token คู่เก่าแลกเป็น token คู่ใหม่

## ความรู้พื้นฐาน

**Refresh Flow:**
```
Client                           Server                        Redis
  │                                │                              │
  │ POST /auth/refresh             │                              │
  │ Header: Bearer <expired JWT>   │                              │
  │ Body: { refreshToken: "xxx" }  │                              │
  │ ──────────────────────────────>│                              │
  │                                │                              │
  │                                │ 1. Extract userId            │
  │                                │    จาก expired JWT           │
  │                                │    (ไม่ตรวจ lifetime)        │
  │                                │                              │
  │                                │ 2. GET refresh:{userId} ────>│
  │                                │ <──── stored token ──────────│
  │                                │                              │
  │                                │ 3. Compare:                  │
  │                                │    stored == ที่ส่งมา?       │
  │                                │                              │
  │                                │ 4. Generate token คู่ใหม่    │
  │                                │                              │
  │                                │ 5. SET refresh:{userId} ────>│
  │                                │    (replace token เก่า)      │
  │                                │                              │
  │ <── { newAccessToken,          │                              │
  │       newRefreshToken }        │                              │
```

**Token Rotation:**
ทุกครั้งที่ refresh → token คู่เก่าใช้ไม่ได้อีก
ถ้า attacker ขโมย token เก่าแล้วลองใช้ → จะ fail ทันที

---

## 3.1 — เพิ่ม RefreshAsync ใน AuthService

### ไฟล์: `Banking.Application/Services/AuthService.cs`

**เพิ่ม method ใหม่ หลัง `GetProfileAsync`:**

```csharp
    // ... GetProfileAsync อยู่ข้างบน ...

    /// <summary>
    /// Refresh tokens — ใช้ expired access token + refresh token เพื่อขอ token คู่ใหม่
    ///
    /// Flow:
    /// 1. ดึง userId จาก expired access token (validate signature แต่ไม่ตรวจ lifetime)
    /// 2. ดึง stored refresh token จาก Redis แล้วเทียบกับที่ส่งมา
    /// 3. Load user จาก DB (ต้องใช้ User entity เพื่อ generate access token)
    /// 4. สร้าง token คู่ใหม่ + replace ใน Redis
    ///
    /// ⚠️ Security — Token Rotation:
    ///   refresh token เก่าจะใช้ไม่ได้อีกหลัง refresh สำเร็จ
    ///   ถ้า attacker ขโมย token เก่าไปใช้ จะ fail เพราะ Redis มี token ใหม่แล้ว
    /// </summary>
    public async Task<AuthResponse> RefreshAsync(
        string expiredAccessToken, string refreshToken, CancellationToken ct = default)
    {
        // 1. ดึง userId จาก expired token
        //    GetUserIdFromExpiredToken ตั้ง ValidateLifetime = false
        //    → ยังตรวจ signature, issuer, audience ได้ แม้ token หมดอายุ
        var userId = _jwtService.GetUserIdFromExpiredToken(expiredAccessToken)
            ?? throw new ArgumentException("Invalid access token.");

        // 2. ดึง stored refresh token จาก Redis แล้วเทียบ
        //    ถ้า null = token หมดอายุ (TTL 7 วัน) หรือถูก logout
        //    ถ้า ≠ refreshToken = มีคน refresh ไปก่อนแล้ว (token rotation)
        var storedRefreshToken = await _redisService.GetAsync($"refresh:{userId}");
        if (storedRefreshToken is null || storedRefreshToken != refreshToken)
            throw new ArgumentException("Invalid or expired refresh token.");

        // 3. Load user จาก DB
        //    ต้องใช้ User entity เพราะ GenerateAccessToken รับ User (ใส่ claims จาก User)
        var user = await _unitOfWork.Users.GetByIdAsync(userId, ct)
            ?? throw new NotFoundException("User", userId);

        // 4. สร้าง token คู่ใหม่
        var newAccessToken = _jwtService.GenerateAccessToken(user);
        var newRefreshToken = _jwtService.GenerateRefreshToken();

        // 5. Replace refresh token ใน Redis (token เก่าใช้ไม่ได้อีก)
        await _redisService.SetAsync($"refresh:{user.Id}", newRefreshToken, TimeSpan.FromDays(7));

        return new AuthResponse(
            UserId: user.Id,
            FullName: user.FullName,
            Email: user.Email,
            AccessToken: newAccessToken,
            RefreshToken: newRefreshToken,
            AccessTokenExpiry: DateTime.UtcNow.AddMinutes(15)
        );
    }
}  // ← ปิด class
```

---

## 3.2 — เพิ่ม Refresh Endpoint ใน AuthController

### ไฟล์: `Banking.Api/Controllers/AuthController.cs`

**เพิ่ม method ใหม่ ก่อน Logout:**

```csharp
    // ... Profile อยู่ข้างบน ...

    /// <summary>
    /// Refresh tokens — POST /api/auth/refresh
    ///
    /// Client ส่ง:
    ///   - Authorization header: Bearer {expired access token}
    ///   - Body: { "refreshToken": "xxx" }
    ///
    /// ⚠️ ต้องเป็น [AllowAnonymous] เพราะ:
    ///   access token หมดอายุแล้ว → JWT middleware จะ reject ถ้าไม่ bypass
    ///   เราจะ validate เองใน AuthService.RefreshAsync
    /// </summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<IActionResult> Refresh(
        [FromBody] RefreshTokenRequest request, CancellationToken ct)
    {
        // ดึง expired access token จาก Authorization header
        var authHeader = Request.Headers.Authorization.ToString();
        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
            return BadRequest(new ApiResponse<object>(false,
                "Access token is required in Authorization header."));

        // ตัด "Bearer " prefix ออก → เหลือแค่ token string
        var expiredAccessToken = authHeader["Bearer ".Length..];

        var result = await _authService.RefreshAsync(expiredAccessToken, request.RefreshToken, ct);
        return Ok(new ApiResponse<AuthResponse>(true, "Tokens refreshed successfully.", result));
    }

    // ... Logout อยู่ข้างล่าง ...
```

> **เรียนรู้ `authHeader["Bearer ".Length..]`:**
> นี่คือ C# Range syntax — `"Bearer ".Length` = 7
> `authHeader[7..]` = ตัด 7 ตัวอักษรแรกออก เหลือแค่ token
> เหมือน `authHeader.Substring(7)` แต่สั้นกว่า

> **ทำไมดึง access token จาก Header ไม่ใช่ Body?**
> เพราะ client ส่ง access token ใน Authorization header อยู่แล้ว (pattern เดิม)
> ไม่ต้องเปลี่ยน client behavior — แค่ส่งมาเหมือนเดิมแม้ token หมดอายุ

### ทดสอบ Step 3
```bash
# 1. Login ให้ได้ access + refresh token

# 2. รอ 15 นาที (หรือแก้ config AccessTokenExpirationMinutes: 1 ชั่วคราว)

# 3. POST /api/auth/refresh
#    Header: Authorization: Bearer {expired access token}
#    Body: { "refreshToken": "{refresh token จาก login}" }
#    → ควรได้ token คู่ใหม่ (200)

# 4. ใช้ refresh token เดิมอีกครั้ง
#    → ควร error "Invalid or expired refresh token." (400)
#    เพราะ token ถูก rotate แล้ว

# 5. ใช้ access token ใหม่กับ GET /api/auth/profile
#    → ควรได้ข้อมูล (200)
```

---

# Step 4: JWT Blacklist (Logout)

## เป้าหมาย
ทำให้ Logout ใช้งานได้จริง — access token ที่ถูก logout จะใช้ไม่ได้ทันที

## ความรู้พื้นฐาน

**ปัญหาของ JWT:**
JWT เป็น **stateless** — server ไม่ได้เก็บ state ว่า token ไหนยังใช้ได้
เมื่อ sign ออกไปแล้ว มันจะ valid จนหมดอายุ (15 นาที)
"logout" จึงต้อง **เพิ่ม state** ด้วยการทำ blacklist

**JTI (JWT ID):**
ทุก access token มี claim `jti` เป็น unique ID (ดูที่ `JwtService.cs:35`)
เราจะใช้ JTI เป็น key สำหรับ blacklist

**Blacklist TTL:**
```
Key:   blacklist:{jti}
Value: "1"
TTL:   เวลาที่เหลือของ access token (สูงสุด ~15 นาที)
```
เมื่อ token หมดอายุ → ใช้ไม่ได้อยู่ดี → blacklist entry auto-delete

---

## 4.1 — เพิ่ม GetJtiFromToken ใน IJwtService

### ไฟล์: `Banking.Application/Services/IJwtService.cs`

**ของเดิม:**
```csharp
public interface IJwtService
{
    string GenerateAccessToken(User user);
    string GenerateRefreshToken();
    Guid? GetUserIdFromExpiredToken(string token);
}
```

**แก้เป็น — เพิ่ม method:**
```csharp
public interface IJwtService
{
    string GenerateAccessToken(User user);
    string GenerateRefreshToken();
    Guid? GetUserIdFromExpiredToken(string token);

    /// <summary>
    /// ดึง JTI + Expiry จาก access token — ใช้ตอน logout เพื่อ blacklist
    /// return null ถ้า token parse ไม่ได้
    /// </summary>
    (string Jti, DateTime Expiry)? GetJtiFromToken(string token);
}
```

> **เรียนรู้ — Nullable Tuple:**
> `(string Jti, DateTime Expiry)?` คือ **named tuple** ที่เป็น nullable
> เรียกใช้: `result.Value.Jti`, `result.Value.Expiry`
> ตรวจ null: `if (result is not null)` หรือ `result?.Jti`

---

## 4.2 — Implement GetJtiFromToken ใน JwtService

### ไฟล์: `Banking.Infrastructure/Services/JwtService.cs`

**เพิ่ม method ใหม่ หลัง `GetUserIdFromExpiredToken`:**

```csharp
    // ... GetUserIdFromExpiredToken อยู่ข้างบน ...

    /// <summary>
    /// ดึง JTI (JWT ID) + เวลาหมดอายุ จาก access token
    ///
    /// ใช้ ReadJwtToken แทน ValidateToken เพราะ:
    ///   - ตอนเรียก method นี้ token ถูก validate แล้วโดย auth middleware
    ///   - แค่ต้องการ parse ค่าออกมา ไม่ต้อง validate ซ้ำ
    ///   - ReadJwtToken เร็วกว่า (ไม่ตรวจ signature)
    /// </summary>
    public (string Jti, DateTime Expiry)? GetJtiFromToken(string token)
    {
        var handler = new JwtSecurityTokenHandler();

        // ตรวจว่า token เป็น JWT format ที่ถูกต้อง
        if (!handler.CanReadToken(token))
            return null;

        // Parse JWT โดยไม่ validate (เร็วกว่า)
        var jwt = handler.ReadJwtToken(token);

        // jwt.Id = JTI claim (ที่เรา generate ใน GenerateAccessToken)
        var jti = jwt.Id;
        if (string.IsNullOrEmpty(jti))
            return null;

        // jwt.ValidTo = expiry time ของ token
        return (jti, jwt.ValidTo);
    }
}
```

---

## 4.3 — เพิ่ม LogoutAsync ใน AuthService

### ไฟล์: `Banking.Application/Services/AuthService.cs`

**เพิ่ม method ใหม่ หลัง `RefreshAsync` (ที่เพิ่มใน Step 3):**

```csharp
    // ... RefreshAsync อยู่ข้างบน ...

    /// <summary>
    /// Logout — blacklist JTI ของ access token + ลบ refresh token จาก Redis
    ///
    /// ทำ 2 อย่าง:
    /// 1. Blacklist access token → ใช้ต่อไม่ได้ทันที (แม้ยังไม่หมดอายุ)
    /// 2. ลบ refresh token → ขอ token ใหม่ไม่ได้
    ///
    /// ⚠️ Blacklist TTL = เวลาที่เหลือของ access token (สูงสุด ~15 นาที)
    ///   เมื่อ token หมดอายุตามธรรมชาติ blacklist entry จะ auto-delete
    ///   ทำให้ Redis ไม่โตไม่จำกัด
    /// </summary>
    public async Task LogoutAsync(Guid userId, string accessToken)
    {
        // 1. Blacklist access token
        var tokenInfo = _jwtService.GetJtiFromToken(accessToken);
        if (tokenInfo is not null)
        {
            // คำนวณเวลาที่เหลือก่อน token หมดอายุ
            var remainingTtl = tokenInfo.Value.Expiry - DateTime.UtcNow;
            if (remainingTtl > TimeSpan.Zero)
            {
                // เพิ่ม JTI ลง blacklist — value "1" (แค่ต้อง exist)
                await _redisService.SetAsync(
                    $"blacklist:{tokenInfo.Value.Jti}",
                    "1",
                    remainingTtl    // TTL = เวลาที่เหลือ เช่น 12 นาที
                );
            }
        }

        // 2. ลบ refresh token → ขอ token ใหม่ไม่ได้
        await _redisService.DeleteAsync($"refresh:{userId}");
    }
}  // ← ปิด class
```

---

## 4.4 — สร้าง JwtBlacklistMiddleware

### ไฟล์ใหม่: `Banking.Api/Middleware/JwtBlacklistMiddleware.cs`

```csharp
using Banking.Application.Services;
using System.IdentityModel.Tokens.Jwt;
using System.Text.Json;

namespace Banking.Api.Middleware;

/// <summary>
/// Middleware ตรวจสอบ JWT Blacklist ทุก request ที่ authenticated
///
/// ทำงานหลัง UseAuthentication (มี User.Claims แล้ว)
/// ทำงานก่อน UseAuthorization (ยังไม่ตรวจ [Authorize])
///
/// Flow:
///   1. User authenticated? → ถ้าไม่ ข้ามไป
///   2. อ่าน JTI จาก claims
///   3. เช็ค Redis: EXISTS blacklist:{jti}
///   4. ถ้ามี → return 401 (token ถูก revoke)
///   5. ถ้าไม่มี → ผ่านไปตามปกติ
///
/// ⚠️ Design Pattern — Scoped Service ใน Singleton Middleware:
///   Middleware ถูกสร้างครั้งเดียว (Singleton)
///   แต่ IRedisService เป็น Scoped
///   ถ้า inject ผ่าน constructor → ได้ Captive Dependency (service ตัวเดิมทุก request)
///   วิธีถูก: inject ผ่าน parameter ของ InvokeAsync
///   → ASP.NET Core จะ resolve ใหม่ทุก request
/// </summary>
public class JwtBlacklistMiddleware
{
    private readonly RequestDelegate _next;

    // ★ รับแค่ RequestDelegate ใน constructor (ไม่ inject IRedisService ที่นี่!)
    public JwtBlacklistMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    // ★ IRedisService inject ผ่าน method parameter → resolve ใหม่ทุก request
    public async Task InvokeAsync(HttpContext context, IRedisService redisService)
    {
        // เช็คเฉพาะ authenticated requests
        if (context.User.Identity?.IsAuthenticated == true)
        {
            // อ่าน JTI จาก JWT claims
            var jti = context.User.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;

            if (jti is not null && await redisService.ExistsAsync($"blacklist:{jti}"))
            {
                // Token ถูก revoke → return 401 ทันที (ไม่เรียก controller)
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.ContentType = "application/json";

                var response = new
                {
                    success = false,
                    message = "Token has been revoked.",
                    statusCode = 401
                };

                await context.Response.WriteAsync(
                    JsonSerializer.Serialize(response, new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    }));
                return;  // ★ ไม่เรียก _next → request จบที่นี่
            }
        }

        await _next(context);  // ★ ผ่านไปตามปกติ
    }
}
```

> **เทียบกับ ExceptionMiddleware ที่มีอยู่:**
> ดู `ExceptionMiddleware.cs` — มีโครงสร้างเหมือนกัน:
> constructor รับ `RequestDelegate` + inject dependencies
> `InvokeAsync` เรียก `_next(context)` เพื่อส่งต่อ

---

## 4.5 — แก้ไข Logout Endpoint

### ไฟล์: `Banking.Api/Controllers/AuthController.cs`

**ของเดิม (บรรทัด 84-90):**
```csharp
    [HttpPost("logout")]
    [Authorize]
    public IActionResult Logout()
    {
        // TODO Phase 3: เพิ่ม JTI ลง Redis blacklist
        return Ok(new ApiResponse<object>(true, "Logged out successfully."));
    }
```

**แก้เป็น — ทำงานจริง:**
```csharp
    /// <summary>
    /// ออกจากระบบ — POST /api/auth/logout
    ///
    /// ทำ 2 อย่าง:
    /// 1. Blacklist access token ใน Redis → ใช้ต่อไม่ได้ทันที
    /// 2. ลบ refresh token จาก Redis → ขอ token ใหม่ไม่ได้
    /// </summary>
    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout()  // ★ เปลี่ยนเป็น async Task<IActionResult>
    {
        // ดึง userId จาก JWT claims (pattern เดียวกับ Profile endpoint)
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userIdClaim is null || !Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized(new ApiResponse<object>(false, "Invalid token."));

        // ดึง access token จาก Authorization header
        var authHeader = Request.Headers.Authorization.ToString();
        var accessToken = authHeader["Bearer ".Length..];

        await _authService.LogoutAsync(userId, accessToken);
        return Ok(new ApiResponse<object>(true, "Logged out successfully."));
    }
```

---

## 4.6 — Register Middleware ใน Pipeline

### ไฟล์: `Banking.Api/Program.cs`

**ของเดิม (บรรทัด 80-85):**
```csharp
// ===== Middleware Pipeline (ลำดับสำคัญ!) =====
app.UseMiddleware<Banking.Api.Middleware.ExceptionMiddleware>();
app.UseHttpsRedirection();
app.UseAuthentication();  // ตรวจ JWT token
app.UseAuthorization();   // ตรวจสิทธิ์ [Authorize]
app.MapControllers();
```

**แก้เป็น — เพิ่ม Blacklist middleware + Rate limiter:**
```csharp
// ===== Middleware Pipeline (ลำดับสำคัญ!) =====
app.UseMiddleware<Banking.Api.Middleware.ExceptionMiddleware>();
app.UseHttpsRedirection();
app.UseAuthentication();                                                    // ตรวจ JWT token
app.UseMiddleware<Banking.Api.Middleware.JwtBlacklistMiddleware>();          // ★ NEW: ตรวจ blacklist
app.UseAuthorization();                                                     // ตรวจสิทธิ์ [Authorize]
app.UseRateLimiter();                                                       // ★ NEW: rate limiting (Step 5)
app.MapControllers();
```

> **ลำดับสำคัญมาก!**
> 1. `UseAuthentication` → parse JWT, populate `context.User` (ต้องมาก่อน)
> 2. `JwtBlacklistMiddleware` → อ่าน JTI จาก `context.User.Claims` (ต้องมี User แล้ว)
> 3. `UseAuthorization` → ตรวจ `[Authorize]` (ต้องมาหลัง authentication)
> 4. `UseRateLimiter` → ตรวจ rate limit (ต้องมี User claims สำหรับ transaction policy)
>
> ถ้าสลับลำดับจะพัง เช่น:
> - Blacklist ก่อน Authentication → ไม่มี claims ให้อ่าน JTI
> - RateLimiter ก่อน Authorization → ไม่มี userId สำหรับ transaction policy

### ทดสอบ Step 4
```bash
# 1. Login → ได้ access token
# 2. GET /api/auth/profile → 200 OK
# 3. POST /api/auth/logout → 200 "Logged out successfully."
# 4. GET /api/auth/profile (ด้วย token เดิม) → 401 "Token has been revoked."
# 5. redis-cli EXISTS "blacklist:{jti}" → 1
# 6. รอ 15 นาที → redis-cli EXISTS "blacklist:{jti}" → 0 (auto-delete)
```

---

# Step 5: Rate Limiting

## เป้าหมาย
จำกัดจำนวน request ต่อช่วงเวลา ป้องกัน brute force และ API abuse

## ความรู้พื้นฐาน

**Rate Limiting คืออะไร?**
- จำกัด request ที่ client ส่งได้ในช่วงเวลาหนึ่ง
- เกินจำนวน → **HTTP 429 Too Many Requests**

**Fixed Window Algorithm:**
```
           Window 1 (1 min)          Window 2 (1 min)
          ┌────────────────┐        ┌────────────────┐
Requests: │ ■■■■■ (5/5)    │        │ ■■ (2/5)       │
          │ → 6th rejected │        │ → OK           │
          └────────────────┘        └────────────────┘
```

**ASP.NET Core Built-in Rate Limiting:**
- มีมาตั้งแต่ .NET 7 — **ไม่ต้องลง package เพิ่ม**
- Namespace: `Microsoft.AspNetCore.RateLimiting` + `System.Threading.RateLimiting`

**3 Policies:**

| Policy | ใช้กับ | Limit | Partition By | ทำไม |
|---|---|---|---|---|
| `fixed` | ทั่วไป | 100 req/min | IP | ป้องกัน abuse ทั่วไป |
| `auth` | Login | 5 req/min | IP | ป้องกัน brute force password |
| `transaction` | ฝาก/ถอน/โอน | 30 req/min | User ID | ป้องกัน transaction flood |

---

## 5.1 — เพิ่ม Rate Limiter Services

### ไฟล์: `Banking.Api/Program.cs`

**เพิ่มก่อน `// ===== ASP.NET Core =====`:**

```csharp
// ===== Rate Limiting =====
builder.Services.AddRateLimiter(options =>
{
    // กำหนด response เมื่อถูก reject
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, cancellationToken) =>
    {
        // ★ ใช้ format เดียวกับ ExceptionMiddleware เพื่อความ consistent
        context.HttpContext.Response.ContentType = "application/json";
        var response = new
        {
            success = false,
            message = "Too many requests. Please try again later.",
            statusCode = 429
        };
        await context.HttpContext.Response.WriteAsync(
            JsonSerializer.Serialize(response, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            }),
            cancellationToken);
    };

    // Policy 1: General — 100 req/min per IP
    options.AddFixedWindowLimiter("fixed", opt =>
    {
        opt.PermitLimit = 100;                    // สูงสุด 100 requests
        opt.Window = TimeSpan.FromMinutes(1);     // ต่อ 1 นาที
        opt.QueueLimit = 0;                       // ไม่ queue — reject ทันที
    });

    // Policy 2: Auth — 5 req/min per IP (ป้องกัน brute force password)
    options.AddFixedWindowLimiter("auth", opt =>
    {
        opt.PermitLimit = 5;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueLimit = 0;
    });

    // Policy 3: Transaction — 30 req/min per User
    //
    // ★ ใช้ AddPolicy แทน AddFixedWindowLimiter เพราะ:
    //   ต้อง custom partition key (ใช้ userId จาก JWT แทน IP)
    //
    // ★ Partition Key:
    //   Rate limit แยกนับตาม partition key
    //   - User A ส่ง 30 req → ถูก limit
    //   - User B ส่ง 30 req → ไม่เกี่ยวกับ A (คนละ counter)
    options.AddPolicy("transaction", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            // partition key: userId จาก JWT ถ้ามี, ไม่มีใช้ IP
            context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? context.Connection.RemoteIpAddress?.ToString()
                ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 30,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
});

// ===== ASP.NET Core =====
```

---

## 5.2 — Apply Attributes บน Controllers

### ไฟล์: `Banking.Api/Controllers/AuthController.cs`

**ของเดิม (บรรทัด 1-13):**
```csharp
using Banking.Application.DTOs;
using Banking.Application.Services;
using Banking.Application.DTOs;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Banking.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
```

**แก้เป็น — เพิ่ม using + attribute:**
```csharp
using Banking.Application.DTOs;
using Banking.Application.Services;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;   // ★ NEW
using System.Security.Claims;

namespace Banking.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[EnableRateLimiting("fixed")]              // ★ NEW — 100 req/min (class-level)
public class AuthController : ControllerBase
```

**เพิ่ม attribute บน Login method:**
```csharp
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]           // ★ NEW — 5 req/min (override class-level)
    public async Task<IActionResult> Login(...)
```

> **เรียนรู้ — Attribute Priority:**
> method-level `[EnableRateLimiting("auth")]` **override** class-level `[EnableRateLimiting("fixed")]`
> Login ใช้ "auth" (5/min) แม้ class ใช้ "fixed" (100/min)

---

### ไฟล์: `Banking.Api/Controllers/TransactionsController.cs`

**ของเดิม (บรรทัด 1-13):**
```csharp
using Banking.Application.DTOs;
using Banking.Application.Services;
using Banking.Domain.Exceptions;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using System.Runtime.InteropServices;
using static System.Net.Mime.MediaTypeNames;

namespace Banking.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TransactionsController : ControllerBase
```

**แก้เป็น — เพิ่ม using + attribute:**
```csharp
using Banking.Application.DTOs;
using Banking.Application.Services;
using Banking.Domain.Exceptions;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;   // ★ NEW
using System.Runtime.InteropServices;
using static System.Net.Mime.MediaTypeNames;

namespace Banking.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[EnableRateLimiting("transaction")]        // ★ NEW — 30 req/min per user
public class TransactionsController : ControllerBase
```

---

### ไฟล์: `Banking.Api/Controllers/AccountsController.cs`

**ของเดิม (บรรทัด 1-12):**
```csharp
using Banking.Application.DTOs;
using Banking.Application.Services;
using Banking.Domain.Enums;
using Banking.Domain.Exceptions;
using Banking.Domain.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Banking.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AccountsController : ControllerBase
```

**แก้เป็น — เพิ่ม using + attribute:**
```csharp
using Banking.Application.DTOs;
using Banking.Application.Services;
using Banking.Domain.Enums;
using Banking.Domain.Exceptions;
using Banking.Domain.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;   // ★ NEW

namespace Banking.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[EnableRateLimiting("fixed")]              // ★ NEW — 100 req/min
public class AccountsController : ControllerBase
```

### ทดสอบ Step 5
```bash
# 1. Login 5 ครั้งภายใน 1 นาที → ครั้งที่ 6 ต้องได้ 429
# 2. Deposit 30 ครั้งภายใน 1 นาที → ครั้งที่ 31 ต้องได้ 429
# 3. รอ 1 นาที → ลองใหม่ → ต้องผ่าน (200)
```

---

# Checklist ก่อนถือว่า Phase 3 เสร็จ

- [ ] Redis server รันได้ (`redis-cli ping` → PONG)
- [ ] `dotnet build` ผ่าน 0 errors
- [ ] Register → ตรวจ Redis มี `refresh:{userId}` (TTL ~7 วัน)
- [ ] Login → ตรวจ Redis update `refresh:{userId}`
- [ ] Refresh → ได้ token คู่ใหม่ + token เดิมใช้ refresh อีกไม่ได้
- [ ] Logout → GET /profile ด้วย token เดิม → 401 "Token has been revoked."
- [ ] Logout → refresh token ถูกลบจาก Redis
- [ ] Login 6 ครั้ง/นาที → ครั้งที่ 6 ได้ 429
- [ ] Blacklist entry หายเองหลัง TTL หมด (15 นาที)
