namespace Banking.Domain.Exceptions;

// หาไม่เจอ (User, Account ที่ระบุ id ไม่มีในระบบ)
public class NotFoundException : Exception
{
    public NotFoundException(string entity, object id)
        : base($"{entity} with id '{id}' was not found.") { }
}

// เงินไม่พอ (ถอน/โอนเกินยอดที่มี)
public class InsufficientFundsException : Exception
{
    public InsufficientFundsException(decimal balance, decimal amount)
        : base($"Insufficient funds. Balance: {balance:N2}, Requested: {amount:N2}") { }
}

// บัญชีถูกระงับ (frozen — ห้ามทำธุรกรรม)
public class AccountFrozenException : Exception
{
    public AccountFrozenException(string accountNumber)
        : base($"Account '{accountNumber}' is frozen.") { }
}

// ถอนเกินวงเงินต่อวัน
public class DailyLimitExceededException : Exception
{
    public DailyLimitExceededException(decimal limit, decimal todayTotal, decimal requested)
        : base($"Daily limit exceeded. Limit: {limit:N2}, Today: {todayTotal:N2}, Requested: {requested:N2}") { }
}

// ข้อมูลซ้ำ (email ซ้ำ, เลขบัญชีซ้ำ)
public class DuplicateException : Exception
{
    public DuplicateException(string message) : base(message) { }
}

// บัญชีถูกล็อค (login ผิดหลายครั้ง)
public class AccountLockedException : Exception
{
    public AccountLockedException() : base("Account is locked due to too many failed attempts.") { }
}
