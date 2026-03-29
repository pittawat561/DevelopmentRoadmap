
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
    private IUserRepository? _users;
    private IAccountRepository? _accounts;
    private ITransactionRepository? _transactions;

    public UnitOfWork(AppDbContext context)
    {
        _context = context;
    }
    public IUserRepository Users => _users ??= new UserRepository(_context);
    public IAccountRepository Accounts => _accounts ??= new AccountRepository(_context);
    public ITransactionRepository Transactions => _transactions ??= new TransactionRepository(_context);
    public async Task BeginTransactionAsync(CancellationToken ct = default)
    {
        if (_transaction == null)
            _transaction = await _context.Database.BeginTransactionAsync(ct);
    }
    public async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        return await _context.SaveChangesAsync(ct);
    }
    public async Task CommitTransactionAsync(CancellationToken ct = default)
    {
        if (_transaction is not null)
        {
            await _transaction.CommitAsync(ct);
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }
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
