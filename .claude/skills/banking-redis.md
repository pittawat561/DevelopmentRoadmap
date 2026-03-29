---
name: Banking Redis Integration
description: Integrate Redis สำหรับ Banking System — balance cache, distributed lock, rate limiting, session management
command: bank-redis
argument-hint: "<feature: cache|lock|rate-limit|session|all>"
allowed-tools:
  - Read
  - Write
  - Edit
  - Glob
  - Bash
  - Grep
---

# Banking Redis Integration Skill

คุณคือผู้เชี่ยวชาญ Redis integration สำหรับ high-concurrency banking system

## Input
- **argument:** feature ที่ต้องการ (required)
  - `cache` — Balance caching strategy
  - `lock` — Distributed lock สำหรับ transaction safety
  - `rate-limit` — Rate limiting per user per endpoint
  - `session` — JWT session + token blacklist
  - `all` — ทุก feature

## Project Context
- **Redis package:** StackExchange.Redis 2.12.8 (มีอยู่แล้วใน Infrastructure)
- **Connection string:** อยู่ใน appsettings.json `ConnectionStrings:Redis`
- **Blueprint Redis strategy:** 5 use cases — cache, lock, rate limit, session, pub/sub

## ขั้นตอนการทำงาน

### Step 1: อ่าน Context
1. อ่าน `Banking.Infrastructure/Banking.Infrastructure.csproj` — ตรวจ Redis package
2. อ่าน `Banking.Api/Program.cs` — ดู service registration
3. อ่าน `Banking.Api/appsettings.json` — ดู Redis connection config
4. อ่าน existing Redis services ใน `Banking.Infrastructure/Services/`

### Step 2: Setup Redis Connection
**ไฟล์:** `Banking.Infrastructure/Services/RedisService.cs`
```csharp
public interface ICacheService
{
    Task<T?> GetAsync<T>(string key);
    Task SetAsync<T>(string key, T value, TimeSpan? expiry = null);
    Task RemoveAsync(string key);
    Task<bool> ExistsAsync(string key);
}

public interface IDistributedLockService
{
    Task<IAsyncDisposable?> AcquireLockAsync(string resource, TimeSpan expiry);
}

public interface IRateLimitService
{
    Task<bool> IsAllowedAsync(string key, int maxRequests, TimeSpan window);
}
```

### Step 3: Implement Features

#### Balance Cache
```
Key pattern: "balance:{accountId}"
TTL: 5 minutes
Strategy: Cache-aside (read from cache → miss → read from DB → write to cache)
Invalidation: Delete key after any balance-changing transaction

Operations:
- GetBalanceAsync(accountId) → check cache first, fallback to DB
- InvalidateBalanceAsync(accountId) → remove cache key after transaction
- SetBalanceAsync(accountId, balance) → cache after DB read
```

#### Distributed Lock
```
Key pattern: "lock:account:{accountId}"
TTL: 10 seconds (auto-release to prevent deadlock)
Implementation: RedLock algorithm (single instance simplified)

Usage in transaction handlers:
await using var lock = await _lockService.AcquireLockAsync(
    $"lock:account:{accountId}", TimeSpan.FromSeconds(10));
if (lock is null) throw new ConflictException("Account is being processed");

// ... perform transaction ...
// lock auto-releases via IAsyncDisposable
```

#### Rate Limiting
```
Key pattern: "ratelimit:{userId}:{endpoint}"
Strategy: Sliding window counter
Default limits:
- /api/transactions/*: 10 requests/minute
- /api/auth/login: 5 requests/minute
- /api/accounts/*: 30 requests/minute

Implementation: Middleware or ActionFilter
```

#### Session / Token Blacklist
```
Key patterns:
- "session:{userId}" → active session metadata (JSON)
- "blacklist:{jti}" → revoked token ID
  TTL = remaining token lifetime

Operations:
- BlacklistTokenAsync(jti, expiry)
- IsBlacklistedAsync(jti) → bool
- SetSessionAsync(userId, metadata)
- GetSessionAsync(userId)
```

### Step 4: Register ใน DI
```csharp
// Program.cs or DependencyInjection.cs
services.AddSingleton<IConnectionMultiplexer>(
    ConnectionMultiplexer.Connect(connectionString));
services.AddScoped<ICacheService, RedisCacheService>();
services.AddScoped<IDistributedLockService, RedisLockService>();
services.AddScoped<IRateLimitService, RedisRateLimitService>();
```

### Step 5: Rate Limit Middleware
```csharp
public class RateLimitMiddleware
{
    // Check Redis counter before allowing request
    // Return 429 Too Many Requests if limit exceeded
    // Include Retry-After header
}
```

### Step 6: ตรวจสอบ
1. Build: `dotnet build`
2. ตรวจ Redis connection string ใน appsettings
3. ตรวจว่า cache invalidation ถูกเรียกหลังทุก balance change
4. ตรวจ lock TTL เหมาะสม (ไม่สั้นเกินจน expire ก่อน transaction เสร็จ)

## ตัวอย่างการใช้งาน
```
/bank-redis cache       → implement balance caching
/bank-redis lock        → implement distributed lock
/bank-redis rate-limit  → implement rate limiting middleware
/bank-redis session     → implement JWT session + blacklist
/bank-redis all         → implement ทุก feature
```
