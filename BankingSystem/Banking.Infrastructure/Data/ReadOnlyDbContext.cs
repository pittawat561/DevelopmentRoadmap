using Microsoft.EntityFrameworkCore;

namespace Banking.Infrastructure.Data;

/// <summary>
/// Read-Only DbContext — ใช้เฉพาะ read queries
/// เชื่อมต่อ PostgreSQL Read Replica
/// ไม่มี SaveChanges (ป้องกันเขียนลง replica)
/// </summary>
public class ReadOnlyDbContext : AppDbContext
{
    public ReadOnlyDbContext(DbContextOptions<ReadOnlyDbContext> options)
        : base(options) { }

    // ปิด change tracking → เร็วขึ้นสำหรับ read-only queries
    // ไม่ track entity changes → ไม่ใช้ memory เก็บ snapshot
    public override int SaveChanges()
    {
        throw new InvalidOperationException("This is a read-only context.");
    }

    public override Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        throw new InvalidOperationException("This is a read-only context.");
    }
}