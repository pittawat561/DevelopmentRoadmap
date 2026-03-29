using Banking.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Banking.Infrastructure.Data.Configurations;

/// <summary>
/// Configuration สำหรับกำหนดโครงสร้างตาราง Transactions ใน database
/// Implement IEntityTypeConfiguration&lt;Transaction&gt; เพื่อให้ EF Core โหลดอัตโนมัติ
/// </summary>
public class TransactionConfiguration : IEntityTypeConfiguration<Transaction>
{
    /// <summary>
    /// กำหนดรายละเอียดของตาราง Transactions — คอลัมน์, constraint, index, relationship
    ///
    /// builder.ToTable("transactions") — ตั้งชื่อตารางเป็น "transactions"
    /// builder.HasKey(t =&gt; t.Id) — กำหนด Id เป็น Primary Key
    ///
    /// builder.Property(t =&gt; t.ReferenceNumber).HasMaxLength(50).IsRequired()
    ///   เลขอ้างอิงธุรกรรม — ห้ามว่าง, ยาวไม่เกิน 50 ตัวอักษร
    /// builder.HasIndex(t =&gt; t.ReferenceNumber).IsUnique()
    ///   Unique Index — เลขอ้างอิงห้ามซ้ำ
    ///
    /// builder.Property(t =&gt; t.Type).HasConversion&lt;string&gt;()
    ///   แปลง enum TransactionType เป็น string (เช่น Deposit → "Deposit")
    ///
    /// builder.Property(t =&gt; t.Amount).HasPrecision(18, 2)
    ///   กำหนดความแม่นยำ — 18 หลัก, ทศนิยม 2 ตำแหน่ง
    ///
    /// builder.HasOne(t =&gt; t.Account).WithMany(a =&gt; a.Transactions)
    ///   ความสัมพันธ์ Transaction → Account (Many-to-One)
    ///   .HasForeignKey(t =&gt; t.AccountId) — AccountId เป็น Foreign Key
    ///   .OnDelete(DeleteBehavior.Restrict) — ห้ามลบ Account ถ้ายังมี Transaction อยู่
    ///
    /// builder.HasOne(t =&gt; t.RelatedTransaction).WithOne()
    ///   Self-Referencing Relationship — Transaction เชื่อมกับ Transaction อื่น
    ///   ใช้ในกรณีโอนเงิน: TransferOut ↔ TransferIn ชี้ถึงกัน
    ///   .HasForeignKey&lt;Transaction&gt;(t =&gt; t.RelatedTransactionId)
    ///   .OnDelete(DeleteBehavior.SetNull) — ถ้าลบ Transaction คู่ → set เป็น null (ไม่ลบตาม)
    ///
    /// builder.HasIndex(t =&gt; new { t.AccountId, t.CreatedAt })
    ///   Composite Index — เพิ่มความเร็วในการค้นหาธุรกรรมของบัญชี + ช่วงเวลา
    ///   ใช้บ่อยมากในการดู Statement (เช่น "ดูธุรกรรมของบัญชี X ในเดือนนี้")
    ///
    /// builder.HasQueryFilter(t =&gt; !t.IsDeleted) — Global Query Filter กรอง Soft Delete
    /// </summary>
    /// <param name="builder">EntityTypeBuilder สำหรับกำหนดโครงสร้างตาราง Transaction</param>
    public void Configure(EntityTypeBuilder<Transaction> builder)
    {
        builder.ToTable("transactions");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.ReferenceNumber).HasMaxLength(50).IsRequired();
        builder.HasIndex(t => t.ReferenceNumber).IsUnique();

        builder.Property(t => t.Type).HasConversion<string>().HasMaxLength(20);
        builder.Property(t => t.Amount).HasPrecision(18, 2);
        builder.Property(t => t.BalanceBefore).HasPrecision(18, 2);
        builder.Property(t => t.BalanceAfter).HasPrecision(18, 2);
        builder.Property(t => t.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(t => t.Description).HasMaxLength(500);
        builder.Property(t => t.IpAddress).HasMaxLength(45);

        builder.HasOne(t => t.Account)
            .WithMany(a => a.Transactions)
            .HasForeignKey(t => t.AccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.RelatedTransaction)
            .WithOne()
            .HasForeignKey<Transaction>(t => t.RelatedTransactionId)
            .OnDelete(DeleteBehavior.SetNull);

        // Composite Index: ค้นหา transactions ของบัญชี + ช่วงเวลา (ใช้บ่อยมาก!)
        builder.HasIndex(t => new { t.AccountId, t.CreatedAt });
        builder.HasIndex(t => t.AccountId);
        builder.HasIndex(t => t.CreatedAt);

        builder.HasQueryFilter(t => !t.IsDeleted);
    }
}
