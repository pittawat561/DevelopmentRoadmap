using Banking.Domain.Entities;

namespace Banking.Application.Services;

/// <summary>
/// JWT Service Interface — อยู่ใน Application layer
/// Business Logic ต้องรู้ว่ามี JWT service แต่ไม่ต้องรู้ implementation detail
/// </summary>
public interface IJwtService
{
    /// <summary>
    /// สร้าง Access Token (JWT) — อายุสั้น 15 นาที
    /// </summary>
    string GenerateAccessToken(User user);

    /// <summary>
    /// สร้าง Refresh Token — อายุยาว 7 วัน
    /// เป็น random string ไม่ใช่ JWT
    /// </summary>
    string GenerateRefreshToken();

    /// <summary>
    /// ดึง User ID จาก expired token — ใช้ตอน refresh
    /// </summary>
    Guid? GetUserIdFromExpiredToken(string token);
}