# Phase 3: Redis + Real-time (SignalR) — คู่มือทำทีละขั้นตอน

> Redis Cache + Distributed Lock + Rate Limiting + Token Blacklist + SignalR Real-time
> ทุกขั้นตอนอธิบายว่า "ทำไม" ต้องทำ + สร้างเพื่ออะไร

---

## สิ่งที่ต้องเสร็จก่อน (จาก Phase 2)

```
☑ Repository + UnitOfWork pattern ครบ
☑ TransactionService: Deposit, Withdraw, Transfer ทำงาน
☑ AuthService: Register, Login, JWT token
☑ FluentValidation validators ครบ
☑ ExceptionMiddleware ดักจับ error
☑ Controllers: Auth, Accounts, Transactions, Admin
☑ dotnet build ผ่าน 0 errors
☑ ทดสอบ API ผ่าน Swagger ได้
```

---

## ภาพรวม Phase 3 — Redis ใช้ทำอะไร?

```
ปัญหาถ้าไม่มี Redis:

1. Balance Query ช้า
   ทุกครั้งที่ดูยอดเงิน → query database
   10,000 users ดูพร้อมกัน → DB ล่ม!

2. Race Condition (ระหว่าง API Server หลายตัว)
   Phase 2 ใช้ DB row lock (FOR UPDATE)
   แต่ถ้ามี 3 API servers → ต้องมี distributed lock ด้วย

3. ไม่มี Rate Limiting
   Bot ส่ง request 1,000 ครั้ง/วินาที → ระบบล่ม

4. Logout ไม่จริง
   JWT ถูก sign แล้ว → ถึง logout แล้ว token ยังใช้ได้จนหมดอายุ!

5. ไม่ real-time
   ฝากเงินแล้วต้อง refresh หน้าถึงจะเห็นยอดใหม่

Redis แก้ทั้ง 5 ปัญหา:
┌──────────────────────────────────────────┐
│ 1. Balance Cache       → อ่านเร็ว < 1ms │
│ 2. Distributed Lock    → ป้องกัน race    │
│ 3. Rate Limiting       → จำกัด request   │
│ 4. Token Blacklist     → logout จริง     │
│ 5. Pub/Sub + SignalR   → real-time push  │
└──────────────────────────────────────────┘
```

---

## ขั้นตอนที่ 1: ติดตั้ง Redis + NuGet Packages

### 1.1 ติดตั้ง Redis (Local Development)

```
ทำไม: ต้องมี Redis server รันอยู่ เพื่อให้ app เชื่อมต่อได้

วิธีติดตั้ง (เลือกอย่างใดอย่างหนึ่ง):
```

**Option A: Docker (แนะนำ)**
```bash
# ติดตั้ง Redis ผ่าน Docker — ง่ายที่สุด
docker run -d --name banking-redis -p 6379:6379 redis:7-alpine

# ทดสอบว่า Redis ทำงาน
docker exec -it banking-redis redis-cli ping
# ผลลัพธ์: PONG
```

**Option B: Windows (Memurai — Redis-compatible)**
```bash
# ดาวน์โหลด Memurai จาก https://www.memurai.com/
# หรือใช้ winget
winget install Memurai.MemuraiDeveloper

# ทดสอบ
redis-cli ping
# ผลลัพธ์: PONG
```

### 1.2 เพิ่ม NuGet Packages

```bash
cd BankingSystem

# Redis client สำหรับ .NET (StackExchange.Redis)
dotnet add Banking.Infrastructure/Banking.Infrastructure.csproj package StackExchange.Redis

# SignalR สำหรับ real-time (มาพร้อม ASP.NET Core แต่ต้องเพิ่ม Redis backplane)
dotnet add Banking.Api/Banking.Api.csproj package Microsoft.AspNetCore.SignalR.StackExchangeRedis
```

```
ทำไมใช้ StackExchange.Redis:
- เป็น Redis client ที่ได้รับความนิยมมากที่สุดใน .NET
- Thread-safe, multiplexed connection (1 connection ใช้ได้หลาย thread)
- รองรับ async/await ครบ
- StackOverflow ใช้ตัวนี้ใน production

ทำไมต้อง Redis backplane สำหรับ SignalR:
- ถ้ามี API server หลายตัว → SignalR ส่งข้อความได้แค่ภายใน server เดียว
- Redis backplane ทำให้ทุก server เห็น message เดียวกัน (Pub/Sub)
```

### 1.3 เพิ่ม Connection String ใน appsettings.json

```json
// Banking.Api/appsettings.json — เพิ่ม Redis section

{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=banking_db;Username=postgres;Password=yourpassword",
    "Redis": "localhost:6379,abortConnect=false,connectTimeout=5000"
  },
  "Jwt": { ... },
  "Redis": {
    "InstanceName": "banking:",
    "BalanceCacheTtlMinutes": 5,
    "LockTimeoutSeconds": 10,
    "RateLimitWindowSeconds": 60,
    "RateLimitMaxRequests": 10,
    "TokenBlacklistTtlMinutes": 20
  }
}
```

```
abortConnect=false — ถ้าเชื่อมต่อไม่ได้ตอนเริ่มต้น → ไม่ crash
  จะลอง reconnect อัตโนมัติ

connectTimeout=5000 — รอเชื่อมต่อ max 5 วินาที

InstanceName = "banking:" — prefix สำหรับทุก key
  ป้องกัน key ชนกับ app อื่นที่ใช้ Redis เดียวกัน
  เช่น key "balance:123" จะถูกเก็บเป็น "banking:balance:123"
```

```json
// Banking.Api/appsettings.Development.json — สำหรับ Development

{
  "ConnectionStrings": {
    "Redis": "localhost:6379,abortConnect=false"
  }
}
```

---

## ขั้นตอนที่ 2: สร้าง Redis Service Interface + Implementation

### 2.1 สร้าง Interface (Application Layer)

```
📁 Banking.Application/Services/IRedisCacheService.cs

ทำไม: Business Logic ต้องรู้ว่ามี cache service แต่ไม่ต้องรู้ว่าใช้ Redis
ถ้าวันหนึ่งเปลี่ยนไปใช้ Memcached → แก้แค่ Implementation
```

```csharp
// Banking.Application/Services/IRedisCacheService.cs

namespace Banking.Application.Services;

/// <summary>
/// Redis Cache Service Interface — อยู่ใน Application layer
///
/// แยก interface ไว้ที่ Application เพื่อ:
/// 1. Business Logic (TransactionService) ใช้ได้โดยไม่ต้อง reference Infrastructure
/// 2. Test ได้ง่าย (Mock IRedisCacheService)
/// 3. เปลี่ยน cache provider ได้โดยไม่แก้ business logic
/// </summary>
public interface IRedisCacheService
{
    // ===== Generic Cache =====

    /// <summary>
    /// เก็บข้อมูลใน cache พร้อม TTL (Time To Live)
    /// หลัง TTL → key จะถูกลบอัตโนมัติ
    /// </summary>
    Task SetAsync<T>(string key, T value, TimeSpan? expiry = null);

    /// <summary>
    /// ดึงข้อมูลจาก cache — return null ถ้าไม่มี (cache miss)
    /// </summary>
    Task<T?> GetAsync<T>(string key);

    /// <summary>
    /// ลบข้อมูลออกจาก cache — ใช้ตอน invalidate
    /// </summary>
    Task RemoveAsync(string key);

    /// <summary>
    /// เช็คว่า key มีอยู่ไหม
    /// </summary>
    Task<bool> ExistsAsync(string key);

    // ===== Balance Cache =====

    /// <summary>
    /// เก็บ balance ใน cache — ใช้หลังทุก transaction
    /// </summary>
    Task SetBalanceCacheAsync(Guid accountId, decimal balance, decimal availableBalance);

    /// <summary>
    /// ดึง balance จาก cache — เร็วกว่า query DB
    /// return null = cache miss → ต้อง query DB
    /// </summary>
    Task<(decimal Balance, decimal AvailableBalance)?> GetBalanceCacheAsync(Guid accountId);

    /// <summary>
    /// ลบ balance cache — ใช้เมื่อ balance เปลี่ยน (invalidate)
    /// </summary>
    Task InvalidateBalanceCacheAsync(Guid accountId);

    // ===== Distributed Lock =====

    /// <summary>
    /// ล็อค resource — return true ถ้าล็อคสำเร็จ
    /// ใช้ป้องกัน race condition เมื่อมีหลาย API server
    ///
    /// lockValue: unique identifier สำหรับคนที่ถือ lock
    ///   → ป้องกันไม่ให้คนอื่นปลดล็อคของเรา
    /// </summary>
    Task<bool> AcquireLockAsync(string lockKey, string lockValue, TimeSpan expiry);

    /// <summary>
    /// ปลดล็อค — ต้องตรวจ lockValue ตรงกันก่อนปลด
    /// ป้องกัน: A ล็อค → A หมดเวลา → B ล็อค → A ปลดล็อคของ B!
    /// </summary>
    Task<bool> ReleaseLockAsync(string lockKey, string lockValue);

    // ===== Rate Limiting =====

    /// <summary>
    /// เช็ค + เพิ่ม counter สำหรับ rate limiting
    /// return true = ยังไม่เกิน limit (อนุญาต)
    /// return false = เกิน limit (ปฏิเสธ)
    /// </summary>
    Task<bool> CheckRateLimitAsync(string key, int maxRequests, TimeSpan window);

    // ===== Token Blacklist =====

    /// <summary>
    /// เพิ่ม JWT token ID (JTI) ลง blacklist — ใช้ตอน logout
    /// TTL = เวลาที่เหลือก่อน token หมดอายุ (ไม่ต้องเก็บตลอด)
    /// </summary>
    Task BlacklistTokenAsync(string jti, TimeSpan ttl);

    /// <summary>
    /// เช็คว่า token ถูก blacklist ไหม — ใช้ทุก request ที่ต้อง auth
    /// </summary>
    Task<bool> IsTokenBlacklistedAsync(string jti);

    // ===== Pub/Sub =====

    /// <summary>
    /// Publish message ไปยัง channel — ใช้แจ้งเตือน real-time
    /// </summary>
    Task PublishAsync(string channel, string message);
}
```

### 2.2 สร้าง Implementation (Infrastructure Layer)

```
📁 Banking.Infrastructure/Services/RedisCacheService.cs

ทำไม: Implementation จริงที่ใช้ StackExchange.Redis
อยู่ใน Infrastructure layer เพราะเป็น external dependency
```

```csharp
// Banking.Infrastructure/Services/RedisCacheService.cs

using Banking.Application.Services;
using Microsoft.Extensions.Configuration;
using StackExchange.Redis;
using System.Text.Json;

namespace Banking.Infrastructure.Services;

/// <summary>
/// Redis Cache Service Implementation
///
/// ใช้ IConnectionMultiplexer จาก StackExchange.Redis
/// → thread-safe, multiplexed (1 connection ใช้ได้ทุก thread)
/// → ไม่ต้องสร้าง connection ใหม่ทุก request (ใช้ Singleton)
/// </summary>
public class RedisCacheService : IRedisCacheService
{
    private readonly IDatabase _db;
    private readonly ISubscriber _subscriber;
    private readonly IConfiguration _config;
    private readonly string _instanceName;

    public RedisCacheService(
        IConnectionMultiplexer redis,
        IConfiguration config)
    {
        _db = redis.GetDatabase();
        _subscriber = redis.GetSubscriber();
        _config = config;
        _instanceName = config["Redis:InstanceName"] ?? "banking:";
    }

    /// <summary>
    /// สร้าง key พร้อม prefix — ป้องกัน key ชนกับ app อื่น
    /// เช่น "balance:123" → "banking:balance:123"
    /// </summary>
    private string GetKey(string key) => $"{_instanceName}{key}";

    // =====================================================
    // Generic Cache
    // =====================================================

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiry = null)
    {
        var json = JsonSerializer.Serialize(value);
        await _db.StringSetAsync(GetKey(key), json, expiry);
    }

    public async Task<T?> GetAsync<T>(string key)
    {
        var value = await _db.StringGetAsync(GetKey(key));
        if (value.IsNullOrEmpty) return default;
        return JsonSerializer.Deserialize<T>(value!);
    }

    public async Task RemoveAsync(string key)
    {
        await _db.KeyDeleteAsync(GetKey(key));
    }

    public async Task<bool> ExistsAsync(string key)
    {
        return await _db.KeyExistsAsync(GetKey(key));
    }

    // =====================================================
    // Balance Cache
    // =====================================================

    /// <summary>
    /// เก็บ balance ใน Redis — format: "balance:{accountId}"
    ///
    /// ใช้ Hash แทน String เพื่อเก็บหลาย field ใน key เดียว:
    ///   HSET balance:123 "balance" "50000" "available" "48000"
    ///
    /// ข้อดี Hash vs 2 String keys:
    /// - Atomic: อ่าน/เขียนทั้ง 2 fields พร้อมกัน
    /// - Memory: ประหยัดกว่า (Redis optimize small hashes)
    /// - Expiry: ตั้ง TTL ได้ที่ key level (ทั้ง hash หมดอายุพร้อมกัน)
    /// </summary>
    public async Task SetBalanceCacheAsync(
        Guid accountId, decimal balance, decimal availableBalance)
    {
        var key = GetKey($"balance:{accountId}");
        var ttl = TimeSpan.FromMinutes(
            _config.GetValue<int>("Redis:BalanceCacheTtlMinutes", 5));

        var entries = new HashEntry[]
        {
            new("balance", balance.ToString("F2")),
            new("available", availableBalance.ToString("F2"))
        };

        await _db.HashSetAsync(key, entries);
        await _db.KeyExpireAsync(key, ttl);
    }

    public async Task<(decimal Balance, decimal AvailableBalance)?> GetBalanceCacheAsync(
        Guid accountId)
    {
        var key = GetKey($"balance:{accountId}");
        var entries = await _db.HashGetAllAsync(key);

        if (entries.Length == 0) return null;  // cache miss

        var balance = decimal.Parse(
            entries.FirstOrDefault(e => e.Name == "balance").Value!);
        var available = decimal.Parse(
            entries.FirstOrDefault(e => e.Name == "available").Value!);

        return (balance, available);
    }

    public async Task InvalidateBalanceCacheAsync(Guid accountId)
    {
        await _db.KeyDeleteAsync(GetKey($"balance:{accountId}"));
    }

    // =====================================================
    // Distributed Lock
    // =====================================================

    /// <summary>
    /// ⚠️ CRITICAL: Distributed Lock ด้วย Redis
    ///
    /// ปัญหาที่แก้:
    ///   Phase 2 ใช้ DB row lock (FOR UPDATE) — ใช้ได้แค่ 1 DB server
    ///   ถ้ามี 3 API servers ต่อ 1 DB → row lock ยังทำงาน
    ///   แต่ถ้าต้อง coordinate ก่อนเข้า DB → ต้องใช้ distributed lock
    ///
    /// Redis SET NX (Set if Not eXists):
    ///   SET lock:account:123 "server1-uuid" NX EX 10
    ///   - NX: สร้างได้เมื่อ key ยังไม่มี (atomic check + set)
    ///   - EX 10: หมดอายุใน 10 วินาที (auto-release ถ้า server ตาย)
    ///
    /// Flow:
    ///   Server A: SET lock:account:123 "A" NX EX 10 → true (ได้ lock)
    ///   Server B: SET lock:account:123 "B" NX EX 10 → false (key มีอยู่แล้ว)
    ///   Server A: ทำ transaction → ปลด lock
    ///   Server B: retry → ได้ lock → ทำ transaction
    /// </summary>
    public async Task<bool> AcquireLockAsync(
        string lockKey, string lockValue, TimeSpan expiry)
    {
        return await _db.StringSetAsync(
            GetKey($"lock:{lockKey}"),
            lockValue,
            expiry,
            When.NotExists);  // NX: Set only if not exists
    }

    /// <summary>
    /// ปลด lock อย่างปลอดภัย — ใช้ Lua script เพื่อ atomic check + delete
    ///
    /// ทำไมต้อง Lua script:
    ///   ❌ แบบนี้อันตราย (ไม่ atomic):
    ///     GET lock:account:123 → "A"
    ///     (ช่วงนี้ lock หมดอายุ → B ได้ lock ใหม่)
    ///     DEL lock:account:123 → ลบ lock ของ B!
    ///
    ///   ✅ Lua script (atomic):
    ///     ถ้า value ตรง → ลบ (ทั้งหมดเกิดใน 1 operation)
    ///     ถ้า value ไม่ตรง → ไม่ทำอะไร
    /// </summary>
    public async Task<bool> ReleaseLockAsync(string lockKey, string lockValue)
    {
        var script = @"
            if redis.call('get', KEYS[1]) == ARGV[1] then
                return redis.call('del', KEYS[1])
            else
                return 0
            end";

        var result = await _db.ScriptEvaluateAsync(
            script,
            new RedisKey[] { GetKey($"lock:{lockKey}") },
            new RedisValue[] { lockValue });

        return (int)result == 1;
    }

    // =====================================================
    // Rate Limiting (Sliding Window Counter)
    // =====================================================

    /// <summary>
    /// Rate Limiting ด้วย Redis INCR + EXPIRE
    ///
    /// วิธีทำงาน (Fixed Window Counter):
    ///   Key: "ratelimit:{userId}:transfer" → counter
    ///   TTL: 60 seconds (window size)
    ///
    ///   Request 1: INCR → 1 (สร้าง key + EXPIRE 60s)
    ///   Request 2: INCR → 2
    ///   ...
    ///   Request 10: INCR → 10 → ยังไม่เกิน limit
    ///   Request 11: INCR → 11 → เกิน! → ปฏิเสธ
    ///   (60 วินาทีผ่านไป → key หมดอายุ → reset เป็น 0)
    ///
    /// ⚠️ Fixed Window มีจุดอ่อน (burst ตรงขอบ window):
    ///   วินาทีที่ 59: ส่ง 10 requests (ผ่าน)
    ///   วินาทีที่ 60: window reset
    ///   วินาทีที่ 61: ส่งอีก 10 requests (ผ่าน)
    ///   → 20 requests ใน 2 วินาที! แต่ยอมรับได้สำหรับ banking app
    /// </summary>
    public async Task<bool> CheckRateLimitAsync(
        string key, int maxRequests, TimeSpan window)
    {
        var redisKey = GetKey($"ratelimit:{key}");

        // INCR: เพิ่ม counter (ถ้า key ไม่มี → สร้างใหม่ value = 1)
        var count = await _db.StringIncrementAsync(redisKey);

        // ถ้าเป็น request แรก (count == 1) → ตั้ง TTL
        if (count == 1)
        {
            await _db.KeyExpireAsync(redisKey, window);
        }

        return count <= maxRequests;
    }

    // =====================================================
    // Token Blacklist
    // =====================================================

    /// <summary>
    /// Blacklist JWT token — ใช้ตอน logout
    ///
    /// ปัญหา JWT:
    ///   JWT เป็น stateless → server ไม่เก็บ state
    ///   ถ้า user logout → token ยังใช้ได้จนหมดอายุ (15 นาที)!
    ///
    /// วิธีแก้: เก็บ JTI (JWT ID) ไว้ใน Redis
    ///   Logout → เก็บ JTI ใน Redis พร้อม TTL = เวลาที่เหลือ
    ///   ทุก request → เช็คว่า JTI อยู่ใน blacklist ไหม
    ///
    /// TTL สำคัญ:
    ///   ไม่ต้องเก็บ JTI ตลอดไป — แค่เก็บจนกว่า token จะหมดอายุ
    ///   Access token อายุ 15 นาที → เก็บ JTI ใน Redis max 20 นาที
    ///   หลังจากนั้น token หมดอายุเอง → ลบ JTI ได้
    /// </summary>
    public async Task BlacklistTokenAsync(string jti, TimeSpan ttl)
    {
        await _db.StringSetAsync(
            GetKey($"blacklist:{jti}"),
            "revoked",
            ttl);
    }

    public async Task<bool> IsTokenBlacklistedAsync(string jti)
    {
        return await _db.KeyExistsAsync(GetKey($"blacklist:{jti}"));
    }

    // =====================================================
    // Pub/Sub
    // =====================================================

    /// <summary>
    /// Publish message ผ่าน Redis Pub/Sub
    ///
    /// ใช้คู่กับ SignalR:
    ///   ฝากเงินสำเร็จ → Publish "balance-updated" → SignalR ส่งให้ client
    ///
    /// ทำไมใช้ Redis Pub/Sub (ไม่ใช่ SignalR โดยตรง):
    ///   SignalR hub อยู่บน 1 server → ส่งได้แค่ client ที่ต่อ server นั้น
    ///   Redis Pub/Sub → broadcast ไปทุก server → ทุก client ได้รับ
    /// </summary>
    public async Task PublishAsync(string channel, string message)
    {
        await _subscriber.PublishAsync(
            RedisChannel.Literal(GetKey(channel)), message);
    }
}
```

---

## ขั้นตอนที่ 3: สร้าง SignalR Hub (Real-time Notifications)

### 3.1 สร้าง NotificationHub

```
📁 Banking.Api/Hubs/NotificationHub.cs

ทำไม: ส่งข้อมูล real-time ไปยัง client (browser)
ไม่ต้อง refresh หน้า — ยอดเงินอัปเดตทันที

SignalR คืออะไร:
- Library ของ Microsoft สำหรับ real-time communication
- ใช้ WebSocket (เร็วที่สุด) → fallback เป็น Server-Sent Events → Long Polling
- Client connect ครั้งเดียว → server push ข้อมูลได้ตลอด
```

```csharp
// Banking.Api/Hubs/NotificationHub.cs

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace Banking.Api.Hubs;

/// <summary>
/// SignalR Hub สำหรับ real-time notifications
///
/// Flow:
///   1. Client connect → เข้า group "user:{userId}"
///   2. มีคนฝากเงินเข้าบัญชี → server ส่ง "BalanceUpdated" ไปยัง group
///   3. Client ได้รับ event → อัปเดต UI ทันที
///
/// Hub vs Controller:
///   Controller: Client ส่ง request → Server ตอบ (request/response)
///   Hub: Server ส่งข้อมูลไปหา Client ได้เลย (push)
/// </summary>
[Authorize]
public class NotificationHub : Hub
{
    private readonly ILogger<NotificationHub> _logger;

    public NotificationHub(ILogger<NotificationHub> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// เมื่อ client connect — เพิ่มเข้า group ของ user
    ///
    /// Group = ห้องส่งข้อความ
    ///   user:123 → ทุก device ของ user 123 อยู่ใน group เดียวกัน
    ///   เปิด browser 3 tab → ทั้ง 3 ได้รับ notification
    ///
    /// Context.User ดึงข้อมูลจาก JWT token (ผ่าน [Authorize])
    /// Context.ConnectionId เป็น ID unique ของแต่ละ connection
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId is not null)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user:{userId}");
            _logger.LogInformation(
                "User {UserId} connected to NotificationHub (ConnectionId: {ConnectionId})",
                userId, Context.ConnectionId);
        }

        await base.OnConnectedAsync();
    }

    /// <summary>
    /// เมื่อ client disconnect — ลบออกจาก group
    /// SignalR จัดการ cleanup อัตโนมัติ แต่ log ไว้เพื่อ debug
    /// </summary>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId is not null)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"user:{userId}");
            _logger.LogInformation(
                "User {UserId} disconnected from NotificationHub", userId);
        }

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Client สมัครรับ notification สำหรับบัญชีเฉพาะ
    ///
    /// ใช้เมื่อ user มีหลายบัญชี — สมัครเฉพาะบัญชีที่กำลังดูอยู่
    ///   JoinAccountGroup("account:abc-123") → ได้รับ balance update ของบัญชีนั้น
    /// </summary>
    public async Task JoinAccountGroup(string accountId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"account:{accountId}");
    }

    public async Task LeaveAccountGroup(string accountId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"account:{accountId}");
    }
}
```

### 3.2 สร้าง Notification Service

```
📁 Banking.Application/Services/INotificationService.cs
📁 Banking.Infrastructure/Services/NotificationService.cs

ทำไม: แยก notification logic ออกจาก TransactionService
TransactionService แค่เรียก "ส่ง notification" — ไม่ต้องรู้ว่าใช้ SignalR หรือ Firebase
```

```csharp
// Banking.Application/Services/INotificationService.cs

namespace Banking.Application.Services;

/// <summary>
/// Notification Service Interface
/// ส่งข้อความ real-time ไปยัง client
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// แจ้ง user ว่า balance เปลี่ยน — ส่งผ่าน SignalR
    /// </summary>
    Task NotifyBalanceUpdatedAsync(
        Guid userId,
        Guid accountId,
        decimal newBalance,
        decimal newAvailableBalance);

    /// <summary>
    /// แจ้ง user ว่ามี transaction ใหม่
    /// </summary>
    Task NotifyTransactionAsync(
        Guid userId,
        string type,
        decimal amount,
        string referenceNumber);
}
```

```csharp
// Banking.Infrastructure/Services/NotificationService.cs

using Banking.Api.Hubs;
using Banking.Application.Services;
using Microsoft.AspNetCore.SignalR;

namespace Banking.Infrastructure.Services;

/// <summary>
/// Notification Service — ใช้ SignalR IHubContext ส่ง real-time messages
///
/// IHubContext<NotificationHub>:
///   ใช้ส่งข้อความจาก service/controller ไปยัง Hub
///   ไม่ต้องอยู่ใน Hub class ก็ส่งได้
///
/// ทำไมใช้ IHubContext แทนการเรียก Hub ตรง:
///   Hub instance ถูกสร้าง/ทำลายทุก connection
///   IHubContext เป็น singleton — ใช้ได้ตลอด
/// </summary>
public class NotificationService : INotificationService
{
    private readonly IHubContext<NotificationHub> _hubContext;

    public NotificationService(IHubContext<NotificationHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task NotifyBalanceUpdatedAsync(
        Guid userId, Guid accountId,
        decimal newBalance, decimal newAvailableBalance)
    {
        // ส่งไปยังทุก device ของ user
        await _hubContext.Clients
            .Group($"user:{userId}")
            .SendAsync("BalanceUpdated", new
            {
                AccountId = accountId,
                Balance = newBalance,
                AvailableBalance = newAvailableBalance,
                UpdatedAt = DateTime.UtcNow
            });

        // ส่งไปยัง client ที่ subscribe บัญชีนี้
        await _hubContext.Clients
            .Group($"account:{accountId}")
            .SendAsync("BalanceUpdated", new
            {
                AccountId = accountId,
                Balance = newBalance,
                AvailableBalance = newAvailableBalance,
                UpdatedAt = DateTime.UtcNow
            });
    }

    public async Task NotifyTransactionAsync(
        Guid userId, string type, decimal amount, string referenceNumber)
    {
        await _hubContext.Clients
            .Group($"user:{userId}")
            .SendAsync("TransactionCompleted", new
            {
                Type = type,
                Amount = amount,
                ReferenceNumber = referenceNumber,
                CreatedAt = DateTime.UtcNow
            });
    }
}
```

---

## ขั้นตอนที่ 4: อัปเดต TransactionService — เพิ่ม Redis

### 4.1 เพิ่ม Cache + Lock + Notification ใน TransactionService

```
📁 แก้ไข Banking.Application/Services/TransactionService.cs

ทำไม: เพิ่ม 3 features จาก Redis:
1. Distributed Lock → ล็อคบัญชีก่อนทำ transaction (เสริม DB row lock)
2. Cache Invalidation → ลบ balance cache เมื่อ balance เปลี่ยน
3. Real-time Notification → แจ้ง client เมื่อ transaction สำเร็จ
```

```csharp
// Banking.Application/Services/TransactionService.cs — Phase 3 (เพิ่ม Redis)

using Banking.Application.DTOs;
using Banking.Domain.Entities;
using Banking.Domain.Enums;
using Banking.Domain.Exceptions;
using Banking.Domain.Interfaces;

namespace Banking.Application.Services;

public class TransactionService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IRedisCacheService _cache;
    private readonly INotificationService _notification;

    public TransactionService(
        IUnitOfWork unitOfWork,
        IRedisCacheService cache,
        INotificationService notification)
    {
        _unitOfWork = unitOfWork;
        _cache = cache;
        _notification = notification;
    }

    // =====================================================
    // ฝากเงิน (DEPOSIT) — Phase 3: + Lock + Cache + Notify
    // =====================================================

    public async Task<TransactionResponse> DepositAsync(
        DepositRequest request, string? ipAddress = null, CancellationToken ct = default)
    {
        if (request.Amount <= 0)
            throw new ArgumentException("Amount must be greater than 0.");

        // === Distributed Lock ===
        var lockKey = $"account:{request.AccountId}";
        var lockValue = Guid.NewGuid().ToString();
        var lockExpiry = TimeSpan.FromSeconds(10);

        if (!await _cache.AcquireLockAsync(lockKey, lockValue, lockExpiry))
            throw new InvalidOperationException(
                "Account is being processed. Please try again.");

        try
        {
            await _unitOfWork.BeginTransactionAsync(ct);

            var account = await _unitOfWork.Accounts
                .GetByIdForUpdateAsync(request.AccountId, ct);

            if (account is null)
                throw new NotFoundException("Account", request.AccountId);

            if (account.Status != AccountStatus.Active)
                throw new AccountFrozenException(account.AccountNumber);

            var balanceBefore = account.Balance;
            account.Balance += request.Amount;
            account.AvailableBalance += request.Amount;
            _unitOfWork.Accounts.Update(account);

            var transaction = new Transaction
            {
                ReferenceNumber = ReferenceNumberGenerator.Generate(),
                AccountId = account.Id,
                Type = TransactionType.Deposit,
                Amount = request.Amount,
                BalanceBefore = balanceBefore,
                BalanceAfter = account.Balance,
                Status = TransactionStatus.Completed,
                Description = request.Description ?? "Deposit",
                IpAddress = ipAddress
            };

            await _unitOfWork.Transactions.AddAsync(transaction, ct);
            await _unitOfWork.SaveChangesAsync(ct);
            await _unitOfWork.CommitTransactionAsync(ct);

            // === Cache: อัปเดต balance cache ===
            await _cache.SetBalanceCacheAsync(
                account.Id, account.Balance, account.AvailableBalance);

            // === Notify: แจ้ง client real-time ===
            await _notification.NotifyBalanceUpdatedAsync(
                account.UserId, account.Id,
                account.Balance, account.AvailableBalance);

            await _notification.NotifyTransactionAsync(
                account.UserId,
                TransactionType.Deposit.ToString(),
                request.Amount,
                transaction.ReferenceNumber);

            return MapToResponse(transaction);
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(ct);
            throw;
        }
        finally
        {
            // === ปลด lock เสมอ (สำเร็จหรือไม่ก็ตาม) ===
            await _cache.ReleaseLockAsync(lockKey, lockValue);
        }
    }

    // =====================================================
    // ถอนเงิน (WITHDRAWAL) — Phase 3: + Lock + Cache + Notify
    // =====================================================

    public async Task<TransactionResponse> WithdrawAsync(
        WithdrawRequest request, string? ipAddress = null, CancellationToken ct = default)
    {
        if (request.Amount <= 0)
            throw new ArgumentException("Amount must be greater than 0.");

        var lockKey = $"account:{request.AccountId}";
        var lockValue = Guid.NewGuid().ToString();

        if (!await _cache.AcquireLockAsync(lockKey, lockValue, TimeSpan.FromSeconds(10)))
            throw new InvalidOperationException(
                "Account is being processed. Please try again.");

        try
        {
            await _unitOfWork.BeginTransactionAsync(ct);

            var account = await _unitOfWork.Accounts
                .GetByIdForUpdateAsync(request.AccountId, ct);

            if (account is null)
                throw new NotFoundException("Account", request.AccountId);

            if (account.Status != AccountStatus.Active)
                throw new AccountFrozenException(account.AccountNumber);

            if (account.Balance < request.Amount)
                throw new InsufficientFundsException(account.Balance, request.Amount);

            var todayTotal = await _unitOfWork.Transactions
                .GetTodayWithdrawalTotalAsync(account.Id, ct);

            if (todayTotal + request.Amount > account.DailyWithdrawalLimit)
                throw new DailyLimitExceededException(
                    account.DailyWithdrawalLimit, todayTotal, request.Amount);

            var balanceBefore = account.Balance;
            account.Balance -= request.Amount;
            account.AvailableBalance -= request.Amount;
            _unitOfWork.Accounts.Update(account);

            var transaction = new Transaction
            {
                ReferenceNumber = ReferenceNumberGenerator.Generate(),
                AccountId = account.Id,
                Type = TransactionType.Withdrawal,
                Amount = request.Amount,
                BalanceBefore = balanceBefore,
                BalanceAfter = account.Balance,
                Status = TransactionStatus.Completed,
                Description = request.Description ?? "Withdrawal",
                IpAddress = ipAddress
            };

            await _unitOfWork.Transactions.AddAsync(transaction, ct);
            await _unitOfWork.SaveChangesAsync(ct);
            await _unitOfWork.CommitTransactionAsync(ct);

            await _cache.SetBalanceCacheAsync(
                account.Id, account.Balance, account.AvailableBalance);

            await _notification.NotifyBalanceUpdatedAsync(
                account.UserId, account.Id,
                account.Balance, account.AvailableBalance);

            await _notification.NotifyTransactionAsync(
                account.UserId,
                TransactionType.Withdrawal.ToString(),
                request.Amount,
                transaction.ReferenceNumber);

            return MapToResponse(transaction);
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(ct);
            throw;
        }
        finally
        {
            await _cache.ReleaseLockAsync(lockKey, lockValue);
        }
    }

    // =====================================================
    // โอนเงิน (TRANSFER) — Phase 3: + Lock ทั้ง 2 บัญชี
    // =====================================================

    /// <summary>
    /// โอนเงิน — lock ทั้ง 2 บัญชีตามลำดับ
    ///
    /// ⚠️ Deadlock Prevention:
    ///   A โอนไป B: lock A → lock B
    ///   B โอนไป A: lock B → lock A
    ///   → Deadlock! (A รอ B, B รอ A)
    ///
    ///   วิธีแก้: lock ตามลำดับ ID (เรียงจากน้อยไปมาก)
    ///   A < B → เสมอ lock A ก่อน B
    ///   ทั้ง 2 กรณี: lock A → lock B (ไม่มี deadlock)
    /// </summary>
    public async Task<TransactionResponse> TransferAsync(
        TransferRequest request, string? ipAddress = null, CancellationToken ct = default)
    {
        if (request.Amount <= 0)
            throw new ArgumentException("Amount must be greater than 0.");

        if (request.FromAccountId == request.ToAccountId)
            throw new ArgumentException("Cannot transfer to the same account.");

        // Lock ตามลำดับ ID → ป้องกัน deadlock
        var firstId = request.FromAccountId < request.ToAccountId
            ? request.FromAccountId : request.ToAccountId;
        var secondId = request.FromAccountId < request.ToAccountId
            ? request.ToAccountId : request.FromAccountId;

        var lockValue1 = Guid.NewGuid().ToString();
        var lockValue2 = Guid.NewGuid().ToString();
        var lockExpiry = TimeSpan.FromSeconds(10);

        if (!await _cache.AcquireLockAsync($"account:{firstId}", lockValue1, lockExpiry))
            throw new InvalidOperationException(
                "Account is being processed. Please try again.");

        if (!await _cache.AcquireLockAsync($"account:{secondId}", lockValue2, lockExpiry))
        {
            await _cache.ReleaseLockAsync($"account:{firstId}", lockValue1);
            throw new InvalidOperationException(
                "Account is being processed. Please try again.");
        }

        try
        {
            await _unitOfWork.BeginTransactionAsync(ct);

            var fromAccount = await _unitOfWork.Accounts
                .GetByIdForUpdateAsync(request.FromAccountId, ct);
            var toAccount = await _unitOfWork.Accounts
                .GetByIdForUpdateAsync(request.ToAccountId, ct);

            if (fromAccount is null)
                throw new NotFoundException("Source Account", request.FromAccountId);
            if (toAccount is null)
                throw new NotFoundException("Destination Account", request.ToAccountId);

            if (fromAccount.Status != AccountStatus.Active)
                throw new AccountFrozenException(fromAccount.AccountNumber);
            if (toAccount.Status != AccountStatus.Active)
                throw new AccountFrozenException(toAccount.AccountNumber);

            if (fromAccount.Balance < request.Amount)
                throw new InsufficientFundsException(fromAccount.Balance, request.Amount);

            var todayTotal = await _unitOfWork.Transactions
                .GetTodayWithdrawalTotalAsync(fromAccount.Id, ct);

            if (todayTotal + request.Amount > fromAccount.DailyWithdrawalLimit)
                throw new DailyLimitExceededException(
                    fromAccount.DailyWithdrawalLimit, todayTotal, request.Amount);

            var fromBefore = fromAccount.Balance;
            var toBefore = toAccount.Balance;

            fromAccount.Balance -= request.Amount;
            fromAccount.AvailableBalance -= request.Amount;
            toAccount.Balance += request.Amount;
            toAccount.AvailableBalance += request.Amount;

            _unitOfWork.Accounts.Update(fromAccount);
            _unitOfWork.Accounts.Update(toAccount);

            var debitTxn = new Transaction
            {
                ReferenceNumber = ReferenceNumberGenerator.Generate(),
                AccountId = fromAccount.Id,
                Type = TransactionType.TransferOut,
                Amount = request.Amount,
                BalanceBefore = fromBefore,
                BalanceAfter = fromAccount.Balance,
                Status = TransactionStatus.Completed,
                Description = request.Description
                    ?? $"Transfer to {toAccount.AccountNumber}",
                IpAddress = ipAddress
            };

            var creditTxn = new Transaction
            {
                ReferenceNumber = ReferenceNumberGenerator.Generate(),
                AccountId = toAccount.Id,
                Type = TransactionType.TransferIn,
                Amount = request.Amount,
                BalanceBefore = toBefore,
                BalanceAfter = toAccount.Balance,
                Status = TransactionStatus.Completed,
                Description = request.Description
                    ?? $"Transfer from {fromAccount.AccountNumber}",
                IpAddress = ipAddress
            };

            debitTxn.RelatedTransactionId = creditTxn.Id;
            creditTxn.RelatedTransactionId = debitTxn.Id;

            await _unitOfWork.Transactions.AddAsync(debitTxn, ct);
            await _unitOfWork.Transactions.AddAsync(creditTxn, ct);

            var transfer = new Transfer
            {
                FromAccountId = fromAccount.Id,
                ToAccountId = toAccount.Id,
                Amount = request.Amount,
                Fee = 0,
                Status = TransactionStatus.Completed,
                DebitTransactionId = debitTxn.Id,
                CreditTransactionId = creditTxn.Id
            };

            await _unitOfWork.SaveChangesAsync(ct);
            await _unitOfWork.CommitTransactionAsync(ct);

            // Cache: อัปเดตทั้ง 2 บัญชี
            await _cache.SetBalanceCacheAsync(
                fromAccount.Id, fromAccount.Balance, fromAccount.AvailableBalance);
            await _cache.SetBalanceCacheAsync(
                toAccount.Id, toAccount.Balance, toAccount.AvailableBalance);

            // Notify: แจ้งทั้ง 2 users
            await _notification.NotifyBalanceUpdatedAsync(
                fromAccount.UserId, fromAccount.Id,
                fromAccount.Balance, fromAccount.AvailableBalance);
            await _notification.NotifyBalanceUpdatedAsync(
                toAccount.UserId, toAccount.Id,
                toAccount.Balance, toAccount.AvailableBalance);

            await _notification.NotifyTransactionAsync(
                fromAccount.UserId,
                TransactionType.TransferOut.ToString(),
                request.Amount, debitTxn.ReferenceNumber);
            await _notification.NotifyTransactionAsync(
                toAccount.UserId,
                TransactionType.TransferIn.ToString(),
                request.Amount, creditTxn.ReferenceNumber);

            return MapToResponse(debitTxn);
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(ct);
            throw;
        }
        finally
        {
            await _cache.ReleaseLockAsync($"account:{secondId}", lockValue2);
            await _cache.ReleaseLockAsync($"account:{firstId}", lockValue1);
        }
    }

    // =====================================================
    // ดูประวัติธุรกรรม
    // =====================================================

    public async Task<PagedResponse<TransactionResponse>> GetHistoryAsync(
        Guid accountId, int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        var transactions = await _unitOfWork.Transactions
            .GetByAccountIdAsync(accountId, page, pageSize, ct);

        var totalCount = await _unitOfWork.Transactions
            .GetCountByAccountIdAsync(accountId, ct);

        var items = transactions.Select(MapToResponse).ToList();

        return new PagedResponse<TransactionResponse>(
            Items: items,
            TotalCount: totalCount,
            Page: page,
            PageSize: pageSize,
            TotalPages: (int)Math.Ceiling(totalCount / (double)pageSize)
        );
    }

    // =====================================================
    // Helper: Entity → DTO
    // =====================================================

    private static TransactionResponse MapToResponse(Transaction t) => new(
        Id: t.Id,
        ReferenceNumber: t.ReferenceNumber,
        Type: t.Type.ToString(),
        Amount: t.Amount,
        BalanceBefore: t.BalanceBefore,
        BalanceAfter: t.BalanceAfter,
        Status: t.Status.ToString(),
        Description: t.Description,
        CreatedAt: t.CreatedAt
    );
}
```

---

## ขั้นตอนที่ 5: อัปเดต AccountsController — ใช้ Balance Cache

```
📁 แก้ไข Banking.Api/Controllers/AccountsController.cs

ทำไม: endpoint ดูยอดเงินควรอ่านจาก Redis ก่อน (< 1ms)
ถ้า cache miss → อ่านจาก DB แล้วเก็บ cache
```

```csharp
// Banking.Api/Controllers/AccountsController.cs — Phase 3 (เพิ่ม Cache)

using Banking.Application.DTOs;
using Banking.Application.Services;
using Banking.Domain.Enums;
using Banking.Domain.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Banking.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AccountsController : ControllerBase
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IRedisCacheService _cache;

    public AccountsController(IUnitOfWork unitOfWork, IRedisCacheService cache)
    {
        _unitOfWork = unitOfWork;
        _cache = cache;
    }

    [HttpGet]
    public async Task<IActionResult> GetByUserId(
        [FromQuery] Guid userId, CancellationToken ct)
    {
        var accounts = await _unitOfWork.Accounts.GetByUserIdAsync(userId, ct);

        var response = accounts.Select(a => new AccountResponse(
            Id: a.Id,
            AccountNumber: a.AccountNumber,
            Type: a.Type.ToString(),
            Currency: a.Currency,
            Balance: a.Balance,
            AvailableBalance: a.AvailableBalance,
            Status: a.Status.ToString(),
            CreatedAt: a.CreatedAt
        )).ToList();

        return Ok(new ApiResponse<List<AccountResponse>>(
            true, "Accounts retrieved.", response));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var account = await _unitOfWork.Accounts.GetByIdAsync(id, ct);
        if (account is null)
            return NotFound(new ApiResponse<object>(false, "Account not found."));

        var response = new AccountResponse(
            Id: account.Id,
            AccountNumber: account.AccountNumber,
            Type: account.Type.ToString(),
            Currency: account.Currency,
            Balance: account.Balance,
            AvailableBalance: account.AvailableBalance,
            Status: account.Status.ToString(),
            CreatedAt: account.CreatedAt
        );

        return Ok(new ApiResponse<AccountResponse>(true, "Account retrieved.", response));
    }

    /// <summary>
    /// ดูยอดเงิน — ใช้ Redis Cache
    ///
    /// Cache Strategy: Cache-Aside (Lazy Loading)
    ///   1. ดูใน cache ก่อน (เร็ว < 1ms)
    ///   2. ถ้ามี (cache hit) → ส่งกลับเลย
    ///   3. ถ้าไม่มี (cache miss) → query DB → เก็บ cache → ส่งกลับ
    ///
    /// ทำไมไม่ cache ทุก endpoint:
    ///   Balance เปลี่ยนบ่อย → cache + invalidation คุ้มค่า
    ///   Account details ไม่ค่อยเปลี่ยน → ไม่จำเป็นต้อง cache
    /// </summary>
    [HttpGet("{id:guid}/balance")]
    public async Task<IActionResult> GetBalance(Guid id, CancellationToken ct)
    {
        // 1. ดูใน cache ก่อน
        var cached = await _cache.GetBalanceCacheAsync(id);
        if (cached.HasValue)
        {
            return Ok(new ApiResponse<object>(true, "Balance retrieved (cached).", new
            {
                Balance = cached.Value.Balance,
                AvailableBalance = cached.Value.AvailableBalance,
                Currency = "THB",
                Source = "cache"
            }));
        }

        // 2. Cache miss → query DB
        var account = await _unitOfWork.Accounts.GetByIdAsync(id, ct);
        if (account is null)
            return NotFound(new ApiResponse<object>(false, "Account not found."));

        // 3. เก็บ cache สำหรับครั้งหน้า
        await _cache.SetBalanceCacheAsync(
            account.Id, account.Balance, account.AvailableBalance);

        return Ok(new ApiResponse<object>(true, "Balance retrieved.", new
        {
            account.Balance,
            account.AvailableBalance,
            account.Currency,
            Source = "database"
        }));
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateAccountRequest request, CancellationToken ct)
    {
        string accountNumber;
        do
        {
            accountNumber = AccountNumberGenerator.Generate();
        } while (await _unitOfWork.Accounts.AccountNumberExistsAsync(accountNumber, ct));

        var account = new Domain.Entities.Account
        {
            UserId = request.UserId,
            AccountNumber = accountNumber,
            Type = Enum.Parse<AccountType>(request.Type),
            Currency = request.Currency ?? "THB",
            Status = AccountStatus.Active
        };

        await _unitOfWork.Accounts.AddAsync(account, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Created($"/api/accounts/{account.Id}",
            new ApiResponse<AccountResponse>(true, "Account created.", new AccountResponse(
                account.Id, account.AccountNumber, account.Type.ToString(),
                account.Currency, account.Balance, account.AvailableBalance,
                account.Status.ToString(), account.CreatedAt
            )));
    }
}

public record CreateAccountRequest(
    Guid UserId,
    string Type = "Savings",
    string? Currency = "THB"
);
```

---

## ขั้นตอนที่ 6: Rate Limiting Middleware

```
📁 Banking.Api/Middleware/RateLimitMiddleware.cs

ทำไม: จำกัดจำนวน request ต่อ user ต่อ endpoint
ป้องกัน:
- Bot attack (ส่ง request 1,000 ครั้ง/วินาที)
- Brute force login (ลอง password ซ้ำๆ)
- DoS จาก user เดียว (ใช้ resources มากเกินไป)
```

```csharp
// Banking.Api/Middleware/RateLimitMiddleware.cs

using Banking.Application.Services;
using System.Net;
using System.Security.Claims;
using System.Text.Json;

namespace Banking.Api.Middleware;

/// <summary>
/// Rate Limit Middleware — จำกัด request ต่อ user ต่อ endpoint
///
/// Flow:
///   1. ดึง userId จาก JWT (ถ้าไม่มี → ใช้ IP address)
///   2. สร้าง key: "ratelimit:{userId}:{endpoint}"
///   3. เช็ค Redis: ยังไม่เกิน limit ไหม?
///   4. ถ้าเกิน → 429 Too Many Requests
///   5. ถ้าไม่เกิน → ส่ง request ต่อ
///
/// ตำแหน่งใน Pipeline:
///   ExceptionMiddleware → RateLimitMiddleware → Authentication → Authorization → Controller
///   ต้องอยู่หลัง Authentication เพื่อให้ดึง userId จาก JWT ได้
/// </summary>
public class RateLimitMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IConfiguration _config;

    public RateLimitMiddleware(RequestDelegate next, IConfiguration config)
    {
        _next = next;
        _config = config;
    }

    public async Task InvokeAsync(HttpContext context, IRedisCacheService cache)
    {
        // ข้าม rate limit สำหรับ Swagger, health check
        var path = context.Request.Path.Value?.ToLower() ?? "";
        if (path.Contains("swagger") || path.Contains("health"))
        {
            await _next(context);
            return;
        }

        // ดึง identifier: userId (ถ้า login แล้ว) หรือ IP address
        var userId = context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var identifier = userId ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        // สร้าง key จาก identifier + endpoint
        var endpoint = context.Request.Path.Value?.Replace("/", "-") ?? "unknown";
        var rateLimitKey = $"{identifier}:{endpoint}";

        var maxRequests = _config.GetValue<int>("Redis:RateLimitMaxRequests", 10);
        var windowSeconds = _config.GetValue<int>("Redis:RateLimitWindowSeconds", 60);

        var allowed = await cache.CheckRateLimitAsync(
            rateLimitKey, maxRequests, TimeSpan.FromSeconds(windowSeconds));

        if (!allowed)
        {
            context.Response.StatusCode = (int)HttpStatusCode.TooManyRequests;
            context.Response.ContentType = "application/json";

            var response = new
            {
                success = false,
                message = $"Rate limit exceeded. Maximum {maxRequests} requests per {windowSeconds} seconds.",
                statusCode = 429
            };

            await context.Response.WriteAsync(
                JsonSerializer.Serialize(response, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                }));
            return;
        }

        await _next(context);
    }
}
```

---

## ขั้นตอนที่ 7: Token Blacklist Middleware

```
📁 Banking.Api/Middleware/TokenBlacklistMiddleware.cs

ทำไม: เช็คทุก request ว่า JWT token ถูก blacklist (logout) หรือยัง
ต้องเช็คก่อนถึง Controller
```

```csharp
// Banking.Api/Middleware/TokenBlacklistMiddleware.cs

using Banking.Application.Services;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Text.Json;

namespace Banking.Api.Middleware;

/// <summary>
/// Token Blacklist Middleware — เช็คว่า JWT ถูก revoke ไหม
///
/// Flow:
///   1. ดึง JWT จาก Authorization header
///   2. อ่าน JTI (JWT ID) จาก token
///   3. เช็ค Redis: JTI อยู่ใน blacklist ไหม?
///   4. ถ้าอยู่ → 401 Unauthorized (token ถูก revoke)
///   5. ถ้าไม่อยู่ → ส่ง request ต่อ
///
/// Performance:
///   Redis GET = < 1ms → แทบไม่กระทบ performance
///   เช็คทุก request ไม่หนัก เพราะ Redis เร็วมาก
/// </summary>
public class TokenBlacklistMiddleware
{
    private readonly RequestDelegate _next;

    public TokenBlacklistMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IRedisCacheService cache)
    {
        var authHeader = context.Request.Headers.Authorization.FirstOrDefault();

        if (authHeader is not null && authHeader.StartsWith("Bearer "))
        {
            var token = authHeader["Bearer ".Length..];

            try
            {
                var handler = new JwtSecurityTokenHandler();
                var jwtToken = handler.ReadJwtToken(token);
                var jti = jwtToken.Id;  // JWT ID claim

                if (!string.IsNullOrEmpty(jti) && await cache.IsTokenBlacklistedAsync(jti))
                {
                    context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                    context.Response.ContentType = "application/json";

                    var response = new
                    {
                        success = false,
                        message = "Token has been revoked. Please login again.",
                        statusCode = 401
                    };

                    await context.Response.WriteAsync(
                        JsonSerializer.Serialize(response, new JsonSerializerOptions
                        {
                            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                        }));
                    return;
                }
            }
            catch
            {
                // token อ่านไม่ได้ → ปล่อยผ่าน ให้ JWT middleware จัดการ
            }
        }

        await _next(context);
    }
}
```

---

## ขั้นตอนที่ 8: อัปเดต AuthController — Logout จริง

```
📁 แก้ไข Banking.Api/Controllers/AuthController.cs

ทำไม: Logout ต้อง blacklist token ใน Redis
ไม่ใช่แค่ return OK แล้วจบ
```

```csharp
// Banking.Api/Controllers/AuthController.cs — Phase 3 (เพิ่ม Redis logout)

using Banking.Application.DTOs;
using Banking.Application.Services;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Banking.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AuthService _authService;
    private readonly IValidator<RegisterRequest> _registerValidator;
    private readonly IValidator<LoginRequest> _loginValidator;
    private readonly IRedisCacheService _cache;

    public AuthController(
        AuthService authService,
        IValidator<RegisterRequest> registerValidator,
        IValidator<LoginRequest> loginValidator,
        IRedisCacheService cache)
    {
        _authService = authService;
        _registerValidator = registerValidator;
        _loginValidator = loginValidator;
        _cache = cache;
    }

    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register(
        [FromBody] RegisterRequest request, CancellationToken ct)
    {
        var validation = await _registerValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return BadRequest(new ApiResponse<object>(false,
                string.Join("; ", validation.Errors.Select(e => e.ErrorMessage))));

        var result = await _authService.RegisterAsync(request, ct);
        return Created($"/api/auth/profile",
            new ApiResponse<AuthResponse>(true, "Registration successful.", result));
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login(
        [FromBody] LoginRequest request, CancellationToken ct)
    {
        var validation = await _loginValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return BadRequest(new ApiResponse<object>(false,
                string.Join("; ", validation.Errors.Select(e => e.ErrorMessage))));

        var result = await _authService.LoginAsync(request, ct);
        return Ok(new ApiResponse<AuthResponse>(true, "Login successful.", result));
    }

    [HttpGet("profile")]
    [Authorize]
    public async Task<IActionResult> Profile(CancellationToken ct)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userIdClaim is null || !Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized(new ApiResponse<object>(false, "Invalid token."));

        var result = await _authService.GetProfileAsync(userId, ct);
        return Ok(new ApiResponse<UserProfileResponse>(true, "Profile retrieved.", result));
    }

    /// <summary>
    /// Logout — Blacklist token ใน Redis
    ///
    /// Flow:
    /// 1. ดึง JWT จาก Authorization header
    /// 2. อ่าน JTI + expiration จาก token
    /// 3. เก็บ JTI ลง Redis พร้อม TTL = เวลาที่เหลือก่อน token หมดอายุ
    /// 4. ทุก request หลังจากนี้จะถูก reject โดย TokenBlacklistMiddleware
    /// </summary>
    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        var authHeader = Request.Headers.Authorization.FirstOrDefault();
        if (authHeader is not null && authHeader.StartsWith("Bearer "))
        {
            var token = authHeader["Bearer ".Length..];
            var handler = new JwtSecurityTokenHandler();
            var jwtToken = handler.ReadJwtToken(token);

            var jti = jwtToken.Id;
            var expiry = jwtToken.ValidTo;
            var ttl = expiry - DateTime.UtcNow;

            if (!string.IsNullOrEmpty(jti) && ttl > TimeSpan.Zero)
            {
                await _cache.BlacklistTokenAsync(jti, ttl);
            }
        }

        return Ok(new ApiResponse<object>(true, "Logged out successfully."));
    }
}
```

---

## ขั้นตอนที่ 9: Program.cs ฉบับสมบูรณ์ Phase 3

```
📁 Banking.Api/Program.cs — รวมทุก Phase (1-3)
```

```csharp
// Banking.Api/Program.cs — Phase 3 สมบูรณ์

using Banking.Api.Hubs;
using Banking.Api.Middleware;
using Banking.Application.Services;
using Banking.Domain.Interfaces;
using Banking.Infrastructure.Data;
using Banking.Infrastructure.Repositories;
using Banking.Infrastructure.Services;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using StackExchange.Redis;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// ===== Database =====
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        npgsqlOptions =>
        {
            npgsqlOptions.MigrationsAssembly("Banking.Infrastructure");
            npgsqlOptions.EnableRetryOnFailure(3);
            npgsqlOptions.CommandTimeout(30);
        }));

// ===== Redis =====
// Singleton: 1 connection ใช้ทั้ง app (thread-safe, multiplexed)
builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    var connectionString = builder.Configuration.GetConnectionString("Redis")
        ?? "localhost:6379,abortConnect=false";
    return ConnectionMultiplexer.Connect(connectionString);
});
builder.Services.AddSingleton<IRedisCacheService, RedisCacheService>();

// ===== JWT Authentication =====
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!)),
            ClockSkew = TimeSpan.Zero
        };

        // ===== SignalR: ส่ง JWT ผ่าน query string (WebSocket ไม่มี header) =====
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;

                // ถ้า request เป็น SignalR hub → ดึง token จาก query string
                if (!string.IsNullOrEmpty(accessToken)
                    && path.StartsWithSegments("/hubs"))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            }
        };
    });

// ===== Repositories + UnitOfWork =====
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

// ===== Application Services =====
builder.Services.AddScoped<TransactionService>();
builder.Services.AddScoped<IJwtService, JwtService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<INotificationService, NotificationService>();

// ===== FluentValidation =====
builder.Services.AddValidatorsFromAssemblyContaining<Banking.Application.Validators.DepositRequestValidator>();

// ===== SignalR =====
builder.Services.AddSignalR()
    .AddStackExchangeRedis(builder.Configuration.GetConnectionString("Redis")
        ?? "localhost:6379", options =>
    {
        options.Configuration.ChannelPrefix = RedisChannel.Literal("banking-signalr:");
    });

// ===== ASP.NET Core =====
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ===== CORS (สำหรับ Next.js frontend) =====
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(
                builder.Configuration["Frontend:Url"] ?? "http://localhost:3000")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();  // จำเป็นสำหรับ SignalR
    });
});

var app = builder.Build();

// ===== Auto Migration & Seed (Debug/Dev only) =====
var env = app.Environment;
if (env.EnvironmentName == "Debug" || env.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await context.Database.MigrateAsync();
    await Banking.Infrastructure.Seeds.DataSeeder.SeedAsync(context);
}

// ===== Swagger =====
var swaggerEnabled = app.Configuration.GetValue<bool>("Swagger:Enabled");
if (swaggerEnabled)
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// ===== Middleware Pipeline (ลำดับสำคัญมาก!) =====
app.UseMiddleware<ExceptionMiddleware>();       // 1. จับ error ทั้งหมด
app.UseHttpsRedirection();                      // 2. HTTP → HTTPS
app.UseCors("AllowFrontend");                   // 3. CORS (ก่อน auth)
app.UseAuthentication();                        // 4. ตรวจ JWT → "คุณเป็นใคร?"
app.UseMiddleware<TokenBlacklistMiddleware>();   // 5. เช็ค token blacklist
app.UseMiddleware<RateLimitMiddleware>();        // 6. จำกัด request rate
app.UseAuthorization();                         // 7. ตรวจสิทธิ์ [Authorize]
app.MapControllers();                           // 8. Route → Controller

// ===== SignalR Hub =====
app.MapHub<NotificationHub>("/hubs/notifications");

app.Run();
```

```
⚠️ ลำดับ Middleware Phase 3:

1. ExceptionMiddleware      — จับ error ก่อนทุกอย่าง
2. HttpsRedirection         — บังคับ HTTPS
3. CORS                     — อนุญาต cross-origin (ก่อน auth เพื่อให้ preflight ผ่าน)
4. Authentication           — decode JWT → ได้ userId
5. TokenBlacklistMiddleware — เช็คว่า token ถูก revoke ไหม (ต้องอยู่หลัง auth)
6. RateLimitMiddleware      — จำกัด request (ต้องอยู่หลัง auth เพื่อดึง userId)
7. Authorization            — ตรวจสิทธิ์ [Authorize]
8. MapControllers           — Route → Controller

ทำไม CORS ต้องอยู่ก่อน Authentication:
  Browser ส่ง preflight (OPTIONS) request ก่อน request จริง
  ถ้า CORS ไม่ตอบ preflight → browser block request จริงเลย
  CORS ต้องตอบก่อน → browser ส่ง request จริง → Authentication ทำงาน
```

---

## ขั้นตอนที่ 10: ทดสอบ

### 10.1 Build + Run

```bash
# 1. ตรวจสอบว่า Redis รันอยู่
docker start banking-redis
# หรือ: redis-cli ping → PONG

# 2. Build
cd BankingSystem
dotnet build    # ต้อง 0 errors

# 3. Run
dotnet run --project Banking.Api

# 4. เปิด Swagger: https://localhost:xxxx/swagger
```

### 10.2 ทดสอบ Rate Limiting

```bash
# ส่ง request ซ้ำๆ เกิน 10 ครั้ง/นาที
for i in {1..15}; do
  curl -s -o /dev/null -w "%{http_code}" \
    https://localhost:xxxx/api/accounts?userId=xxx
done

# request ที่ 11-15 ควรได้ 429 Too Many Requests
```

### 10.3 ทดสอบ Token Blacklist (Logout จริง)

```bash
# 1. Login → ได้ token
POST /api/auth/login
→ { "accessToken": "eyJ..." }

# 2. ใช้ token ดู profile → 200 OK
GET /api/auth/profile
Header: Authorization: Bearer eyJ...

# 3. Logout
POST /api/auth/logout
Header: Authorization: Bearer eyJ...

# 4. ใช้ token เดิมอีกครั้ง → 401 Unauthorized (token revoked!)
GET /api/auth/profile
Header: Authorization: Bearer eyJ...
→ 401 "Token has been revoked."
```

### 10.4 ทดสอบ Balance Cache

```bash
# 1. ดูยอดเงิน → ครั้งแรก "source": "database"
GET /api/accounts/{id}/balance

# 2. ดูอีกครั้ง → "source": "cache" (เร็วกว่า!)
GET /api/accounts/{id}/balance

# 3. ฝากเงิน → cache ถูก invalidate + อัปเดต
POST /api/transactions/deposit

# 4. ดูยอดเงินอีกครั้ง → ยอดเงินใหม่ (จาก cache ที่อัปเดตแล้ว)
GET /api/accounts/{id}/balance
```

### 10.5 ทดสอบ SignalR (Real-time)

```
ใช้ tool เช่น:
- wscat: wscat -c "wss://localhost:xxxx/hubs/notifications?access_token=eyJ..."
- Postman WebSocket
- หรือสร้าง HTML + JavaScript client ง่ายๆ

ทดสอบ:
1. เปิด WebSocket connection ไปที่ /hubs/notifications
2. ฝากเงินผ่าน API
3. WebSocket ควรได้รับ event "BalanceUpdated" + "TransactionCompleted"
```

---

## ขั้นตอนที่ 11: อัปเดต appsettings ทุก Environment

### 11.1 appsettings.json (Default)

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=banking_db;Username=postgres;Password=yourpassword",
    "Redis": "localhost:6379,abortConnect=false,connectTimeout=5000"
  },
  "Jwt": {
    "Key": "YourSuperSecretKeyAtLeast32CharactersLong!@#$",
    "Issuer": "banking-api",
    "Audience": "banking-frontend",
    "AccessTokenExpirationMinutes": 15,
    "RefreshTokenExpirationDays": 7
  },
  "Redis": {
    "InstanceName": "banking:",
    "BalanceCacheTtlMinutes": 5,
    "LockTimeoutSeconds": 10,
    "RateLimitWindowSeconds": 60,
    "RateLimitMaxRequests": 10,
    "TokenBlacklistTtlMinutes": 20
  },
  "Frontend": {
    "Url": "http://localhost:3000"
  },
  "Swagger": {
    "Enabled": false
  },
  "Logging": {
    "LogLevel": {
      "Default": "Warning",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*"
}
```

### 11.2 appsettings.Development.json

```json
{
  "ConnectionStrings": {
    "Redis": "localhost:6379,abortConnect=false"
  },
  "Swagger": {
    "Enabled": true
  },
  "Redis": {
    "RateLimitMaxRequests": 100
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore.SignalR": "Debug"
    }
  }
}
```

---

## Checklist — สิ่งที่ต้องเสร็จก่อนไป Phase 4

```
NuGet Packages:
☐ StackExchange.Redis ติดตั้งใน Banking.Infrastructure
☐ Microsoft.AspNetCore.SignalR.StackExchangeRedis ติดตั้งใน Banking.Api

Redis Service:
☐ IRedisCacheService interface (Application layer)
☐ RedisCacheService implementation (Infrastructure layer)
☐ Generic cache: Set, Get, Remove, Exists
☐ Balance cache: SetBalanceCache, GetBalanceCache, InvalidateBalanceCache
☐ Distributed lock: AcquireLock, ReleaseLock (Lua script)
☐ Rate limiting: CheckRateLimit (INCR + EXPIRE)
☐ Token blacklist: BlacklistToken, IsTokenBlacklisted

SignalR:
☐ NotificationHub สร้างแล้ว (OnConnected, JoinAccountGroup)
☐ INotificationService interface
☐ NotificationService implementation (ใช้ IHubContext)
☐ Map hub ใน Program.cs: /hubs/notifications
☐ Redis backplane สำหรับ SignalR

Middleware:
☐ RateLimitMiddleware สร้างแล้ว
☐ TokenBlacklistMiddleware สร้างแล้ว
☐ ลำดับ middleware ถูกต้องใน Program.cs

อัปเดต Services:
☐ TransactionService: เพิ่ม distributed lock, cache, notification
☐ AccountsController: GetBalance ใช้ cache
☐ AuthController: Logout blacklist token จริง
☐ Transfer: lock ตามลำดับ ID (deadlock prevention)

Configuration:
☐ appsettings.json: ConnectionStrings.Redis + Redis section
☐ CORS configuration สำหรับ frontend
☐ JWT events สำหรับ SignalR (query string token)
☐ Program.cs: Redis Singleton, SignalR, CORS

Testing:
☐ Build ผ่าน 0 errors
☐ Redis เชื่อมต่อสำเร็จ (PING → PONG)
☐ Rate limiting: request ที่ 11+ → 429
☐ Logout: token ถูก blacklist → 401 เมื่อใช้ซ้ำ
☐ Balance cache: ครั้งแรก "database", ครั้งที่ 2 "cache"
☐ SignalR: ฝากเงิน → client ได้รับ BalanceUpdated event
☐ Distributed lock: 2 request พร้อมกัน → 1 สำเร็จ, 1 retry

เมื่อ checklist ครบ → พร้อมไป Phase 4: Next.js Frontend
```

---

## Troubleshooting

### "Unable to connect to Redis"
```
1. ตรวจว่า Redis รันอยู่:
   docker ps | grep redis
   หรือ: redis-cli ping → PONG

2. ตรวจ connection string:
   "localhost:6379,abortConnect=false"

3. Firewall block port 6379?
   netstat -an | grep 6379
```

### "Rate limit exceeded" ตอน develop
```
Development: ตั้ง RateLimitMaxRequests สูงๆ (100+)
หรือข้าม rate limit สำหรับ localhost:
  if (context.Connection.RemoteIpAddress?.IsIPv4MappedToIPv6 == true) ...
```

### SignalR "401 Unauthorized"
```
1. ตรวจว่าส่ง token ผ่าน query string:
   /hubs/notifications?access_token=eyJ...

2. ตรวจว่า Program.cs มี OnMessageReceived event handler

3. ตรวจว่า CORS AllowCredentials() เปิดอยู่
```

### "Distributed lock timeout" บ่อยๆ
```
1. เพิ่ม LockTimeoutSeconds (10 → 30)
2. ตรวจว่า ReleaseLock ทำงานจริง (อยู่ใน finally block)
3. ดู Redis: redis-cli KEYS "*lock*" → มี lock ค้างไหม?
   → ลบ: redis-cli DEL "banking:lock:account:xxx"
```

### Balance cache ไม่ตรง
```
Cache-aside pattern:
- ทุก write → ต้อง invalidate หรือ update cache
- ตรวจว่า TransactionService เรียก SetBalanceCacheAsync หลัง commit

Debug:
  redis-cli HGETALL "banking:balance:{accountId}"
  → เทียบกับ DB
```

### Memory leak (Redis connections)
```
StackExchange.Redis ต้อง Singleton!
❌ AddScoped<IConnectionMultiplexer>() → สร้าง connection ทุก request
✅ AddSingleton<IConnectionMultiplexer>() → 1 connection ใช้ทั้ง app
```
