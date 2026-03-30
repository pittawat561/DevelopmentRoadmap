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