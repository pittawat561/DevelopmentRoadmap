
using Banking.Domain.Entities;
using Banking.Domain.Enums;
using Banking.Domain.Interfaces;
using Banking.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Banking.Infrastructure.Repositories
{
    public class TransactionRepository : Repository<Transaction>, ITransactionRepository
    {
        public TransactionRepository(AppDbContext context) : base(context) { }

        /// <summary>
        /// ดึงธุรกรรมแบบ Pagination — ใช้แสดง Statement
        /// Skip + Take = pagination จริง (ไม่โหลดทุก record)
        /// OrderByDescending(CreatedAt) = ล่าสุดขึ้นก่อน
        /// </summary>
        public async Task<List<Transaction>> GetByAccountIdAsync(Guid accountId, int page, int pageSize, CancellationToken ct = default)
        {
            return await _dbSet
                .Where(t => t.AccountId == accountId)
                .OrderByDescending(t => t.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);
        }
        /// <summary>
        /// นับจำนวนธุรกรรมทั้งหมด — ใช้คำนวณจำนวนหน้า
        /// CountAsync เร็วกว่าโหลดทั้งหมดแล้ว .Count
        /// </summary>
        public async Task<int> GetCountByAccountIdAsync(Guid accountId, CancellationToken ct = default)
        {
            return await _dbSet.CountAsync(t => t.AccountId == accountId, ct);
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
        public async Task<decimal> GetTodayWithdrawalTotalAsync(Guid accountId, CancellationToken ct = default)
        {
            var todayUtc = DateTime.UtcNow.Date;
            return await _dbSet
                .Where(t => t.AccountId == accountId)
                .Where(t => t.Type == TransactionType.Withdrawal || t.Type == TransactionType.TransferOut)
                .Where(t => t.Status == TransactionStatus.Completed)
                .Where(t => t.CreatedAt >= todayUtc)
                .SumAsync(t => t.Amount, ct);
        }
    }
}
