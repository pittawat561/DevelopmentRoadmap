namespace Banking.Application.Services;

/// <summary>
/// สร้างเลขบัญชี format: "XXXX-XXXX-XXXX"
/// ใช้ Random + เช็คซ้ำกับ database
/// </summary>
public static class AccountNumberGenerator
{
    public static string Generate()
    {
        var random = Random.Shared;
        var part1 = random.Next(1000, 9999);
        var part2 = random.Next(1000, 9999);
        var part3 = random.Next(1000, 9999);
        return $"{part1}-{part2}-{part3}";
    }
}