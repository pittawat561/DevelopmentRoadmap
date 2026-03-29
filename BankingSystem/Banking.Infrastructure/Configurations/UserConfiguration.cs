using Banking.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Banking.Infrastructure.Data.Configurations;

/// <summary>
/// Configuration สำหรับกำหนดโครงสร้างตาราง Users ใน database
/// Implement IEntityTypeConfiguration&lt;User&gt; เพื่อให้ EF Core โหลดอัตโนมัติ
/// ผ่าน ApplyConfigurationsFromAssembly() ใน AppDbContext.OnModelCreating()
/// </summary>
public class UserConfiguration : IEntityTypeConfiguration<User>
{
    /// <summary>
    /// กำหนดรายละเอียดของตาราง Users — คอลัมน์, constraint, index ต่างๆ
    /// EF Core เรียก method นี้ตอนสร้าง database model
    ///
    /// builder.ToTable("users") — ตั้งชื่อตารางเป็น "users" (lowercase ตาม PostgreSQL convention)
    /// builder.HasKey(u =&gt; u.Id) — กำหนด Id เป็น Primary Key
    ///
    /// builder.Property(...).IsRequired() — กำหนดให้คอลัมน์นี้ห้ามเป็น NULL
    /// builder.Property(...).HasMaxLength(n) — กำหนดความยาวสูงสุดของ string
    ///
    /// builder.HasIndex(u =&gt; u.Email).IsUnique() — สร้าง Unique Index บนคอลัมน์ Email
    ///   ป้องกันไม่ให้มีอีเมลซ้ำกันใน database (ถ้า INSERT อีเมลซ้ำ database จะ reject)
    ///
    /// builder.HasIndex(u =&gt; u.Phone).IsUnique() — สร้าง Unique Index บนคอลัมน์ Phone
    ///   ป้องกันเบอร์โทรซ้ำ
    ///
    /// builder.Property(u =&gt; u.KycStatus).HasConversion&lt;string&gt;() — แปลง enum เป็น string
    ///   ก่อนบันทึกลง database (เช่น KycStatus.Verified → "Verified")
    ///   ทำให้อ่านข้อมูลใน database ได้ง่ายกว่าเก็บเป็นตัวเลข
    ///
    /// builder.HasQueryFilter(u =&gt; !u.IsDeleted) — กำหนด Global Query Filter
    ///   ทุก query ที่ดึง User จะกรอง record ที่ IsDeleted == true ออกอัตโนมัติ
    ///   ทำให้ไม่ต้องเขียน .Where(u =&gt; !u.IsDeleted) ทุกครั้ง
    /// </summary>
    /// <param name="builder">EntityTypeBuilder สำหรับกำหนดโครงสร้างตาราง User</param>
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");
        builder.HasKey(u => u.Id);
        builder.Property(u => u.FirstName).IsRequired().HasMaxLength(100);
        builder.Property(u => u.LastName).IsRequired().HasMaxLength(100);
        builder.Property(u => u.Email).IsRequired().HasMaxLength(255);
        builder.HasIndex(u => u.Email).IsUnique();
        builder.Property(u => u.Phone).IsRequired().HasMaxLength(20);
        builder.HasIndex(u => u.Phone).IsUnique();
        builder.Property(u => u.PasswordHash).IsRequired().HasMaxLength(255);
        builder.Property(u => u.NationalIdHash).HasMaxLength(255);
        builder.Property(u => u.KycStatus).HasConversion<string>().HasMaxLength(20);
        builder.HasQueryFilter(u => !u.IsDeleted);
    }
}
