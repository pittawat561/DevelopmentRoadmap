using Banking.Domain.Enums;

namespace Banking.Domain.Entities;

public class Transaction : BaseEntity
{
    public Guid AccountId { get; set; }
    public TransactionType Type { get; set; }
    public decimal Amount { get; set; }
    public decimal BalanceBefore { get; set; }
    public decimal BalanceAfter { get; set; }
    public TransactionStatus Status { get; set; } = TransactionStatus.Pending;
    public string? Description { get; set; }
    public Guid? RelatedTransactionId { get; set; }
    public string? IpAddress { get; set; }
    public Account Account { get; set; } = null!;
    public Transaction? RelatedTransaction { get; set; }
}
