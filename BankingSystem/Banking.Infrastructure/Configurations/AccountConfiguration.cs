using Banking.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Banking.Infrastructure.Data.Configurations;

/// <summary>
/// Configuration สำหรับกำหนดโครงสร้างตาราง Accounts ใน database
/// Implement IEntityTypeConfiguration&lt;Account&gt; เพื่อให้ EF Core โหลดอัตโนมัติ
/// </summary>
public class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    /// <summary>
    /// กำหนดรายละเอียดของตาราง Accounts — คอลัมน์, constraint, index, relationship
    ///
    /// builder.ToTable("accounts") — ตั้งชื่อตารางเป็น "accounts"
    /// builder.HasKey(a =&gt; a.Id) — กำหนด Id เป็น Primary Key
    ///
    /// builder.Property(a =&gt; a.AccountNumber).IsRequired().HasMaxLength(20)
    ///   กำหนดให้เลขบัญชีห้ามเป็น NULL และยาวไม่เกิน 20 ตัวอักษร
    ///
    /// builder.HasIndex(a =&gt; a.AccountNumber).IsUnique()
    ///   สร้าง Unique Index — เลขบัญชีห้ามซ้ำกัน
    ///
    /// builder.Property(a =&gt; a.Type).HasConversion&lt;string&gt;()
    ///   แปลง enum AccountType เป็น string ก่อนบันทึก (เช่น Savings → "Savings")
    ///
    /// builder.Property(a =&gt; a.Balance).HasPrecision(18, 2)
    ///   กำหนดความแม่นยำของ decimal — 18 หลักรวม, 2 ทศนิยม
    ///   รองรับตัวเลขสูงสุดถึง 9,999,999,999,999,999.99
    ///
    /// builder.ToTable(t =&gt; t.HasCheckConstraint("CK_accounts_balance_positive", ...))
    ///   สร้าง Check Constraint ในระดับ database — ห้าม Balance ติดลบ
    ///   เป็นการป้องกันอีกชั้น นอกจาก application-level validation
    ///
    /// builder.HasOne(a =&gt; a.User).WithMany(u =&gt; u.Accounts)
    ///   กำหนดความสัมพันธ์ Account → User (Many-to-One)
    ///   .HasForeignKey(a =&gt; a.UserId) — UserId เป็น Foreign Key ชี้ไปที่ Users.Id
    ///   .OnDelete(DeleteBehavior.Restrict) — ห้ามลบ User ถ้ายังมี Account อยู่
    ///     (ต้องลบ/ปิดบัญชีทั้งหมดก่อนถึงจะลบ User ได้)
    ///
    /// builder.HasQueryFilter(a =&gt; !a.IsDeleted) — Global Query Filter กรอง Soft Delete
    ///
    /// builder.HasIndex(a =&gt; a.UserId) — สร้าง Index บน UserId
    ///   เพิ่มความเร็วในการค้นหาบัญชีของผู้ใช้คนหนึ่ง
    ///
    /// builder.HasIndex(a =&gt; a.Status) — สร้าง Index บน Status
    ///   เพิ่มความเร็วในการกรองบัญชีตามสถานะ (Active, Frozen, Closed)
    /// </summary>
    /// <param name="builder">EntityTypeBuilder สำหรับกำหนดโครงสร้างตาราง Account</param>
    public void Configure(EntityTypeBuilder<Account> builder)
    {
        builder.ToTable("accounts");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.AccountNumber).IsRequired().HasMaxLength(20);
        builder.HasIndex(a => a.AccountNumber).IsUnique();
        builder.Property(a => a.Type).HasConversion<string>().HasMaxLength(20);
        builder.Property(a => a.Currency).HasMaxLength(3).HasDefaultValue("THB");
        builder.Property(a => a.Balance).HasPrecision(18, 2).HasDefaultValue(0);
        builder.Property(a => a.AvailableBalance).HasPrecision(18, 2).HasDefaultValue(0);
        builder.Property(a => a.DailyWithdrawalLimit).HasPrecision(18, 2).HasDefaultValue(50000);
        builder.Property(a => a.Status).HasConversion<string>().HasMaxLength(20);
        builder.ToTable(t => t.HasCheckConstraint("CK_accounts_balance_positive", "\"Balance\" >= 0"));
        builder.HasOne(a => a.User)
           .WithMany(u => u.Accounts)
           .HasForeignKey(a => a.UserId)
           .OnDelete(DeleteBehavior.Restrict);
        builder.HasQueryFilter(a => !a.IsDeleted);
        builder.HasIndex(a => a.UserId);
        builder.HasIndex(a => a.Status);

    }
}
