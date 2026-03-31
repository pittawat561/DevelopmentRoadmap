namespace Banking.Application.Services;

/// <summary>
/// Fraud Detection Service — ตรวจจับธุรกรรมผิดปกติ
/// Rule-based detection:
///   1. จำนวนเงินผิดปกติ (สูงกว่า threshold)
///   2. ความถี่ผิดปกติ (หลายครั้งใน time window สั้นๆ)
///   3. เวลาผิดปกติ (ดึกมาก)
///   4. ใกล้ daily limit
/// </summary>
public interface IFraudDetectionService
{
    Task<FraudCheckResult> CheckTransactionAsync(
        Guid accountId,
        decimal amount,
        string transactionType,
        CancellationToken ct = default);
}

public record FraudCheckResult(
    bool IsSuspicious,
    string? Reason,
    FraudRiskLevel RiskLevel
);

public enum FraudRiskLevel
{
    Low,       // ปกติ
    Medium,    // ต้องจับตา (flag + log)
    High,      // ต้อง review (flag + notify admin)
    Critical   // block ทันที
}
