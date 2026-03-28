using Banking.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Banking.Infrastructure.Data;

public class AppDbContext: DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    // DbSet = ตาราง 1 ตัว → แต่ละ DbSet จะกลายเป็น 1 table ใน DB
    public DbSet<User> Users => Set<User>();
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<Transfer> Transfers => Set<Transfer>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            _ = entry.State switch
            {
                EntityState.Added => entry.Entity.CreatedAt = DateTime.UtcNow,
                EntityState.Modified => entry.Entity.UpdatedAt = DateTime.UtcNow,
                _ => default
            };
        }
        return await base.SaveChangesAsync(cancellationToken);
    }
}
