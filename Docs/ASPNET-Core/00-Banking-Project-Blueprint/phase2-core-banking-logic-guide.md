# Phase 2: Core Banking Logic — คู่มือทำทีละขั้นตอน

> Repository Pattern + UnitOfWork + Deposit/Withdraw/Transfer + Controllers
> ทุกขั้นตอนอธิบายว่า "ทำไม" ต้องทำ + สร้างเพื่ออะไร

---

## สิ่งที่ต้องเสร็จก่อน (จาก Phase 1)

```
☑ PostgreSQL + banking_db สร้างแล้ว
☑ Solution 6 projects (Domain, Application, Infrastructure, Api, Tests x2)
☑ Entities: User, Account, Transaction, Transfer, AuditLog, BaseEntity
☑ EF Core Configurations ครบ 5 ตาราง
☑ Migration + Seed Data สำเร็จ
☑ dotnet build ผ่าน 0 errors
```

---

## ขั้นตอนที่ 1: สร้าง Repository Implementations

### 1.1 ทำไมต้องมี Repository?

```
ปัญหาถ้าไม่มี Repository:
Controller เรียก DbContext ตรงๆ → Business Logic กระจายอยู่ทุกที่

// ❌ แบบนี้ไม่ดี — Controller ทำทุกอย่าง
public class AccountsController : ControllerBase
{
    public async Task<IActionResult> GetBalance(Guid id)
    {
        var account = await _context.Accounts
            .Where(a => !a.IsDeleted)           // กรอง soft delete เอง
            .Where(a => a.UserId == userId)     // กรอง owner เอง
            .FirstOrDefaultAsync(a => a.Id == id);
        // ... validation เอง, mapping เอง
    }
}

// ✅ แบบนี้ดี — Repository ซ่อน database logic
public class AccountsController : ControllerBase
{
    public async Task<IActionResult> GetBalance(Guid id)
    {
        var account = await _unitOfWork.Accounts.GetByIdAsync(id);
        // Clean, simple, testable
    }
}

ข้อดี Repository Pattern:
1. แยก database logic ออกจาก business logic
2. Test ได้ง่าย (Mock repository แทน database จริง)
3. เปลี่ยน database ได้โดยไม่แก้ Controller
4. Query ซับซ้อนอยู่ที่เดียว ไม่กระจาย
```

### 1.2 สร้าง Generic Repository

```
📁 ที่ต้องสร้าง: Banking.Infrastructure/Repositories/Repository.cs

ทำไม: เป็น base class ที่ทุก repository ใช้ร่วมกัน
ลดการเขียนโค้ดซ้ำ (GetById, GetAll, Add, Update, Remove)
```

```csharp
// Banking.Infrastructure/Repositories/Repository.cs

using Banking.Domain.Entities;
using Banking.Domain.Interfaces;
using Banking.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Banking.Infrastructure.Repositories;

/// <summary>
/// Generic Repository — implement method พื้นฐาน CRUD ให้ทุก Entity
/// ทุก specific repository (UserRepository, AccountRepository) จะ inherit จากนี้
/// ไม่ต้องเขียน GetById, GetAll, Add, Update, Remove ซ้ำทุกตัว
/// </summary>
public class Repository<T> : IRepository<T> where T : BaseEntity
{
    // _context: ใช้เข้าถึง database ผ่าน EF Core
    // protected: ให้ class ลูก (UserRepository ฯลฯ) เข้าถึงได้
    protected readonly AppDbContext _context;

    // _dbSet: เหมือนตารางใน database (เช่น DbSet<User> = ตาราง users)
    protected readonly DbSet<T> _dbSet;

    public Repository(AppDbContext context)
    {
        _context = context;
        _dbSet = context.Set<T>();  // ดึง DbSet ของ Entity type T
    }

    /// <summary>
    /// ค้นหาจาก Id — ใช้ FindAsync ซึ่งดูใน cache ก่อน ถ้าไม่มีค่อย query database
    /// เร็วกว่า FirstOrDefaultAsync เมื่อค้นหาด้วย Primary Key
    /// </summary>
    public virtual async Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _dbSet.FindAsync([id], ct);
    }

    /// <summary>
    /// ดึงทั้งหมด — QueryFilter (IsDeleted) ถูกใช้อัตโนมัติ
    /// ToListAsync() ส่ง query ไป database แล้วแปลงเป็น List ใน memory
    /// </summary>
    public virtual async Task<List<T>> GetAllAsync(CancellationToken ct = default)
    {
        return await _dbSet.ToListAsync(ct);
    }

    /// <summary>
    /// เพิ่ม Entity ใหม่ — ยังไม่บันทึกจริงจนกว่าจะ SaveChanges
    /// EF Core จะ track entity นี้ใน state "Added"
    /// </summary>
    public virtual async Task AddAsync(T entity, CancellationToken ct = default)
    {
        await _dbSet.AddAsync(entity, ct);
    }

    /// <summary>
    /// อัปเดต Entity — EF Core จะ track การเปลี่ยนแปลง
    /// ไม่ต้อง query ก่อน ถ้ามี entity อยู่แล้ว แค่ attach + mark Modified
    /// </summary>
    public virtual void Update(T entity)
    {
        _dbSet.Update(entity);
    }

    /// <summary>
    /// Soft Delete — ไม่ลบจริง แค่เปลี่ยน IsDeleted = true
    /// ระบบธนาคารห้ามลบข้อมูลจริง ต้องเก็บไว้เพื่อ audit
    /// </summary>
    public virtual void Remove(T entity)
    {
        entity.IsDeleted = true;
        _dbSet.Update(entity);
    }
}
```

### 1.3 สร้าง UserRepository

```
📁 Banking.Infrastructure/Repositories/UserRepository.cs

ทำไม: method เฉพาะสำหรับ User เช่น ค้นหาจาก email, เช็คซ้ำ
ใช้ตอน Login, Register
```

```csharp
using Banking.Domain.Entities;
using Banking.Domain.Interfaces;
using Banking.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Banking.Infrastructure.Repositories;

public class UserRepository : Repository<User>, IUserRepository
{
    public UserRepository(AppDbContext context) : base(context) { }

    /// <summary>
    /// ค้นหา User จาก email — ใช้ตอน Login
    /// Include(u => u.Accounts) — โหลด Accounts มาด้วย (Eager Loading)
    /// เพื่อให้ดูข้อมูลบัญชีได้ทันทีหลัง login
    /// </summary>
    public async Task<User?> GetByEmailAsync(string email, CancellationToken ct = default)
    {
        return await _dbSet
            .Include(u => u.Accounts)
            .FirstOrDefaultAsync(u => u.Email == email, ct);
    }

    /// <summary>
    /// เช็คว่า email มีอยู่แล้วไหม — ใช้ตอน Register
    /// AnyAsync เร็วกว่า FirstOrDefault เพราะแค่เช็ค EXISTS (ไม่โหลดข้อมูล)
    /// </summary>
    public async Task<bool> EmailExistsAsync(string email, CancellationToken ct = default)
    {
        return await _dbSet.AnyAsync(u => u.Email == email, ct);
    }

    public async Task<bool> PhoneExistsAsync(string phone, CancellationToken ct = default)
    {
        return await _dbSet.AnyAsync(u => u.Phone == phone, ct);
    }
}
```

### 1.4 สร้าง AccountRepository

```
📁 Banking.Infrastructure/Repositories/AccountRepository.cs

ทำไม: method เฉพาะ Account เช่น ค้นหาจากเลขบัญชี, lock สำหรับ update
สำคัญมาก: GetByIdForUpdateAsync ป้องกัน Race Condition ตอนฝาก/ถอน/โอน
```

```csharp
using Banking.Domain.Entities;
using Banking.Domain.Interfaces;
using Banking.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Banking.Infrastructure.Repositories;

public class AccountRepository : Repository<Account>, IAccountRepository
{
    public AccountRepository(AppDbContext context) : base(context) { }

    public async Task<Account?> GetByAccountNumberAsync(
        string accountNumber, CancellationToken ct = default)
    {
        return await _dbSet
            .FirstOrDefaultAsync(a => a.AccountNumber == accountNumber, ct);
    }

    public async Task<List<Account>> GetByUserIdAsync(
        Guid userId, CancellationToken ct = default)
    {
        return await _dbSet
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync(ct);
    }

    /// <summary>
    /// ⚠️ CRITICAL: ดึง Account พร้อม Row-Level Lock
    ///
    /// ปัญหาที่แก้ (Race Condition):
    ///   User A: อ่าน balance = 10,000 → ถอน 8,000 → balance = 2,000
    ///   User B: อ่าน balance = 10,000 → ถอน 8,000 → balance = 2,000
    ///   ผลลัพธ์: ถอนไป 16,000 แต่มีแค่ 10,000!
    ///
    /// วิธีแก้: SELECT ... FOR UPDATE
    ///   User A: Lock row → อ่าน 10,000 → ถอน 8,000 → balance = 2,000 → Unlock
    ///   User B: รอ... → Lock row → อ่าน 2,000 → ถอน 8,000 → ❌ เงินไม่พอ!
    ///
    /// FromSqlRaw: ใช้ raw SQL เพราะ EF Core ไม่มี built-in FOR UPDATE
    /// </summary>
    public async Task<Account?> GetByIdForUpdateAsync(
        Guid id, CancellationToken ct = default)
    {
        return await _dbSet
            .FromSqlRaw(
                "SELECT * FROM accounts WHERE \"Id\" = {0} AND \"IsDeleted\" = false FOR UPDATE",
                id)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<bool> AccountNumberExistsAsync(
        string accountNumber, CancellationToken ct = default)
    {
        return await _dbSet.AnyAsync(a => a.AccountNumber == accountNumber, ct);
    }
}
```

### 1.5 สร้าง TransactionRepository

```
📁 Banking.Infrastructure/Repositories/TransactionRepository.cs

ทำไม: method สำหรับดึงประวัติ + คำนวณยอดถอนวันนี้ (Daily Limit)
```

```csharp
using Banking.Domain.Entities;
using Banking.Domain.Enums;
using Banking.Domain.Interfaces;
using Banking.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Banking.Infrastructure.Repositories;

public class TransactionRepository : Repository<Transaction>, ITransactionRepository
{
    public TransactionRepository(AppDbContext context) : base(context) { }

    /// <summary>
    /// ดึงธุรกรรมแบบ Pagination — ใช้แสดง Statement
    /// Skip + Take = pagination จริง (ไม่โหลดทุก record)
    /// OrderByDescending(CreatedAt) = ล่าสุดขึ้นก่อน
    /// </summary>
    public async Task<List<Transaction>> GetByAccountIdAsync(
        Guid accountId, int page, int pageSize, CancellationToken ct = default)
    {
        return await _dbSet
            .Where(t => t.AccountId == accountId)
            .OrderByDescending(t => t.CreatedAt)
            .Skip((page - 1) * pageSize)   // ข้าม records ของหน้าก่อนๆ
            .Take(pageSize)                 // เอาแค่จำนวนที่ต้องการ
            .ToListAsync(ct);
    }

    /// <summary>
    /// นับจำนวนธุรกรรมทั้งหมด — ใช้คำนวณจำนวนหน้า
    /// CountAsync เร็วกว่าโหลดทั้งหมดแล้ว .Count
    /// </summary>
    public async Task<int> GetCountByAccountIdAsync(
        Guid accountId, CancellationToken ct = default)
    {
        return await _dbSet
            .CountAsync(t => t.AccountId == accountId, ct);
    }

    /// <summary>
    /// ⚠️ CRITICAL: คำนวณยอดถอนวันนี้ — ใช้เช็ค Daily Limit
    ///
    /// รวมเฉพาะ:
    /// - ประเภท Withdrawal และ TransferOut (เงินออก)
    /// - สถานะ Completed เท่านั้น (ไม่นับที่ Failed/Reversed)
    /// - เฉพาะวันนี้ (UTC)
    ///
    /// ถ้ายอดรวม + จำนวนที่ร้องขอ > DailyWithdrawalLimit → ปฏิเสธ
    /// </summary>
    public async Task<decimal> GetTodayWithdrawalTotalAsync(
        Guid accountId, CancellationToken ct = default)
    {
        var todayUtc = DateTime.UtcNow.Date;  // เที่ยงคืน UTC วันนี้

        return await _dbSet
            .Where(t => t.AccountId == accountId)
            .Where(t => t.Type == TransactionType.Withdrawal
                     || t.Type == TransactionType.TransferOut)
            .Where(t => t.Status == TransactionStatus.Completed)
            .Where(t => t.CreatedAt >= todayUtc)
            .SumAsync(t => t.Amount, ct);
    }
}
```

### 1.6 สร้าง UnitOfWork

```
📁 Banking.Infrastructure/Repositories/UnitOfWork.cs

ทำไม: รวม Repositories + จัดการ DB Transaction ไว้ที่เดียว
ทำให้ฝาก/ถอน/โอน เป็น Atomic (สำเร็จทั้งหมดหรือ rollback ทั้งหมด)
```

```csharp
using Banking.Domain.Interfaces;
using Banking.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Storage;

namespace Banking.Infrastructure.Repositories;

/// <summary>
/// UnitOfWork — รวม Repositories ทั้งหมด + จัดการ DB Transaction
///
/// ทำไมต้องมี:
/// การโอนเงินต้องทำ 4 อย่างพร้อมกัน:
///   1. หักเงินบัญชี A
///   2. เพิ่มเงินบัญชี B
///   3. สร้าง Transaction 2 รายการ
///   4. สร้าง Transfer 1 รายการ
///
/// ถ้า step 3 ล้มเหลว → ต้อง rollback step 1-2 ด้วย
/// UnitOfWork จัดการ BEGIN TRANSACTION ... COMMIT/ROLLBACK ให้
/// </summary>
public class UnitOfWork : IUnitOfWork, IDisposable
{
    private readonly AppDbContext _context;
    private IDbContextTransaction? _transaction;

    // Lazy initialization — สร้าง Repository เมื่อถูกเรียกใช้ครั้งแรก
    private IUserRepository? _users;
    private IAccountRepository? _accounts;
    private ITransactionRepository? _transactions;

    public UnitOfWork(AppDbContext context)
    {
        _context = context;
    }

    // ??= ถ้ายังเป็น null → สร้างใหม่, ถ้ามีแล้ว → ใช้ตัวเดิม
    public IUserRepository Users =>
        _users ??= new UserRepository(_context);

    public IAccountRepository Accounts =>
        _accounts ??= new AccountRepository(_context);

    public ITransactionRepository Transactions =>
        _transactions ??= new TransactionRepository(_context);

    public async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        return await _context.SaveChangesAsync(ct);
    }

    /// <summary>
    /// เริ่ม DB Transaction — ทุก operation หลังจากนี้อยู่ใน transaction เดียวกัน
    /// </summary>
    public async Task BeginTransactionAsync(CancellationToken ct = default)
    {
        _transaction = await _context.Database.BeginTransactionAsync(ct);
    }

    /// <summary>
    /// Commit — ยืนยันทุกอย่าง บันทึกถาวร
    /// </summary>
    public async Task CommitTransactionAsync(CancellationToken ct = default)
    {
        if (_transaction is not null)
        {
            await _transaction.CommitAsync(ct);
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    /// <summary>
    /// Rollback — ยกเลิกทุกอย่าง เหมือนไม่เคยเกิดขึ้น
    /// </summary>
    public async Task RollbackTransactionAsync(CancellationToken ct = default)
    {
        if (_transaction is not null)
        {
            await _transaction.RollbackAsync(ct);
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    public void Dispose()
    {
        _transaction?.Dispose();
        _context.Dispose();
    }
}
```

---

## ขั้นตอนที่ 2: สร้าง DTOs (Data Transfer Objects)

```
📁 Banking.Application/DTOs/

ทำไม: ไม่ส่ง Entity ตรงๆ ไป client (อันตราย! — อาจเผย password hash)
DTO = เลือกส่งเฉพาะ field ที่จำเป็น
```

```csharp
// Banking.Application/DTOs/TransactionDtos.cs

namespace Banking.Application.DTOs;

/// <summary>
/// Request ฝากเงิน — client ส่งมา
/// </summary>
public record DepositRequest(
    Guid AccountId,
    decimal Amount,
    string? Description
);

/// <summary>
/// Request ถอนเงิน — client ส่งมา
/// </summary>
public record WithdrawRequest(
    Guid AccountId,
    decimal Amount,
    string? Description
);

/// <summary>
/// Request โอนเงิน — client ส่งมา
/// </summary>
public record TransferRequest(
    Guid FromAccountId,
    Guid ToAccountId,
    decimal Amount,
    string? Description
);

/// <summary>
/// Response ธุรกรรม — ส่งกลับ client
/// ไม่มี PasswordHash, IsDeleted ฯลฯ
/// </summary>
public record TransactionResponse(
    Guid Id,
    string ReferenceNumber,
    string Type,
    decimal Amount,
    decimal BalanceBefore,
    decimal BalanceAfter,
    string Status,
    string? Description,
    DateTime CreatedAt
);

/// <summary>
/// Response บัญชี — ส่งกลับ client
/// </summary>
public record AccountResponse(
    Guid Id,
    string AccountNumber,
    string Type,
    string Currency,
    decimal Balance,
    decimal AvailableBalance,
    string Status,
    DateTime CreatedAt
);

/// <summary>
/// Response แบบมี pagination — สำหรับ list endpoints
/// </summary>
public record PagedResponse<T>(
    List<T> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages
);

/// <summary>
/// Response มาตรฐาน — ใช้กับทุก API
/// </summary>
public record ApiResponse<T>(
    bool Success,
    string Message,
    T? Data = default
);
```

---

## ขั้นตอนที่ 3: สร้าง Services (Business Logic)

### 3.1 Account Number Generator

```
📁 Banking.Application/Services/AccountNumberGenerator.cs

ทำไม: สร้างเลขบัญชีที่ไม่ซ้ำ + อ่านง่าย
```

```csharp
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
```

### 3.2 Reference Number Generator

```
📁 Banking.Application/Services/ReferenceNumberGenerator.cs

ทำไม: สร้างเลข reference ที่มนุษย์อ่านได้ + ไม่ซ้ำ
```

```csharp
namespace Banking.Application.Services;

/// <summary>
/// สร้าง Reference Number format: "TXN-20260329-XXXXXX"
/// ใช้วันที่ + random 6 หลัก
/// </summary>
public static class ReferenceNumberGenerator
{
    public static string Generate()
    {
        var date = DateTime.UtcNow.ToString("yyyyMMdd");
        var random = Random.Shared.Next(100000, 999999);
        return $"TXN-{date}-{random}";
    }
}
```

### 3.3 TransactionService — หัวใจของระบบ

```
📁 Banking.Application/Services/TransactionService.cs

ทำไม: Business Logic หลัก — ฝาก/ถอน/โอน ทั้งหมดอยู่ที่นี่
แยกออกจาก Controller เพื่อให้ test ได้ + reuse ได้
```

```csharp
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
```

---

## ขั้นตอนที่ 4: สร้าง Controllers

### 4.1 TransactionsController

```
📁 Banking.Api/Controllers/TransactionsController.cs

ทำไม: รับ HTTP request → เรียก Service → ส่ง response
Controller ไม่มี business logic — แค่เป็นตัวกลาง
```

```csharp
using Banking.Application.DTOs;
using Banking.Application.Services;
using Banking.Domain.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace Banking.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TransactionsController : ControllerBase
{
    private readonly TransactionService _transactionService;

    public TransactionsController(TransactionService transactionService)
    {
        _transactionService = transactionService;
    }

    /// <summary>
    /// ฝากเงิน
    /// POST /api/transactions/deposit
    /// </summary>
    [HttpPost("deposit")]
    public async Task<IActionResult> Deposit(
        [FromBody] DepositRequest request, CancellationToken ct)
    {
        try
        {
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
            var result = await _transactionService.DepositAsync(request, ipAddress, ct);

            return Ok(new ApiResponse<TransactionResponse>(
                Success: true,
                Message: $"Deposit of {request.Amount:N2} THB completed.",
                Data: result
            ));
        }
        catch (NotFoundException ex)
        {
            return NotFound(new ApiResponse<object>(false, ex.Message));
        }
        catch (AccountFrozenException ex)
        {
            return BadRequest(new ApiResponse<object>(false, ex.Message));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ApiResponse<object>(false, ex.Message));
        }
    }

    /// <summary>
    /// ถอนเงิน
    /// POST /api/transactions/withdraw
    /// </summary>
    [HttpPost("withdraw")]
    public async Task<IActionResult> Withdraw(
        [FromBody] WithdrawRequest request, CancellationToken ct)
    {
        try
        {
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
            var result = await _transactionService.WithdrawAsync(request, ipAddress, ct);

            return Ok(new ApiResponse<TransactionResponse>(
                Success: true,
                Message: $"Withdrawal of {request.Amount:N2} THB completed.",
                Data: result
            ));
        }
        catch (NotFoundException ex)
        {
            return NotFound(new ApiResponse<object>(false, ex.Message));
        }
        catch (InsufficientFundsException ex)
        {
            return BadRequest(new ApiResponse<object>(false, ex.Message));
        }
        catch (DailyLimitExceededException ex)
        {
            return BadRequest(new ApiResponse<object>(false, ex.Message));
        }
        catch (AccountFrozenException ex)
        {
            return BadRequest(new ApiResponse<object>(false, ex.Message));
        }
    }

    /// <summary>
    /// โอนเงิน
    /// POST /api/transactions/transfer
    /// </summary>
    [HttpPost("transfer")]
    public async Task<IActionResult> Transfer(
        [FromBody] TransferRequest request, CancellationToken ct)
    {
        try
        {
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
            var result = await _transactionService.TransferAsync(request, ipAddress, ct);

            return Ok(new ApiResponse<TransactionResponse>(
                Success: true,
                Message: $"Transfer of {request.Amount:N2} THB completed.",
                Data: result
            ));
        }
        catch (NotFoundException ex)
        {
            return NotFound(new ApiResponse<object>(false, ex.Message));
        }
        catch (InsufficientFundsException ex)
        {
            return BadRequest(new ApiResponse<object>(false, ex.Message));
        }
        catch (DailyLimitExceededException ex)
        {
            return BadRequest(new ApiResponse<object>(false, ex.Message));
        }
        catch (AccountFrozenException ex)
        {
            return BadRequest(new ApiResponse<object>(false, ex.Message));
        }
    }

    /// <summary>
    /// ดูประวัติธุรกรรม (Pagination)
    /// GET /api/transactions?accountId=xxx&page=1&pageSize=20
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetHistory(
        [FromQuery] Guid accountId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _transactionService.GetHistoryAsync(
            accountId, page, pageSize, ct);

        return Ok(new ApiResponse<PagedResponse<TransactionResponse>>(
            Success: true,
            Message: "Transaction history retrieved.",
            Data: result
        ));
    }
}
```

### 4.2 AccountsController

```
📁 Banking.Api/Controllers/AccountsController.cs
```

```csharp
using Banking.Application.DTOs;
using Banking.Application.Services;
using Banking.Domain.Enums;
using Banking.Domain.Exceptions;
using Banking.Domain.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Banking.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AccountsController : ControllerBase
{
    private readonly IUnitOfWork _unitOfWork;

    public AccountsController(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    /// <summary>
    /// ดูบัญชีทั้งหมดของ user
    /// GET /api/accounts?userId=xxx
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetByUserId(
        [FromQuery] Guid userId, CancellationToken ct)
    {
        var accounts = await _unitOfWork.Accounts.GetByUserIdAsync(userId, ct);

        var response = accounts.Select(a => new AccountResponse(
            Id: a.Id,
            AccountNumber: a.AccountNumber,
            Type: a.Type.ToString(),
            Currency: a.Currency,
            Balance: a.Balance,
            AvailableBalance: a.AvailableBalance,
            Status: a.Status.ToString(),
            CreatedAt: a.CreatedAt
        )).ToList();

        return Ok(new ApiResponse<List<AccountResponse>>(
            true, "Accounts retrieved.", response));
    }

    /// <summary>
    /// ดูรายละเอียดบัญชี
    /// GET /api/accounts/{id}
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var account = await _unitOfWork.Accounts.GetByIdAsync(id, ct);
        if (account is null)
            return NotFound(new ApiResponse<object>(false, "Account not found."));

        var response = new AccountResponse(
            Id: account.Id,
            AccountNumber: account.AccountNumber,
            Type: account.Type.ToString(),
            Currency: account.Currency,
            Balance: account.Balance,
            AvailableBalance: account.AvailableBalance,
            Status: account.Status.ToString(),
            CreatedAt: account.CreatedAt
        );

        return Ok(new ApiResponse<AccountResponse>(true, "Account retrieved.", response));
    }

    /// <summary>
    /// ดูยอดเงินคงเหลือ
    /// GET /api/accounts/{id}/balance
    /// </summary>
    [HttpGet("{id:guid}/balance")]
    public async Task<IActionResult> GetBalance(Guid id, CancellationToken ct)
    {
        var account = await _unitOfWork.Accounts.GetByIdAsync(id, ct);
        if (account is null)
            return NotFound(new ApiResponse<object>(false, "Account not found."));

        return Ok(new ApiResponse<object>(true, "Balance retrieved.", new
        {
            account.Balance,
            account.AvailableBalance,
            account.Currency
        }));
    }

    /// <summary>
    /// สร้างบัญชีใหม่
    /// POST /api/accounts
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateAccountRequest request, CancellationToken ct)
    {
        // สร้างเลขบัญชีที่ไม่ซ้ำ
        string accountNumber;
        do
        {
            accountNumber = AccountNumberGenerator.Generate();
        } while (await _unitOfWork.Accounts.AccountNumberExistsAsync(accountNumber, ct));

        var account = new Domain.Entities.Account
        {
            UserId = request.UserId,
            AccountNumber = accountNumber,
            Type = Enum.Parse<AccountType>(request.Type),
            Currency = request.Currency ?? "THB",
            Status = AccountStatus.Active
        };

        await _unitOfWork.Accounts.AddAsync(account, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Created($"/api/accounts/{account.Id}",
            new ApiResponse<AccountResponse>(true, "Account created.", new AccountResponse(
                account.Id, account.AccountNumber, account.Type.ToString(),
                account.Currency, account.Balance, account.AvailableBalance,
                account.Status.ToString(), account.CreatedAt
            )));
    }
}

public record CreateAccountRequest(
    Guid UserId,
    string Type = "Savings",
    string? Currency = "THB"
);
```

---

## ขั้นตอนที่ 5: ลงทะเบียน Services ใน Program.cs

```
📁 เพิ่มใน Banking.Api/Program.cs (ก่อน var app = builder.Build())

ทำไม: ASP.NET Core ใช้ Dependency Injection
ต้องบอกว่า "เมื่อมีคนขอ IUnitOfWork → สร้าง UnitOfWork ให้"
```

```csharp
// === เพิ่มตรงนี้ใน Program.cs ก่อน builder.Build() ===

// Repositories + UnitOfWork
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

// Services
builder.Services.AddScoped<TransactionService>();
```

```
AddScoped คืออะไร:
- Scoped = สร้างใหม่ทุก HTTP request
- ทุก request ได้ UnitOfWork ใหม่ (DbContext ใหม่)
- request จบ → Dispose อัตโนมัติ

ทำไมไม่ใช้ Singleton:
- DbContext ไม่ thread-safe → ต้องสร้างใหม่ทุก request
- Singleton = ใช้ตัวเดียวทุก request → ชน!
```

---

## ขั้นตอนที่ 6: ทดสอบ API

### 6.1 Build + Run

```bash
cd BankingSystem
dotnet build                    # ต้อง 0 errors
dotnet run --project Banking.Api

# เปิด browser: https://localhost:xxxx/swagger
# จะเห็น endpoints ทั้งหมดใน Swagger UI
```

### 6.2 ทดสอบใน Swagger UI

```
1. สร้างบัญชี:
   POST /api/accounts
   Body: { "userId": "xxx-demo-user-id-xxx", "type": "Savings" }
   → ได้ accountId กลับมา

2. ฝากเงิน:
   POST /api/transactions/deposit
   Body: { "accountId": "xxx", "amount": 10000, "description": "ฝากเงินสด" }
   → ดู response: balanceBefore=0, balanceAfter=10000

3. ถอนเงิน:
   POST /api/transactions/withdraw
   Body: { "accountId": "xxx", "amount": 3000 }
   → ดู response: balanceBefore=10000, balanceAfter=7000

4. ถอนเกิน:
   POST /api/transactions/withdraw
   Body: { "accountId": "xxx", "amount": 99999 }
   → 400 Bad Request: "Insufficient funds"

5. โอนเงิน:
   POST /api/transactions/transfer
   Body: { "fromAccountId": "xxx", "toAccountId": "yyy", "amount": 2000 }
   → ดู response: balanceBefore=7000, balanceAfter=5000

6. ดูประวัติ:
   GET /api/transactions?accountId=xxx&page=1&pageSize=10
   → เห็น 3 transactions (deposit, withdraw, transfer)
```

---

## ขั้นตอนที่ 7: Exception Handling Middleware

```
📁 Banking.Api/Middleware/ExceptionMiddleware.cs

ทำไม: Controllers ทุกตัวมี try-catch ซ้ำกัน → ย้าย error handling มาอยู่ที่เดียว
ข้อดี:
1. Controllers สะอาดขึ้น (ไม่ต้อง try-catch ซ้ำ)
2. Error response format เดียวกันทุก endpoint
3. ไม่ leak stack trace ใน Production
```

```csharp
// Banking.Api/Middleware/ExceptionMiddleware.cs

using Banking.Domain.Exceptions;
using System.Net;
using System.Text.Json;

namespace Banking.Api.Middleware;

/// <summary>
/// Global Exception Handler — จับ Exception ทุกตัวที���ไม่ถูก catch ใน Controller
///
/// Flow:
///   Request → Middleware → Controller → (Exception thrown) → Middleware catches → Error Response
/// </summary>
public class ExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionMiddleware> _logger;

    public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var (statusCode, message) = exception switch
        {
            NotFoundException ex         => (HttpStatusCode.NotFound, ex.Message),
            InsufficientFundsException ex => (HttpStatusCode.BadRequest, ex.Message),
            DailyLimitExceededException ex => (HttpStatusCode.BadRequest, ex.Message),
            AccountFrozenException ex    => (HttpStatusCode.Forbidden, ex.Message),
            AccountLockedException ex    => (HttpStatusCode.Forbidden, ex.Message),
            DuplicateException ex        => (HttpStatusCode.Conflict, ex.Message),
            ArgumentException ex         => (HttpStatusCode.BadRequest, ex.Message),
            _ => (HttpStatusCode.InternalServerError, "An unexpected error occurred.")
        };

        if (statusCode == HttpStatusCode.InternalServerError)
            _logger.LogError(exception, "Unhandled exception: {Message}", exception.Message);
        else
            _logger.LogWarning("Handled exception: {Type} — {Message}",
                exception.GetType().Name, exception.Message);

        context.Response.ContentType = "application/problem+json";
        context.Response.StatusCode = (int)statusCode;

        var response = new
        {
            success = false,
            message = message,
            statusCode = (int)statusCode
        };

        await context.Response.WriteAsync(
            JsonSerializer.Serialize(response, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            }));
    }
}
```

### 7.1 ลงทะเบียน Middleware ใน Program.cs

```csharp
// เพิ่มใน Program.cs — ก่อน app.UseHttpsRedirection()

app.UseMiddleware<ExceptionMiddleware>();
app.UseHttpsRedirection();
```

### 7.2 ปรับ Controllers — ลบ try-catch ออก

```
เมื่อมี ExceptionMiddleware แล้ว Controllers ไม่ต้อง try-catch เอง
```

**ก่อน (มี try-catch):**
```csharp
[HttpPost("deposit")]
public async Task<IActionResult> Deposit([FromBody] DepositRequest request, CancellationToken ct)
{
    try
    {
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        var result = await _transactionService.DepositAsync(request, ipAddress, ct);
        return Ok(new ApiResponse<TransactionResponse>(true, "Deposit completed.", result));
    }
    catch (NotFoundException ex)    { return NotFound(...); }
    catch (AccountFrozenException ex) { return BadRequest(...); }
    catch (ArgumentException ex)     { return BadRequest(...); }
}
```

**หลัง (สะอาด):**
```csharp
[HttpPost("deposit")]
public async Task<IActionResult> Deposit([FromBody] DepositRequest request, CancellationToken ct)
{
    var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
    var result = await _transactionService.DepositAsync(request, ipAddress, ct);
    return Ok(new ApiResponse<TransactionResponse>(true, "Deposit completed.", result));
}
```

---

## ขั้นตอนที่ 8: FluentValidation

```
📁 ที่ต้องสร้าง:
  Banking.Application/Validators/DepositRequestValidator.cs
  Banking.Application/Validators/WithdrawRequestValidator.cs
  Banking.Application/Validators/TransferRequestValidator.cs

ทำไม: ตรวจสอบ input ก่อนถึง business logic
FluentValidation อ่านง่ายกว่า if-else + reuse ได้
```

### 8.1 Validators

```csharp
// Banking.Application/Validators/DepositRequestValidator.cs

using Banking.Application.DTOs;
using FluentValidation;

namespace Banking.Application.Validators;

public class DepositRequestValidator : AbstractValidator<DepositRequest>
{
    public DepositRequestValidator()
    {
        RuleFor(x => x.AccountId)
            .NotEmpty().WithMessage("Account ID is required.");

        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("Amount must be greater than 0.")
            .LessThanOrEqualTo(1_000_000).WithMessage("Amount cannot exceed 1,000,000 per transaction.")
            .PrecisionScale(18, 2, true).WithMessage("Amount must have at most 2 decimal places.");

        RuleFor(x => x.Description)
            .MaximumLength(500)
            .When(x => x.Description is not null);
    }
}
```

```csharp
// Banking.Application/Validators/WithdrawRequestValidator.cs

using Banking.Application.DTOs;
using FluentValidation;

namespace Banking.Application.Validators;

public class WithdrawRequestValidator : AbstractValidator<WithdrawRequest>
{
    public WithdrawRequestValidator()
    {
        RuleFor(x => x.AccountId)
            .NotEmpty().WithMessage("Account ID is required.");

        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("Amount must be greater than 0.")
            .LessThanOrEqualTo(1_000_000).WithMessage("Amount cannot exceed 1,000,000 per transaction.")
            .PrecisionScale(18, 2, true).WithMessage("Amount must have at most 2 decimal places.");

        RuleFor(x => x.Description)
            .MaximumLength(500)
            .When(x => x.Description is not null);
    }
}
```

```csharp
// Banking.Application/Validators/TransferRequestValidator.cs

using Banking.Application.DTOs;
using FluentValidation;

namespace Banking.Application.Validators;

public class TransferRequestValidator : AbstractValidator<TransferRequest>
{
    public TransferRequestValidator()
    {
        RuleFor(x => x.FromAccountId)
            .NotEmpty().WithMessage("Source account ID is required.");

        RuleFor(x => x.ToAccountId)
            .NotEmpty().WithMessage("Destination account ID is required.");

        RuleFor(x => x)
            .Must(x => x.FromAccountId != x.ToAccountId)
            .WithMessage("Cannot transfer to the same account.");

        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("Amount must be greater than 0.")
            .LessThanOrEqualTo(1_000_000).WithMessage("Amount cannot exceed 1,000,000 per transaction.")
            .PrecisionScale(18, 2, true).WithMessage("Amount must have at most 2 decimal places.");

        RuleFor(x => x.Description)
            .MaximumLength(500)
            .When(x => x.Description is not null);
    }
}
```

### 8.2 ใช้ Validator ใน Controller

```csharp
// ตัวอย่างใช้ใน TransactionsController

using FluentValidation;

[ApiController]
[Route("api/[controller]")]
public class TransactionsController : ControllerBase
{
    private readonly TransactionService _transactionService;
    private readonly IValidator<DepositRequest> _depositValidator;

    public TransactionsController(
        TransactionService transactionService,
        IValidator<DepositRequest> depositValidator)
    {
        _transactionService = transactionService;
        _depositValidator = depositValidator;
    }

    [HttpPost("deposit")]
    public async Task<IActionResult> Deposit(
        [FromBody] DepositRequest request, CancellationToken ct)
    {
        var validation = await _depositValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return BadRequest(new ApiResponse<object>(false,
                string.Join("; ", validation.Errors.Select(e => e.ErrorMessage))));

        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        var result = await _transactionService.DepositAsync(request, ipAddress, ct);
        return Ok(new ApiResponse<TransactionResponse>(
            true, $"Deposit of {request.Amount:N2} THB completed.", result));
    }
}
```

### 8.3 ลงทะเบียน Validators ใน Program.cs

```csharp
using FluentValidation;

// สแกนหา Validator ทั้งหมดใน Assembly ของ Banking.Application
builder.Services.AddValidatorsFromAssemblyContaining<Banking.Application.Validators.DepositRequestValidator>();
```

---

## ขั้นตอนที่ 9: Authentication — JWT + Login/Register

```
📁 ที่ต้องสร้าง:
  Banking.Application/DTOs/AuthDtos.cs
  Banking.Application/Services/IJwtService.cs
  Banking.Application/Services/AuthService.cs
  Banking.Application/Validators/RegisterRequestValidator.cs
  Banking.Application/Validators/LoginRequestValidator.cs
  Banking.Infrastructure/Services/JwtService.cs
  Banking.Api/Controllers/AuthController.cs

ทำไม: ทุก endpoint ต้องรู้ว่า "ใครเป็นคนขอ"
ถ้าไม่มี Auth → ใครก็ถอนเงินได้ ดูบัญชีคนอื่นได้!

JWT (JSON Web Token):
- Token ที่ server สร้างหลัง login สำเร็จ
- Client ส่ง token มาใน Header ทุก request
- Server ตรวจ token → รู้ว่าเป็น user ไหน + สิทธิ์อะไร
- ไม่ต้องเก็บ session ใน server (stateless) → scale ได้ง่าย
```

### 9.1 Auth DTOs

```csharp
// Banking.Application/DTOs/AuthDtos.cs

namespace Banking.Application.DTOs;

public record RegisterRequest(
    string FirstName,
    string LastName,
    string Email,
    string Phone,
    string Password,
    string ConfirmPassword
);

public record LoginRequest(
    string Email,
    string Password
);

public record RefreshTokenRequest(
    string RefreshToken
);

public record AuthResponse(
    Guid UserId,
    string FullName,
    string Email,
    string AccessToken,
    string RefreshToken,
    DateTime AccessTokenExpiry
);

public record UserProfileResponse(
    Guid Id,
    string FirstName,
    string LastName,
    string Email,
    string Phone,
    string KycStatus,
    DateTime CreatedAt
);
```

### 9.2 JWT Service Interface + Implementation

```csharp
// Banking.Application/Services/IJwtService.cs

using Banking.Domain.Entities;

namespace Banking.Application.Services;

/// <summary>
/// JWT Service Interface — อยู่ใน Application layer
/// Business Logic ต้องรู้ว่ามี JWT service แต่ไม่ต้องรู้ implementation detail
/// </summary>
public interface IJwtService
{
    /// <summary>
    /// สร้าง Access Token (JWT) — อายุสั้น 15 นาที
    /// </summary>
    string GenerateAccessToken(User user);

    /// <summary>
    /// สร้าง Refresh Token — อายุยาว 7 วัน
    /// เป็น random string ไม่ใช่ JWT
    /// </summary>
    string GenerateRefreshToken();

    /// <summary>
    /// ดึง User ID จาก expired token — ใช้ตอน refresh
    /// </summary>
    Guid? GetUserIdFromExpiredToken(string token);
}
```

```csharp
// Banking.Infrastructure/Services/JwtService.cs

using Banking.Application.Services;
using Banking.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace Banking.Infrastructure.Services;

/// <summary>
/// JWT Service Implementation — อยู่ใน Infrastructure layer
///
/// Access Token Flow:
///   Login → GenerateAccessToken → Client เก็บไว้ → ส่งมาทุก request
///   → Server ตรวจ → ถ้าหมดอายุ → Client ใช้ Refresh Token ขอใหม่
/// </summary>
public class JwtService : IJwtService
{
    private readonly IConfiguration _config;

    public JwtService(IConfiguration config)
    {
        _config = config;
    }

    public string GenerateAccessToken(User user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Name, user.FullName),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(
                int.Parse(_config["Jwt:AccessTokenExpirationMinutes"] ?? "15")),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string GenerateRefreshToken()
    {
        var randomNumber = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomNumber);
        return Convert.ToBase64String(randomNumber);
    }

    public Guid? GetUserIdFromExpiredToken(string token)
    {
        var tokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = false,  // อนุญาตให้ token หมดอายุ (กำลัง refresh)
            ValidateIssuerSigningKey = true,
            ValidIssuer = _config["Jwt:Issuer"],
            ValidAudience = _config["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(_config["Jwt:Key"]!))
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var principal = tokenHandler.ValidateToken(token, tokenValidationParameters, out _);
        var userIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return userIdClaim is not null ? Guid.Parse(userIdClaim) : null;
    }
}
```

### 9.3 Auth Service (Business Logic)

```csharp
// Banking.Application/Services/AuthService.cs

using Banking.Application.DTOs;
using Banking.Application.Services;
using Banking.Domain.Entities;
using Banking.Domain.Enums;
using Banking.Domain.Exceptions;
using Banking.Domain.Interfaces;

namespace Banking.Application.Services;

public class AuthService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IJwtService _jwtService;

    public AuthService(IUnitOfWork unitOfWork, IJwtService jwtService)
    {
        _unitOfWork = unitOfWork;
        _jwtService = jwtService;
    }

    /// <summary>
    /// สมัครสมาชิก + สร้างบัญชีออมทรัพย์เริ่มต้น
    ///
    /// Flow:
    /// 1. Validate: email/phone ไม่ซ้ำ, password match
    /// 2. Hash password ด้วย BCrypt
    /// 3. สร้าง User (KycStatus = Pending)
    /// 4. สร้าง default Savings Account
    /// 5. Generate JWT + Refresh Token
    /// </summary>
    public async Task<AuthResponse> RegisterAsync(
        RegisterRequest request, CancellationToken ct = default)
    {
        if (request.Password != request.ConfirmPassword)
            throw new ArgumentException("Passwords do not match.");

        if (await _unitOfWork.Users.EmailExistsAsync(request.Email, ct))
            throw new DuplicateException("Email already registered.");

        if (await _unitOfWork.Users.PhoneExistsAsync(request.Phone, ct))
            throw new DuplicateException("Phone number already registered.");

        var passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);

        var user = new User
        {
            FirstName = request.FirstName,
            LastName = request.LastName,
            Email = request.Email.ToLower().Trim(),
            Phone = request.Phone.Trim(),
            PasswordHash = passwordHash,
            KycStatus = KycStatus.Pending,
            IsActive = true
        };

        await _unitOfWork.Users.AddAsync(user, ct);

        // สร้าง default Savings Account
        string accountNumber;
        do
        {
            accountNumber = AccountNumberGenerator.Generate();
        } while (await _unitOfWork.Accounts.AccountNumberExistsAsync(accountNumber, ct));

        var account = new Account
        {
            UserId = user.Id,
            AccountNumber = accountNumber,
            Type = AccountType.Savings,
            Currency = "THB",
            Status = AccountStatus.Active
        };

        await _unitOfWork.Accounts.AddAsync(account, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        var accessToken = _jwtService.GenerateAccessToken(user);
        var refreshToken = _jwtService.GenerateRefreshToken();

        // TODO: เก็บ refreshToken ใน Redis (Phase 3)

        return new AuthResponse(
            UserId: user.Id,
            FullName: user.FullName,
            Email: user.Email,
            AccessToken: accessToken,
            RefreshToken: refreshToken,
            AccessTokenExpiry: DateTime.UtcNow.AddMinutes(15)
        );
    }

    /// <summary>
    /// Login ด้วย email + password
    ///
    /// Flow:
    /// 1. Find user by email
    /// 2. Check: ถูก lock ไหม?
    /// 3. Verify password (BCrypt)
    /// 4. ถ้าผิด → เพิ่ม FailedLoginAttempts (ถ้าครบ 5 → lock)
    /// 5. ถ้าถูก → reset counter + generate tokens
    ///
    /// ⚠️ Security: ข้อความ error ต้องกว้างๆ
    ///   ✅ "Invalid email or password." — ไม่บอกว่าอีเมลมีอยู่ไหม
    ///   ❌ "Email not found." → attacker รู้ว่าอีเมลไม่มี
    /// </summary>
    public async Task<AuthResponse> LoginAsync(
        LoginRequest request, CancellationToken ct = default)
    {
        var user = await _unitOfWork.Users.GetByEmailAsync(
            request.Email.ToLower().Trim(), ct);

        if (user is null)
            throw new ArgumentException("Invalid email or password.");

        if (user.IsLocked)
            throw new AccountLockedException();

        if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            user.FailedLoginAttempts++;
            if (user.FailedLoginAttempts >= 5)
                user.IsLocked = true;

            _unitOfWork.Users.Update(user);
            await _unitOfWork.SaveChangesAsync(ct);

            throw new ArgumentException("Invalid email or password.");
        }

        // Login สำเร็จ → reset counter
        user.FailedLoginAttempts = 0;
        user.LastLoginAt = DateTime.UtcNow;
        _unitOfWork.Users.Update(user);
        await _unitOfWork.SaveChangesAsync(ct);

        var accessToken = _jwtService.GenerateAccessToken(user);
        var refreshToken = _jwtService.GenerateRefreshToken();

        return new AuthResponse(
            UserId: user.Id,
            FullName: user.FullName,
            Email: user.Email,
            AccessToken: accessToken,
            RefreshToken: refreshToken,
            AccessTokenExpiry: DateTime.UtcNow.AddMinutes(15)
        );
    }

    public async Task<UserProfileResponse> GetProfileAsync(
        Guid userId, CancellationToken ct = default)
    {
        var user = await _unitOfWork.Users.GetByIdAsync(userId, ct)
            ?? throw new NotFoundException("User", userId);

        return new UserProfileResponse(
            Id: user.Id,
            FirstName: user.FirstName,
            LastName: user.LastName,
            Email: user.Email,
            Phone: user.Phone,
            KycStatus: user.KycStatus.ToString(),
            CreatedAt: user.CreatedAt
        );
    }
}
```

### 9.4 Auth Validators

```csharp
// Banking.Application/Validators/RegisterRequestValidator.cs

using Banking.Application.DTOs;
using FluentValidation;

namespace Banking.Application.Validators;

public class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("First name is required.")
            .MaximumLength(100);

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("Last name is required.")
            .MaximumLength(100);

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("Invalid email format.")
            .MaximumLength(255);

        RuleFor(x => x.Phone)
            .NotEmpty().WithMessage("Phone is required.")
            .Matches(@"^0\d{8,9}$").WithMessage("Invalid Thai phone number format.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.")
            .MinimumLength(8).WithMessage("Password must be at least 8 characters.")
            .Matches(@"[A-Z]").WithMessage("Must contain at least one uppercase letter.")
            .Matches(@"[a-z]").WithMessage("Must contain at least one lowercase letter.")
            .Matches(@"\d").WithMessage("Must contain at least one digit.");

        RuleFor(x => x.ConfirmPassword)
            .Equal(x => x.Password).WithMessage("Passwords do not match.");
    }
}
```

```csharp
// Banking.Application/Validators/LoginRequestValidator.cs

using Banking.Application.DTOs;
using FluentValidation;

namespace Banking.Application.Validators;

public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("Invalid email format.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.");
    }
}
```

### 9.5 Auth Controller

```csharp
// Banking.Api/Controllers/AuthController.cs

using Banking.Application.DTOs;
using Banking.Application.Services;
using Banking.Application.DTOs;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Banking.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AuthService _authService;
    private readonly IValidator<RegisterRequest> _registerValidator;
    private readonly IValidator<LoginRequest> _loginValidator;

    public AuthController(
        AuthService authService,
        IValidator<RegisterRequest> registerValidator,
        IValidator<LoginRequest> loginValidator)
    {
        _authService = authService;
        _registerValidator = registerValidator;
        _loginValidator = loginValidator;
    }

    /// <summary>
    /// สมัครสมาชิก — POST /api/auth/register
    /// </summary>
    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register(
        [FromBody] RegisterRequest request, CancellationToken ct)
    {
        var validation = await _registerValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return BadRequest(new ApiResponse<object>(false,
                string.Join("; ", validation.Errors.Select(e => e.ErrorMessage))));

        var result = await _authService.RegisterAsync(request, ct);
        return Created($"/api/auth/profile",
            new ApiResponse<AuthResponse>(true, "Registration successful.", result));
    }

    /// <summary>
    /// เข้าสู่ระบบ — POST /api/auth/login
    /// </summary>
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login(
        [FromBody] LoginRequest request, CancellationToken ct)
    {
        var validation = await _loginValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return BadRequest(new ApiResponse<object>(false,
                string.Join("; ", validation.Errors.Select(e => e.ErrorMessage))));

        var result = await _authService.LoginAsync(request, ct);
        return Ok(new ApiResponse<AuthResponse>(true, "Login successful.", result));
    }

    /// <summary>
    /// ดูโปรไฟล์ (ต้อง login) — GET /api/auth/profile
    /// User.FindFirst(ClaimTypes.NameIdentifier) ดึง userId จาก JWT token
    /// </summary>
    [HttpGet("profile")]
    [Authorize]
    public async Task<IActionResult> Profile(CancellationToken ct)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userIdClaim is null || !Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized(new ApiResponse<object>(false, "Invalid token."));

        var result = await _authService.GetProfileAsync(userId, ct);
        return Ok(new ApiResponse<UserProfileResponse>(true, "Profile retrieved.", result));
    }

    /// <summary>
    /// ออกจากระบบ — POST /api/auth/logout
    /// Placeholder สำหรับ Phase 3 (Redis token blacklist)
    /// </summary>
    [HttpPost("logout")]
    [Authorize]
    public IActionResult Logout()
    {
        // TODO Phase 3: เพิ่ม JTI ลง Redis blacklist
        return Ok(new ApiResponse<object>(true, "Logged out successfully."));
    }
}
```

### 9.6 ตั้งค่า JWT ใน appsettings.json

```json
{
  "Jwt": {
    "Key": "YourSuperSecretKeyAtLeast32CharactersLong!@#$",
    "Issuer": "banking-api",
    "Audience": "banking-frontend",
    "AccessTokenExpirationMinutes": 15,
    "RefreshTokenExpirationDays": 7
  }
}
```

```
⚠️ Key ต้องยาวอย่างน้อย 32 ตัวอักษร (256 bits สำหรับ HMAC-SHA256)
Production: ใช้ Environment Variable แทน appsettings (ห้าม commit ลง git)
```

### 9.7 ลงทะเบียน JWT + Auth Services ใน Program.cs

```csharp
using Banking.Application.Services;
using Banking.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

// === JWT Authentication ===
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!)),
            ClockSkew = TimeSpan.Zero  // ไม่อนุญาต buffer time (default 5 นาที)
        };
    });

// === Auth Services ===
builder.Services.AddScoped<IJwtService, JwtService>();
builder.Services.AddScoped<AuthService>();
```

```
⚠️ ลำดับ Middleware สำคัญ (ใน Program.cs):

app.UseMiddleware<ExceptionMiddleware>(); // 1. จับ error
app.UseHttpsRedirection();                // 2. HTTP → HTTPS
app.UseAuthentication();                  // 3. ตรวจ JWT → "คุณเป็นใคร?"
app.UseAuthorization();                   // 4. ตรวจสิทธิ์ → "คุณมีสิทธิ์��หม?"
app.MapControllers();                     // 5. Route → Controller

ทำไม Authentication ต้องอยู่ก่อน Authorization:
  UseAuthentication → decode JWT → ได้ userId
  UseAuthorization  → ดู role/policy
  ถ้าสลับ → Authorization ไม่รู้ว่าเป็นใคร → reject ทุก request
```

---

## ขั้นตอนที่ 10: Admin Endpoints

```
📁 Banking.Api/Controllers/AdminController.cs

ทำไม: ระบบธนาคารต้องมี admin ดูสถิติ + จัดการบัญชีผิดปกติ
Phase ถัดไปจะเพิ่ม Role-based: [Authorize(Roles = "Admin")]
```

```csharp
// Banking.Api/Controllers/AdminController.cs

using Banking.Application.DTOs;
using Banking.Domain.Enums;
using Banking.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Banking.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AdminController : ControllerBase
{
    private readonly IUnitOfWork _unitOfWork;

    public AdminController(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    /// <summary>
    /// Dashboard สถิติระบบ — GET /api/admin/dashboard
    /// </summary>
    [HttpGet("dashboard")]
    public async Task<IActionResult> Dashboard(CancellationToken ct)
    {
        var users = await _unitOfWork.Users.GetAllAsync(ct);
        var accounts = await _unitOfWork.Accounts.GetAllAsync(ct);

        var dashboard = new
        {
            TotalUsers = users.Count,
            TotalAccounts = accounts.Count,
            ActiveAccounts = accounts.Count(a => a.Status == AccountStatus.Active),
            FrozenAccounts = accounts.Count(a => a.Status == AccountStatus.Frozen),
            TotalBalance = accounts.Sum(a => a.Balance),
            LockedUsers = users.Count(u => u.IsLocked)
        };

        return Ok(new ApiResponse<object>(true, "Dashboard data retrieved.", dashboard));
    }

    /// <summary>
    /// อายัดบัญชี — POST /api/admin/accounts/{id}/freeze
    /// </summary>
    [HttpPost("accounts/{id:guid}/freeze")]
    public async Task<IActionResult> FreezeAccount(Guid id, CancellationToken ct)
    {
        var account = await _unitOfWork.Accounts.GetByIdAsync(id, ct);
        if (account is null)
            return NotFound(new ApiResponse<object>(false, "Account not found."));

        if (account.Status == AccountStatus.Frozen)
            return BadRequest(new ApiResponse<object>(false, "Account is already frozen."));

        account.Status = AccountStatus.Frozen;
        _unitOfWork.Accounts.Update(account);
        await _unitOfWork.SaveChangesAsync(ct);

        return Ok(new ApiResponse<object>(true,
            $"Account {account.AccountNumber} has been frozen."));
    }

    /// <summary>
    /// ปลดอายัด — POST /api/admin/accounts/{id}/unfreeze
    /// </summary>
    [HttpPost("accounts/{id:guid}/unfreeze")]
    public async Task<IActionResult> UnfreezeAccount(Guid id, CancellationToken ct)
    {
        var account = await _unitOfWork.Accounts.GetByIdAsync(id, ct);
        if (account is null)
            return NotFound(new ApiResponse<object>(false, "Account not found."));

        if (account.Status != AccountStatus.Frozen)
            return BadRequest(new ApiResponse<object>(false, "Account is not frozen."));

        account.Status = AccountStatus.Active;
        _unitOfWork.Accounts.Update(account);
        await _unitOfWork.SaveChangesAsync(ct);

        return Ok(new ApiResponse<object>(true,
            $"Account {account.AccountNumber} has been unfrozen."));
    }

    /// <summary>
    /// ปลด lock user — POST /api/admin/users/{id}/unlock
    /// </summary>
    [HttpPost("users/{id:guid}/unlock")]
    public async Task<IActionResult> UnlockUser(Guid id, CancellationToken ct)
    {
        var user = await _unitOfWork.Users.GetByIdAsync(id, ct);
        if (user is null)
            return NotFound(new ApiResponse<object>(false, "User not found."));

        if (!user.IsLocked)
            return BadRequest(new ApiResponse<object>(false, "User is not locked."));

        user.IsLocked = false;
        user.FailedLoginAttempts = 0;
        _unitOfWork.Users.Update(user);
        await _unitOfWork.SaveChangesAsync(ct);

        return Ok(new ApiResponse<object>(true,
            $"User {user.FullName} has been unlocked."));
    }
}
```

---

## ขั้นตอนที่ 11: แก้ไขโครงสร้างไฟล์ — ย้าย Controllers ไปที่ถูกต้อง

```
⚠️ ปัญหาปัจจุบัน:
  Controllers อยู่ใน Banking.Application/Controllers/
  แต่ควรอยู่ใน Banking.Api/Controllers/

ทำไมต้องย้าย:
  Application layer = business logic (Services, DTOs, Validators)
  Api layer = HTTP concerns (Controllers, Middleware, Filters)
  Controller ใช้ HttpContext, [ApiController], IActionResult — เป็น HTTP concerns
```

### 11.1 ขั้นตอนย้าย

```bash
# 1. ย้าย Controller จาก Application → Api
mv BankingSystem/Banking.Application/Controllers/TransactionsController.cs \
   BankingSystem/Banking.Api/Controllers/
mv BankingSystem/Banking.Application/Controllers/AccountsController.cs \
   BankingSystem/Banking.Api/Controllers/

# 2. ลบ folder Controllers ที่ว่างเปล่าใน Application
rmdir BankingSystem/Banking.Application/Controllers

# 3. ลบ placeholder files
rm BankingSystem/Banking.Api/Controllers/WeatherForecastController.cs
rm BankingSystem/Banking.Application/Class1.cs
```

### 11.2 แก้ Banking.Application.csproj

```xml
<!-- ก่อน: มี MVC reference ที่ไม่จำเป็น -->
<ItemGroup>
  <PackageReference Include="FluentValidation" Version="12.1.1" />
  <PackageReference Include="MediatR" Version="14.1.0" />
  <PackageReference Include="Microsoft.AspNetCore.Mvc.Core" Version="2.3.9" />  <!-- ลบบรรทัดนี้ -->
  <PackageReference Include="Microsoft.EntityFrameworkCore" Version="10.0.5" />
</ItemGroup>

<!-- หลัง: -->
<ItemGroup>
  <PackageReference Include="FluentValidation" Version="12.1.1" />
  <PackageReference Include="MediatR" Version="14.1.0" />
  <PackageReference Include="Microsoft.EntityFrameworkCore" Version="10.0.5" />
</ItemGroup>
```

```
namespace ไม่ต้องแก้ เพราะ Controllers ใช้ Banking.Api.Controllers อยู่แล้ว
แค่ย้ายไฟล์ไป Banking.Api/Controllers/ ก็พอ
```

---

## ขั้นตอนที่ 12: Program.cs ฉบับสมบูรณ์

```
รวมทุกอย่างจากขั้นตอนที่ 5, 7, 8, 9
```

```csharp
// Banking.Api/Program.cs — Phase 2 สมบูรณ์

using Banking.Application.Services;
using Banking.Application.Services;
using Banking.Domain.Interfaces;
using Banking.Infrastructure.Data;
using Banking.Infrastructure.Repositories;
using Banking.Infrastructure.Services;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// ===== Database =====
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        npgsqlOptions =>
        {
            npgsqlOptions.MigrationsAssembly("Banking.Infrastructure");
            npgsqlOptions.EnableRetryOnFailure(3);
            npgsqlOptions.CommandTimeout(30);
        }));

// ===== JWT Authentication =====
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!)),
            ClockSkew = TimeSpan.Zero
        };
    });

// ===== Repositories + UnitOfWork =====
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

// ===== Application Services =====
builder.Services.AddScoped<TransactionService>();
builder.Services.AddScoped<IJwtService, JwtService>();
builder.Services.AddScoped<AuthService>();

// ===== FluentValidation =====
builder.Services.AddValidatorsFromAssemblyContaining<Banking.Application.Validators.DepositRequestValidator>();

// ===== ASP.NET Core =====
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// ===== Auto Migration & Seed (Debug/Dev only) =====
var env = app.Environment;
if (env.EnvironmentName == "Debug" || env.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await context.Database.MigrateAsync();
    await Banking.Infrastructure.Seeds.DataSeeder.SeedAsync(context);
}

// ===== Swagger =====
var swaggerEnabled = app.Configuration.GetValue<bool>("Swagger:Enabled");
if (swaggerEnabled)
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// ===== Middleware Pipeline (ลำดับสำคัญ!) =====
app.UseMiddleware<Banking.Api.Middleware.ExceptionMiddleware>();
app.UseHttpsRedirection();
app.UseAuthentication();  // ตรวจ JWT token
app.UseAuthorization();   // ตรวจสิทธิ์ [Authorize]
app.MapControllers();

app.Run();
```

---

## Checklist — สิ่งที่ต้องเสร็จก่อนไป Phase 3

```
Repository + UnitOfWork:
☐ Repository.cs (Generic) สร้างแล้ว
☐ UserRepository.cs สร้างแล้ว
☐ AccountRepository.cs + GetByIdForUpdateAsync (Row Lock) สร้างแล้ว
☐ TransactionRepository.cs + GetTodayWithdrawalTotalAsync สร้างแล้ว
☐ UnitOfWork.cs สร้างแล้ว + จัดการ DB Transaction ได้

DTOs + Services:
☐ TransactionDtos.cs: Request + Response records
☐ AuthDtos.cs: Register/Login/Auth Response
☐ TransactionService: Deposit, Withdraw, Transfer logic ครบ
☐ AuthService: Register, Login, Profile ครบ
☐ JwtService: GenerateAccessToken, GenerateRefreshToken ครบ

Validators:
☐ DepositRequestValidator
☐ WithdrawRequestValidator
☐ TransferRequestValidator
☐ RegisterRequestValidator
☐ LoginRequestValidator

Controllers (อยู่ใน Banking.Api/Controllers/):
☐ TransactionsController: deposit, withdraw, transfer, history
☐ AccountsController: list, detail, balance, create
☐ AuthController: register, login, profile, logout
☐ AdminController: dashboard, freeze, unfreeze, unlock

Middleware:
☐ ExceptionMiddleware สร้างแล้ว

Infrastructure:
☐ ย้าย Controllers จาก Application → Api
☐ ลบ WeatherForecastController.cs + Class1.cs
☐ ลบ Microsoft.AspNetCore.Mvc.Core จาก Application.csproj
☐ appsettings.json มี Jwt section
☐ Program.cs: Authentication + Authorization middleware ตามลำดับ

Testing:
☐ Build ผ่าน 0 errors
☐ Register → Login → ใช้ token ทำราย���าร
☐ ฝาก/ถอน/โอน ทำงานถูกต้อง
☐ ถอนเกิน → 400 InsufficientFundsException
☐ ถอนเกิน Daily Limit → 400 DailyLimitExceededException
☐ โอนบัญชีเดียวกัน → 400 ArgumentException
☐ Login ผิด 5 ครั้ง → 403 AccountLockedException
☐ เข้า endpoint ไม่มี token → 401 Unauthorized
☐ Validation ผิด (amount=-100) → 400 + error message

เมื่อ checklist ครบ → พร้อมไป Phase 3: Redis + Real-time (SignalR)
```

---

## Troubleshooting

### "Unable to resolve service for type IUnitOfWork"
```
ลืมลงทะเบียนใน Program.cs:
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
```

### "Cannot access a disposed context instance"
```
ใช้ DbContext หลังจาก scope จบ → ต���วจสอบว่า async/await ครบทุกที่
อย่า return Task โดยไม่ await
```

### "Transaction has already been committed/disposed"
```
เรียก CommitTransactionAsync() หรือ RollbackTransactionAsync() ซ้ำ
→ เช็คว่า _transaction is not null ก่อนเรียก
```

### "FOR UPDATE cannot be used with window functions"
```
EF Core เพิ่ม ROW_NUMBER() อัตโนมัติ
→ ใช้ FromSqlRaw แทน LINQ สำหรับ FOR UPDATE query
```

### ฝากเงิน 2 ครั้งพร้อมกัน balance ผิด
```
ไม่ได้��ช้ GetByIdForUpdateAsync (ลืม lock row)
→ ทุก write operation ต้องใช้ FOR UPDATE
```

### "401 Unauthorized" ทุก request
```
1. ตรวจว่า app.UseAuthentication() อยู่ก่อน app.UseAuthorization()
2. ตรวจว่าส่ง header: Authorization: Bearer <token>
3. ตรวจว่า token ยังไม่หมดอายุ (15 นาที)
4. ตรวจว่า Jwt:Key ใน appsettings ตรงกับที่ JwtService ใช้
```

### "Unable to resolve service for type IJwtService"
```
ลืมลงทะเบียนใน Program.cs:
builder.Services.AddScoped<IJwtService, JwtService>();
```

### Register แล้ว "Email already registered"
```
DataSeeder สร้าง demo user ไว้ (admin@bank.com, demo@bank.com)
→ ใช้อีเมลอื่น หรือลบ seed data ก่อน
```

### FluentValidation ไม่ทำงาน
```
1. ตรวจว่า AddValidatorsFromAssemblyContaining<...>() ใน Program.cs
2. ตรวจว่า Controller inject IValidator<T> (ไม่ใช่ AbstractValidator<T>)
3. ตรวจว่า Validator class เป็น public (ไม่ใช่ internal)
```
