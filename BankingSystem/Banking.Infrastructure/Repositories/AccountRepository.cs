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
    /// Row-Level Lock — ป้องกัน Race Condition ตอนฝาก/ถอน/โอน
    /// SELECT ... FOR UPDATE ทำให้ request อื่นต้องรอจนกว่า transaction จะจบ
    /// </summary>
    public async Task<Account?> GetByIdForUpdateAsync(Guid id, CancellationToken ct = default)
    {
        // 1. ใช้ Interpolated String ($) ร่วมกับ FromSql เพื่อป้องกัน SQL Injection อัตโนมัติ
        // 2. EF Core จะเปลี่ยน {id} เป็น Parameter (@p0) ให้เอง
        // 3. ใช้ "Id" (Double quote) ให้ตรงกับ Case-sensitive ของ PostgreSQL
        return await _dbSet
            .FromSql($@"SELECT * FROM accounts 
                    WHERE ""Id"" = {id} 
                    AND ""IsDeleted"" = false 
                    FOR UPDATE")
            .AsTracking() // มั่นใจว่า EF จะ Track ตัวนี้เพื่อรอการ Update กลับ
            .FirstOrDefaultAsync(ct);
    }
    public async Task<bool> AccountNumberExistsAsync(
        string accountNumber, CancellationToken ct = default)
    {
        return await _dbSet.AnyAsync(a => a.AccountNumber == accountNumber, ct);
    }
}
