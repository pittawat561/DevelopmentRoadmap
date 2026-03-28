
using Banking.Domain.Enums;
using System.Transactions;

namespace Banking.Domain.Entities;

public class Account : BaseEntity
{
    public Guid UserId { get; set; }
    public string AccountNumber { get; set; } = string.Empty;
    public AccountType Type { get; set; }  = AccountType.Savings;
    public string Currency { get; set; } = "THB";
    public decimal Balance { get; set; } = 0;
    public decimal AvailableBalance { get; set; } = 0;
    public AccountStatus Status { get; set; } = AccountStatus.Active;
    public User User { get; set; } = null!;
    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
}
