using Banking.Application.Services;
using Banking.Domain.Enums;
using Banking.Domain.Interfaces;

namespace Banking.Infrastructure.Services;

/// <summary>
/// Fraud Detection — Rule-based
///
/// Rules:
///   1. Large Transaction: ยอดเงินสูงกว่า threshold (100,000+)
///   2. High Frequency: มากกว่า 5 transactions ใน 10 นาที
///   3. Unusual Hours: ธุรกรรมตี 1 - ตี 5 (เวลาไทย)
///   4. Near Daily Limit: ยอดถอนวันนี้เกิน 80% ของ daily limit
/// </summary>
public class FraudDetectionService : IFraudDetectionService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IRedisCacheService _cache;
    private readonly IAuditService _auditService;

    private const decimal LargeTransactionThreshold = 100_000;
    private const int MaxTransactionsPerWindow = 5;
    private static readonly TimeSpan FrequencyWindow = TimeSpan.FromMinutes(10);
    private const decimal DailyLimitWarningPercent = 0.8m;

    public FraudDetectionService(
        IUnitOfWork unitOfWork,
        IRedisCacheService cache,
        IAuditService auditService)
    {
        _unitOfWork = unitOfWork;
        _cache = cache;
        _auditService = auditService;
    }

    public async Task<FraudCheckResult> CheckTransactionAsync(
        Guid accountId, decimal amount, string transactionType, CancellationToken ct = default)
    {
        var reasons = new List<string>();
        var riskLevel = FraudRiskLevel.Low;

        // === Rule 1: Large Transaction ===
        if (amount >= LargeTransactionThreshold)
        {
            reasons.Add($"Large transaction: {amount:N2} THB (threshold: {LargeTransactionThreshold:N2})");
            riskLevel = FraudRiskLevel.Medium;
        }

        // === Rule 2: High Frequency (Redis counter) ===
        var frequencyKey = $"fraud:frequency:{accountId}";
        var allowed = await _cache.CheckRateLimitAsync(
            frequencyKey, MaxTransactionsPerWindow, FrequencyWindow);

        if (!allowed)
        {
            reasons.Add($"High frequency: >{MaxTransactionsPerWindow} transactions in {FrequencyWindow.TotalMinutes} minutes");
            riskLevel = FraudRiskLevel.High;
        }

        // === Rule 3: Unusual Hours (01:00 - 05:00 Bangkok time) ===
        var localHour = DateTime.UtcNow.AddHours(7).Hour; // UTC+7
        if (localHour >= 1 && localHour < 5)
        {
            reasons.Add($"Unusual hour: {localHour}:00 (Bangkok time)");
            riskLevel = (FraudRiskLevel)Math.Max((int)riskLevel, (int)FraudRiskLevel.Medium);
        }

        // === Rule 4: Near Daily Limit (สำหรับ withdrawal/transfer) ===
        if (transactionType is "Withdrawal" or "Transfer" or "TransferOut")
        {
            var account = await _unitOfWork.Accounts.GetByIdAsync(accountId, ct);
            if (account is not null)
            {
                var todayTotal = await _unitOfWork.Transactions
                    .GetTodayWithdrawalTotalAsync(accountId, ct);

                var usedPercent = (todayTotal + amount) / account.DailyWithdrawalLimit;
                if (usedPercent >= DailyLimitWarningPercent)
                {
                    reasons.Add($"Near daily limit: {usedPercent:P0} used ({todayTotal + amount:N2} / {account.DailyWithdrawalLimit:N2})");
                    riskLevel = (FraudRiskLevel)Math.Max((int)riskLevel, (int)FraudRiskLevel.Medium);
                }
            }
        }

        var isSuspicious = riskLevel > FraudRiskLevel.Low;
        var reason = reasons.Count > 0 ? string.Join("; ", reasons) : null;

        // Log suspicious activity
        if (isSuspicious)
        {
            await _auditService.LogAsync(
                userId: null,
                action: "FraudAlert",
                entityType: "Account",
                entityId: accountId.ToString(),
                newValues: new
                {
                    TransactionType = transactionType,
                    Amount = amount,
                    RiskLevel = riskLevel.ToString(),
                    Reasons = reasons
                }
            );
        }

        return new FraudCheckResult(isSuspicious, reason, riskLevel);
    }
}
