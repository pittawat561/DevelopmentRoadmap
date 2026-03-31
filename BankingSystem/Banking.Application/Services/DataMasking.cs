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

    public static string MaskPhone(string phone)
    {
        if (string.IsNullOrEmpty(phone) || phone.Length < 4)
            return "****";

        return $"{"".PadLeft(phone.Length - 4, '*')}{phone[^4..]}";
    }
}
