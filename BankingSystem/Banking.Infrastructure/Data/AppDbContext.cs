using Banking.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Banking.Infrastructure.Data;

/// <summary>
/// Database Context หลักของระบบธนาคาร — เป็นตัวกลางระหว่าง Application กับ Database
/// สืบทอดจาก DbContext ของ Entity Framework Core
/// ทำหน้าที่:
///   1. กำหนดตาราง (DbSet) ที่จะสร้างใน database
///   2. โหลด Configuration ของแต่ละ Entity (ผ่าน OnModelCreating)
///   3. อัปเดต CreatedAt/UpdatedAt อัตโนมัติเมื่อบันทึกข้อมูล (ผ่าน SaveChangesAsync)
/// </summary>
public class AppDbContext: DbContext
{
    /// <summary>
    /// Constructor รับ DbContextOptions ซึ่งมีการตั้งค่าต่างๆ เช่น connection string, database provider
    /// base(options) — ส่ง options ไปให้ constructor ของ DbContext (parent class)
    /// options ถูกกำหนดใน Program.cs ตอน AddDbContext (เลือก PostgreSQL + connection string)
    /// </summary>
    /// <param name="options">ตัวเลือกการตั้งค่า เช่น database provider และ connection string</param>
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    /// <summary>
    /// Protected constructor สำหรับ derived classes (ReadOnlyDbContext)
    /// ใช้ DbContextOptions non-generic เพื่อให้ subclass ส่ง options ของตัวเองมาได้
    /// </summary>
    protected AppDbContext(DbContextOptions options) : base(options) { }

    // === DbSet Properties ===
    // แต่ละ DbSet จะกลายเป็น 1 table ใน database
    // Set<T>() — method ของ DbContext ที่คืน DbSet<T> สำหรับ Entity ที่ระบุ

    /// <summary>
    /// ตาราง Users — เก็บข้อมูลผู้ใช้ทั้งหมด
    /// Set&lt;User&gt;() — สร้าง DbSet สำหรับ Entity User
    /// </summary>
    public DbSet<User> Users => Set<User>();

    /// <summary>
    /// ตาราง Accounts — เก็บข้อมูลบัญชีธนาคารทั้งหมด
    /// Set&lt;Account&gt;() — สร้าง DbSet สำหรับ Entity Account
    /// </summary>
    public DbSet<Account> Accounts => Set<Account>();

    /// <summary>
    /// ตาราง Transactions — เก็บข้อมูลธุรกรรมทั้งหมด (ฝาก, ถอน, โอน ฯลฯ)
    /// Set&lt;Transaction&gt;() — สร้าง DbSet สำหรับ Entity Transaction
    /// </summary>
    public DbSet<Transaction> Transactions => Set<Transaction>();

    /// <summary>
    /// ตาราง Transfers — เก็บข้อมูลการโอนเงินระหว่างบัญชี
    /// Set&lt;Transfer&gt;() — สร้าง DbSet สำหรับ Entity Transfer
    /// </summary>
    public DbSet<Transfer> Transfers => Set<Transfer>();

    /// <summary>
    /// ตาราง AuditLogs — เก็บบันทึกการกระทำทั้งหมดในระบบ (Audit Trail)
    /// Set&lt;AuditLog&gt;() — สร้าง DbSet สำหรับ Entity AuditLog
    /// </summary>
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    /// <summary>
    /// กำหนดโครงสร้างของ database (schema) — EF Core เรียก method นี้ตอนสร้าง Model
    /// ApplyConfigurationsFromAssembly() — สแกนหา class ทั้งหมดที่ implement IEntityTypeConfiguration
    /// ใน Assembly เดียวกัน แล้วนำ configuration มาใช้ทั้งหมดอัตโนมัติ
    /// ไม่ต้อง register ทีละตัว — แค่สร้าง Configuration class ไว้ใน project เดียวกันก็พอ
    ///
    /// typeof(AppDbContext).Assembly — ดึง Assembly ของ class นี้ (Banking.Infrastructure)
    /// ซึ่งจะพบ UserConfiguration, AccountConfiguration, TransactionConfiguration ฯลฯ
    /// </summary>
    /// <param name="modelBuilder">ตัวช่วยสร้าง Model สำหรับกำหนดโครงสร้าง database</param>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }

    /// <summary>
    /// Override SaveChangesAsync เพื่ออัปเดต CreatedAt/UpdatedAt อัตโนมัติก่อนบันทึกลง database
    /// ทำงานทุกครั้งที่เรียก SaveChangesAsync() จากที่ใดก็ตามในระบบ
    ///
    /// ChangeTracker.Entries&lt;BaseEntity&gt;() — ดึง Entity ทั้งหมดที่กำลังถูก track โดย EF Core
    /// กรองเฉพาะ Entity ที่สืบทอดจาก BaseEntity (ไม่รวม AuditLog)
    ///
    /// entry.State switch — ตรวจสอบสถานะของ Entity:
    ///   EntityState.Added — Entity ใหม่ที่กำลังจะ INSERT → ตั้ง CreatedAt = เวลาปัจจุบัน
    ///   EntityState.Modified — Entity ที่ถูกแก้ไข → ตั้ง UpdatedAt = เวลาปัจจุบัน
    ///   _ (default) — สถานะอื่น (Unchanged, Deleted) → ไม่ทำอะไร
    ///
    /// base.SaveChangesAsync() — เรียก SaveChangesAsync ของ DbContext ตัวจริง
    /// เพื่อส่ง SQL command ไปยัง database
    /// </summary>
    /// <param name="cancellationToken">Token สำหรับยกเลิก operation</param>
    /// <returns>จำนวน record ที่ถูกเปลี่ยนแปลงใน database</returns>
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
