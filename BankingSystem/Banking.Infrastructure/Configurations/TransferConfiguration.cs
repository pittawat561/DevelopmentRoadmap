using Banking.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Banking.Infrastructure.Data.Configurations;

/// <summary>
/// Configuration สำหรับกำหนดโครงสร้างตาราง Transfers ใน database
/// Implement IEntityTypeConfiguration&lt;Transfer&gt; เพื่อให้ EF Core โหลดอัตโนมัติ
/// </summary>
public class TransferConfiguration : IEntityTypeConfiguration<Transfer>
{
    /// <summary>
    /// กำหนดรายละเอียดของตาราง Transfers — คอลัมน์, constraint, relationship
    ///
    /// builder.ToTable("transfers") — ตั้งชื่อตารางเป็น "transfers"
    /// builder.HasKey(t =&gt; t.Id) — กำหนด Id เป็น Primary Key
    ///
    /// builder.Property(t =&gt; t.Amount).HasPrecision(18, 2)
    ///   จำนวนเงินที่โอน — 18 หลัก, ทศนิยม 2 ตำแหน่ง
    ///
    /// builder.Property(t =&gt; t.Fee).HasPrecision(18, 2).HasDefaultValue(0)
    ///   ค่าธรรมเนียม — ค่าเริ่มต้นเป็น 0
    ///
    /// builder.HasOne(t =&gt; t.FromAccount).WithMany()
    ///   ความสัมพันธ์ Transfer → Account (บัญชีต้นทาง)
    ///   .HasForeignKey(t =&gt; t.FromAccountId) — FromAccountId เป็น Foreign Key
    ///   .OnDelete(DeleteBehavior.Restrict) — ห้ามลบบัญชีถ้ายังมี Transfer อ้างอิง
    ///   .WithMany() — ไม่ต้องมี Navigation Property ฝั่ง Account กลับมา
    ///
    /// builder.HasOne(t =&gt; t.ToAccount).WithMany()
    ///   ความสัมพันธ์ Transfer → Account (บัญชีปลายทาง)
    ///   เหมือนกับ FromAccount แต่เป็นฝั่งผู้รับโอน
    ///
    /// builder.HasOne(t =&gt; t.DebitTransaction).WithOne()
    ///   ความสัมพันธ์ Transfer → Transaction (ธุรกรรมฝั่งหักเงิน)
    ///   .HasForeignKey&lt;Transfer&gt;(t =&gt; t.DebitTransactionId)
    ///   .OnDelete(DeleteBehavior.SetNull) — ถ้าลบ Transaction → set FK เป็น null
    ///
    /// builder.HasOne(t =&gt; t.CreditTransaction).WithOne()
    ///   ความสัมพันธ์ Transfer → Transaction (ธุรกรรมฝั่งเพิ่มเงิน)
    ///   คล้ายกับ DebitTransaction แต่เป็นฝั่งผู้รับ
    ///
    /// builder.HasQueryFilter(t =&gt; !t.IsDeleted) — Global Query Filter กรอง Soft Delete
    /// </summary>
    /// <param name="builder">EntityTypeBuilder สำหรับกำหนดโครงสร้างตาราง Transfer</param>
    public void Configure(EntityTypeBuilder<Transfer> builder)
    {
        builder.ToTable("transfers");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Amount).HasPrecision(18, 2);
        builder.Property(t => t.Fee).HasPrecision(18, 2).HasDefaultValue(0);
        builder.Property(t => t.Status).HasConversion<string>().HasMaxLength(20);

        builder.HasOne(t => t.FromAccount)
            .WithMany()
            .HasForeignKey(t => t.FromAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.ToAccount)
            .WithMany()
            .HasForeignKey(t => t.ToAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.DebitTransaction)
            .WithOne()
            .HasForeignKey<Transfer>(t => t.DebitTransactionId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(t => t.CreditTransaction)
            .WithOne()
            .HasForeignKey<Transfer>(t => t.CreditTransactionId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasQueryFilter(t => !t.IsDeleted);
    }
}
