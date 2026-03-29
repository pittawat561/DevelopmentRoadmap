---
name: Banking Transaction Logic
description: Implement core banking transaction logic — Deposit, Withdraw, Transfer พร้อม DB transaction, row locking, daily limit
command: bank-txn
argument-hint: "<type: deposit|withdraw|transfer|all> [--with-redis]"
allowed-tools:
  - Read
  - Write
  - Edit
  - Glob
  - Bash
  - Grep
---

# Banking Transaction Logic Skill

คุณคือผู้เชี่ยวชาญ financial transaction processing สำหรับระบบ Banking ที่ต้องรับประกัน data integrity และ ACID compliance

## Input
- **argument แรก:** Transaction type (required) — `deposit`, `withdraw`, `transfer`, `all`
- **flag:** `--with-redis` — รวม Redis caching + distributed lock (optional)

## Critical Domain Knowledge

### Entity References
- `Account.cs`: Balance, AvailableBalance, DailyWithdrawalLimit, Status
- `Transaction.cs`: Type, Amount, BalanceBefore, BalanceAfter, Status, ReferenceNumber
- `Transfer.cs`: FromAccountId, ToAccountId, DebitTransactionId, CreditTransactionId
- `IAccountRepository.GetByIdForUpdateAsync()`: Row-level lock (`SELECT ... FOR UPDATE`)
- `ITransactionRepository.GetTodayWithdrawalTotalAsync()`: Daily limit check
- `IUnitOfWork`: BeginTransaction, CommitTransaction, RollbackTransaction

### Domain Exceptions
- `InsufficientFundsException` — balance < amount
- `AccountFrozenException` — account status != Active
- `DailyLimitExceededException` — today's total + amount > daily limit
- `NotFoundException` — account/transaction not found

## ขั้นตอนการทำงาน

### Step 1: อ่าน Context
1. อ่าน `Banking.Domain/Entities/` — ทุก entity ที่เกี่ยวข้อง
2. อ่าน `Banking.Domain/Interfaces/IRepositories.cs` — repository methods
3. อ่าน `Banking.Domain/Exceptions/` — domain exceptions
4. อ่าน `Banking.Infrastructure/Repositories/` — ดู implementation details
5. อ่าน existing handlers ใน `Banking.Application/` (ถ้ามี)

### Step 2: Implement Transaction Logic

#### Deposit Flow
```
1. Validate: amount > 0
2. Validate: account exists + status = Active
3. Begin DB Transaction
4. Lock account row: GetByIdForUpdateAsync(accountId)
5. Create Transaction record:
   - Type = Deposit
   - BalanceBefore = account.Balance
   - BalanceAfter = account.Balance + amount
   - Status = Completed
   - ReferenceNumber = GenerateReferenceNumber()
6. Update account.Balance += amount
7. Update account.AvailableBalance += amount
8. Commit Transaction
9. (Optional) Invalidate Redis cache: balance:{accountId}
10. Return TransactionResponse
```

#### Withdrawal Flow (CRITICAL — most complex)
```
1. Validate: amount > 0
2. Validate: account exists + status = Active
3. Validate: user KYC = Verified
4. Check Rate Limit (Redis): ไม่เกิน 10 ครั้ง/นาที
5. Begin DB Transaction
6. Lock account row: GetByIdForUpdateAsync(accountId)
7. Check: account.Balance >= amount → InsufficientFundsException
8. Check Daily Limit:
   - todayTotal = GetTodayWithdrawalTotalAsync(accountId)
   - if (todayTotal + amount > account.DailyWithdrawalLimit)
     → DailyLimitExceededException
9. Create Transaction record:
   - Type = Withdrawal
   - BalanceBefore = account.Balance
   - BalanceAfter = account.Balance - amount
   - Status = Completed
10. Update account.Balance -= amount
11. Update account.AvailableBalance -= amount
12. Commit Transaction
13. (Optional) Invalidate Redis cache
14. Return TransactionResponse
```

#### Transfer Flow (TWO linked transactions)
```
1. Validate: amount > 0, fromAccountId != toAccountId
2. Validate: both accounts exist + Active
3. Begin DB Transaction
4. Lock BOTH accounts (consistent ordering by ID to prevent deadlock):
   - Sort [fromAccountId, toAccountId] ascending
   - Lock first, then lock second
5. Check: fromAccount.Balance >= amount + fee
6. Create Transfer record
7. Create Debit Transaction (TransferOut):
   - AccountId = fromAccountId
   - Amount = -(amount + fee)
   - BalanceBefore/After
8. Create Credit Transaction (TransferIn):
   - AccountId = toAccountId
   - Amount = +amount
   - BalanceBefore/After
9. Link: debitTxn.RelatedTransactionId = creditTxn.Id (and vice versa)
10. Update Transfer: DebitTransactionId, CreditTransactionId
11. Update both account balances
12. Commit Transaction
13. Return TransferResponse
```

### Step 3: Reference Number Generator
```csharp
// Format: TXN-{yyyyMMdd}-{random6}
private static string GenerateReferenceNumber()
{
    var date = DateTime.UtcNow.ToString("yyyyMMdd");
    var random = Guid.NewGuid().ToString("N")[..6].ToUpper();
    return $"TXN-{date}-{random}";
}
```

### Step 4: สร้าง DTOs
- `DepositRequest { AccountId, Amount, Description? }`
- `WithdrawRequest { AccountId, Amount, Description? }`
- `TransferRequest { FromAccountId, ToAccountId, Amount, Description? }`
- `TransactionResponse { Id, ReferenceNumber, Type, Amount, BalanceBefore, BalanceAfter, Status, CreatedAt }`
- `TransferResponse { Id, FromAccount, ToAccount, Amount, Fee, Status, DebitTransaction, CreditTransaction }`

### Step 5: Validators
- Amount: `GreaterThan(0)`, `LessThanOrEqualTo(1_000_000)` (max per transaction)
- AccountId: `NotEmpty()`
- FromAccountId != ToAccountId (for transfer)

### Step 6: ตรวจสอบ
1. Build: `dotnet build`
2. ตรวจ UnitOfWork implementation มีอยู่
3. ตรวจ DB Transaction flow ครบ (Begin → Commit/Rollback)
4. ตรวจ row locking ใช้ `GetByIdForUpdateAsync`
5. ตรวจ deadlock prevention สำหรับ transfer (consistent lock ordering)

## ตัวอย่างการใช้งาน
```
/bank-txn deposit              → implement deposit เท่านั้น
/bank-txn withdraw --with-redis → implement withdrawal + Redis lock
/bank-txn transfer             → implement transfer
/bank-txn all --with-redis     → implement ทั้ง 3 + Redis integration
```
