namespace Banking.Application.Services;

/// <summary>
/// สร้าง Reference Number format: "TXN-20260329-XXXXXX"
/// ใช้วันที่ + random 6 หลัก
/// </summary>
public static class ReferenceNumberGenerator
{
    public static string Generate()
    {
        var date = DateTime.UtcNow.ToString("yyyyMMdd");
        var random = Random.Shared.Next(100000, 999999);
        return $"TXN-{date}-{random}";
    }
}