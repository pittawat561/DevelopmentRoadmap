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
        await _db.StringSetAsync(GetKey(key), json, expiry.HasValue ? (Expiration)expiry.Value : default(Expiration));
    }

    public async Task<T?> GetAsync<T>(string key)
    {
        var value = await _db.StringGetAsync(GetKey(key));
        if (value.IsNullOrEmpty) return default;
        return JsonSerializer.Deserialize<T>(value.ToString());
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