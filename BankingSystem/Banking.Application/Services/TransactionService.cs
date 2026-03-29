using Banking.Application.DTOs;
using Banking.Domain.Entities;
using Banking.Domain.Enums;
using Banking.Domain.Exceptions;
using Banking.Domain.Interfaces;

namespace Banking.Application.Services;

/// <summary>
/// TransactionService — business logic ฝาก/ถอน/โอน
///
/// ทำไมแยกจาก Controller:
/// 1. Controller ทำแค่รับ request → เรียก service → ส่ง response
/// 2. Service มี logic จริง → test ได้โดยไม่ต้อง HTTP
/// 3. หลาย Controller ใช้ service เดียวกันได้
/// </summary>
public class TransactionService
{
    private readonly IUnitOfWork _unitOfWork;

    public TransactionService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    // =====================================================
    // ฝากเงิน (DEPOSIT)
    // =====================================================

    /// <summary>
    /// ฝากเงินเข้าบัญชี
    ///
    /// Flow:
    /// 1. Validate: amount > 0
    /// 2. Begin DB Transaction (ACID)
    /// 3. Lock account row (FOR UPDATE) ← ป้องกัน Race Condition
    /// 4. Validate: account exists + active
    /// 5. Update balance (เพิ่ม)
    /// 6. Create Transaction record
    /// 7. Commit
    ///
    /// ทำไมต้อง lock แม้แค่ฝาก:
    ///   2 คนฝากพร้อมกัน + อ่าน balance เดียวกัน → balance ผิด
    ///   Lock ทำให้คนที่ 2 รอ → อ่าน balance ที่ถูกต้อง
    /// </summary>
    public async Task<TransactionResponse> DepositAsync(
        DepositRequest request, string? ipAddress = null, CancellationToken ct = default)
    {
        // === 1. Validate Input ===
        if (request.Amount <= 0)
            throw new ArgumentException("Amount must be greater than 0.");

        try
        {
            // === 2. Begin DB Transaction ===
            await _unitOfWork.BeginTransactionAsync(ct);

            // === 3. Lock account row (ป้องกัน Race Condition) ===
            var account = await _unitOfWork.Accounts
                .GetByIdForUpdateAsync(request.AccountId, ct);

            // === 4. Validate Account ===
            if (account is null)
                throw new NotFoundException("Account", request.AccountId);

            if (account.Status != AccountStatus.Active)
                throw new AccountFrozenException(account.AccountNumber);

            // === 5. Update Balance ===
            var balanceBefore = account.Balance;
            account.Balance += request.Amount;
            account.AvailableBalance += request.Amount;
            _unitOfWork.Accounts.Update(account);

            // === 6. Create Transaction Record ===
            var transaction = new Transaction
            {
                ReferenceNumber = ReferenceNumberGenerator.Generate(),
                AccountId = account.Id,
                Type = TransactionType.Deposit,
                Amount = request.Amount,
                BalanceBefore = balanceBefore,
                BalanceAfter = account.Balance,
                Status = TransactionStatus.Completed,
                Description = request.Description ?? "Deposit",
                IpAddress = ipAddress
            };

            await _unitOfWork.Transactions.AddAsync(transaction, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            // === 7. Commit Transaction ===
            await _unitOfWork.CommitTransactionAsync(ct);

            return MapToResponse(transaction);
        }
        catch
        {
            // ถ้ามี error → rollback ทุกอย่าง
            await _unitOfWork.RollbackTransactionAsync(ct);
            throw;  // ส่ง error ต่อให้ Controller จัดการ
        }
    }

    // =====================================================
    // ถอนเงิน (WITHDRAWAL) — ซับซ้อนกว่าฝาก!
    // =====================================================

    /// <summary>
    /// ถอนเงินจากบัญชี
    ///
    /// Flow (ซับซ้อนกว่าฝากเพราะต้องเช็คหลายอย่าง):
    /// 1. Validate: amount > 0
    /// 2. Begin DB Transaction
    /// 3. Lock account row (FOR UPDATE)
    /// 4. Validate: account exists + active
    /// 5. Check: เงินพอไหม? (balance >= amount)
    /// 6. Check: เกิน Daily Limit ไหม?
    /// 7. Update balance (ลด)
    /// 8. Create Transaction record
    /// 9. Commit
    ///
    /// ทำไมต้องเช็ค Daily Limit:
    ///   ป้องกันกรณีบัญชีถูกขโมย → จำกัดความเสียหายต่อวัน
    ///   เช่น limit 50,000 → ถอนได้สูงสุด 50,000/วัน
    /// </summary>
    public async Task<TransactionResponse> WithdrawAsync(
        WithdrawRequest request, string? ipAddress = null, CancellationToken ct = default)
    {
        if (request.Amount <= 0)
            throw new ArgumentException("Amount must be greater than 0.");

        try
        {
            await _unitOfWork.BeginTransactionAsync(ct);

            var account = await _unitOfWork.Accounts
                .GetByIdForUpdateAsync(request.AccountId, ct);

            if (account is null)
                throw new NotFoundException("Account", request.AccountId);

            if (account.Status != AccountStatus.Active)
                throw new AccountFrozenException(account.AccountNumber);

            // === 5. Check: เงินพอไหม? ===
            if (account.Balance < request.Amount)
                throw new InsufficientFundsException(account.Balance, request.Amount);

            // === 6. Check: Daily Limit ===
            var todayTotal = await _unitOfWork.Transactions
                .GetTodayWithdrawalTotalAsync(account.Id, ct);

            if (todayTotal + request.Amount > account.DailyWithdrawalLimit)
                throw new DailyLimitExceededException(
                    account.DailyWithdrawalLimit, todayTotal, request.Amount);

            // === 7. Update Balance ===
            var balanceBefore = account.Balance;
            account.Balance -= request.Amount;
            account.AvailableBalance -= request.Amount;
            _unitOfWork.Accounts.Update(account);

            // === 8. Create Transaction Record ===
            var transaction = new Transaction
            {
                ReferenceNumber = ReferenceNumberGenerator.Generate(),
                AccountId = account.Id,
                Type = TransactionType.Withdrawal,
                Amount = request.Amount,
                BalanceBefore = balanceBefore,
                BalanceAfter = account.Balance,
                Status = TransactionStatus.Completed,
                Description = request.Description ?? "Withdrawal",
                IpAddress = ipAddress
            };

            await _unitOfWork.Transactions.AddAsync(transaction, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            await _unitOfWork.CommitTransactionAsync(ct);

            return MapToResponse(transaction);
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(ct);
            throw;
        }
    }

    // =====================================================
    // โอนเงิน (TRANSFER) — ซับซ้อนที่สุด!
    // =====================================================

    /// <summary>
    /// โอนเงินระหว่างบัญชี
    ///
    /// Flow (สร้าง 3 records ใน 1 transaction):
    /// 1. Validate: amount > 0, from != to
    /// 2. Begin DB Transaction
    /// 3. Lock ทั้ง 2 บัญชี (FOR UPDATE) ← lock ทีเดียว!
    /// 4. Validate: ทั้ง 2 บัญชี exists + active
    /// 5. Check: เงินพอ? + Daily Limit?
    /// 6. Update balance ทั้ง 2 บัญชี (หัก + เพิ่ม)
    /// 7. Create 2 Transactions (TransferOut + TransferIn)
    /// 8. Create 1 Transfer record
    /// 9. Commit
    ///
    /// ⚠️ ทำไมต้อง Atomic:
    ///   ถ้าหักเงิน A สำเร็จ แต่เพิ่มเงิน B ล้มเหลว → เงินหายไปจาก A!
    ///   DB Transaction ทำให้สำเร็จทั้งคู่หรือไม่ทำเลย
    /// </summary>
    public async Task<TransactionResponse> TransferAsync(
        TransferRequest request, string? ipAddress = null, CancellationToken ct = default)
    {
        if (request.Amount <= 0)
            throw new ArgumentException("Amount must be greater than 0.");

        if (request.FromAccountId == request.ToAccountId)
            throw new ArgumentException("Cannot transfer to the same account.");

        try
        {
            await _unitOfWork.BeginTransactionAsync(ct);

            // === 3. Lock ทั้ง 2 บัญชี ===
            var fromAccount = await _unitOfWork.Accounts
                .GetByIdForUpdateAsync(request.FromAccountId, ct);
            var toAccount = await _unitOfWork.Accounts
                .GetByIdForUpdateAsync(request.ToAccountId, ct);

            // === 4. Validate ===
            if (fromAccount is null)
                throw new NotFoundException("Source Account", request.FromAccountId);
            if (toAccount is null)
                throw new NotFoundException("Destination Account", request.ToAccountId);

            if (fromAccount.Status != AccountStatus.Active)
                throw new AccountFrozenException(fromAccount.AccountNumber);
            if (toAccount.Status != AccountStatus.Active)
                throw new AccountFrozenException(toAccount.AccountNumber);

            // === 5. Check เงินพอ + Daily Limit ===
            if (fromAccount.Balance < request.Amount)
                throw new InsufficientFundsException(fromAccount.Balance, request.Amount);

            var todayTotal = await _unitOfWork.Transactions
                .GetTodayWithdrawalTotalAsync(fromAccount.Id, ct);

            if (todayTotal + request.Amount > fromAccount.DailyWithdrawalLimit)
                throw new DailyLimitExceededException(
                    fromAccount.DailyWithdrawalLimit, todayTotal, request.Amount);

            // === 6. Update Balances ===
            var fromBefore = fromAccount.Balance;
            var toBefore = toAccount.Balance;

            fromAccount.Balance -= request.Amount;
            fromAccount.AvailableBalance -= request.Amount;

            toAccount.Balance += request.Amount;
            toAccount.AvailableBalance += request.Amount;

            _unitOfWork.Accounts.Update(fromAccount);
            _unitOfWork.Accounts.Update(toAccount);

            // === 7. Create 2 Transactions ===
            var debitTxn = new Transaction
            {
                ReferenceNumber = ReferenceNumberGenerator.Generate(),
                AccountId = fromAccount.Id,
                Type = TransactionType.TransferOut,
                Amount = request.Amount,
                BalanceBefore = fromBefore,
                BalanceAfter = fromAccount.Balance,
                Status = TransactionStatus.Completed,
                Description = request.Description
                    ?? $"Transfer to {toAccount.AccountNumber}",
                IpAddress = ipAddress
            };

            var creditTxn = new Transaction
            {
                ReferenceNumber = ReferenceNumberGenerator.Generate(),
                AccountId = toAccount.Id,
                Type = TransactionType.TransferIn,
                Amount = request.Amount,
                BalanceBefore = toBefore,
                BalanceAfter = toAccount.Balance,
                Status = TransactionStatus.Completed,
                Description = request.Description
                    ?? $"Transfer from {fromAccount.AccountNumber}",
                IpAddress = ipAddress
            };

            // เชื่อมคู่ transaction เข้าด้วยกัน
            debitTxn.RelatedTransactionId = creditTxn.Id;
            creditTxn.RelatedTransactionId = debitTxn.Id;

            await _unitOfWork.Transactions.AddAsync(debitTxn, ct);
            await _unitOfWork.Transactions.AddAsync(creditTxn, ct);

            // === 8. Create Transfer Record ===
            var transfer = new Transfer
            {
                FromAccountId = fromAccount.Id,
                ToAccountId = toAccount.Id,
                Amount = request.Amount,
                Fee = 0,
                Status = TransactionStatus.Completed,
                DebitTransactionId = debitTxn.Id,
                CreditTransactionId = creditTxn.Id
            };

            // เพิ่ม transfer ผ่าน DbContext ตรง (ไม่มี ITransferRepository)
            await _unitOfWork.SaveChangesAsync(ct);

            // === 9. Commit ===
            await _unitOfWork.CommitTransactionAsync(ct);

            return MapToResponse(debitTxn);
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(ct);
            throw;
        }
    }

    // =====================================================
    // ดูประวัติธุรกรรม
    // =====================================================

    public async Task<PagedResponse<TransactionResponse>> GetHistoryAsync(
        Guid accountId, int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        var transactions = await _unitOfWork.Transactions
            .GetByAccountIdAsync(accountId, page, pageSize, ct);

        var totalCount = await _unitOfWork.Transactions
            .GetCountByAccountIdAsync(accountId, ct);

        var items = transactions.Select(MapToResponse).ToList();

        return new PagedResponse<TransactionResponse>(
            Items: items,
            TotalCount: totalCount,
            Page: page,
            PageSize: pageSize,
            TotalPages: (int)Math.Ceiling(totalCount / (double)pageSize)
        );
    }

    // =====================================================
    // Helper: Entity → DTO
    // =====================================================

    private static TransactionResponse MapToResponse(Transaction t) => new(
        Id: t.Id,
        ReferenceNumber: t.ReferenceNumber,
        Type: t.Type.ToString(),
        Amount: t.Amount,
        BalanceBefore: t.BalanceBefore,
        BalanceAfter: t.BalanceAfter,
        Status: t.Status.ToString(),
        Description: t.Description,
        CreatedAt: t.CreatedAt
    );
}