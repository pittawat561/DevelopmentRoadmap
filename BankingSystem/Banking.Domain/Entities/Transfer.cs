using Banking.Domain.Enums;

namespace Banking.Domain.Entities;

public class Transfer
{
    public Guid FromAccountId { get; set; }
    public Guid ToAccountId { get; set; }
    public decimal Amount { get; set; }
    public decimal Fee { get; set; } = 0;
    public TransactionStatus Status { get; set; } = TransactionStatus.Pending;
    public Guid? DebitTransactionId { get; set; }
    public Guid? CreditTransactionId { get; set; }
    public Account FromAccount { get; set; } = null!;
    public Account ToAccount { get; set; } = null!;
    public Transaction? DebitTransaction { get; set; }
    public Transaction? CreditTransaction { get; set; }
}
