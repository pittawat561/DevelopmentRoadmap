using Banking.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Banking.Infrastructure.Data.Configurations;

/// <summary>
/// Configuration สำหรับกำหนดโครงสร้างตาราง AuditLogs ใน database
/// Implement IEntityTypeConfiguration&lt;AuditLog&gt; เพื่อให้ EF Core โหลดอัตโนมัติ
/// AuditLog ไม่ใช้ Soft Delete เพราะ log ห้ามลบ — ต้องเก็บถาวรเพื่อ compliance
/// </summary>
public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    /// <summary>
    /// กำหนดรายละเอียดของตาราง AuditLogs — คอลัมน์, constraint, index
    ///
    /// builder.ToTable("audit_logs") — ตั้งชื่อตารางเป็น "audit_logs"
    /// builder.HasKey(a =&gt; a.Id) — กำหนด Id เป็น Primary Key
    ///
    /// builder.Property(a =&gt; a.Id).UseIdentityAlwaysColumn()
    ///   ใช้ PostgreSQL Identity Column — database จะสร้างค่า Id อัตโนมัติ (auto-increment)
    ///   "Always" หมายถึงห้าม INSERT ค่า Id เอง — ต้องให้ database สร้างเท่านั้น
    ///
    /// builder.Property(a =&gt; a.Action).HasMaxLength(50).IsRequired()
    ///   ชื่อ action เช่น "Create", "Update" — ห้ามว่าง, ยาวไม่เกิน 50
    ///
    /// builder.Property(a =&gt; a.OldValues).HasColumnType("jsonb")
    ///   ใช้ jsonb column type ของ PostgreSQL — เก็บ JSON แบบ binary
    ///   เร็วกว่า json ธรรมดาเพราะถูก parse ตอนบันทึก ไม่ต้อง parse ตอนอ่าน
    ///   รองรับ query ข้อมูลภายใน JSON ได้ (เช่น ค้นหา field เฉพาะใน JSON)
    ///
    /// builder.HasIndex(a =&gt; a.UserId) — Index สำหรับค้นหา log ตามผู้ใช้
    ///   เช่น "ดูว่า user นี้ทำอะไรบ้าง"
    ///
    /// builder.HasIndex(a =&gt; a.CreatedAt) — Index สำหรับค้นหา log ตามวันเวลา
    ///   เช่น "ดู log ของวันนี้", "ดู log ย้อนหลัง 7 วัน"
    ///
    /// builder.HasIndex(a =&gt; new { a.EntityType, a.EntityId })
    ///   Composite Index — ค้นหา log ตามประเภท Entity + รหัส Entity
    ///   เช่น "ดูว่า Account นี้ถูกแก้ไขอะไรบ้าง"
    /// </summary>
    /// <param name="builder">EntityTypeBuilder สำหรับกำหนดโครงสร้างตาราง AuditLog</param>
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("audit_logs");

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).UseIdentityAlwaysColumn();

        builder.Property(a => a.Action).HasMaxLength(50).IsRequired();
        builder.Property(a => a.EntityType).HasMaxLength(100).IsRequired();
        builder.Property(a => a.EntityId).HasMaxLength(100);
        builder.Property(a => a.IpAddress).HasMaxLength(45);
        builder.Property(a => a.UserAgent).HasMaxLength(500);

        // JSONB columns (PostgreSQL)
        builder.Property(a => a.OldValues).HasColumnType("jsonb");
        builder.Property(a => a.NewValues).HasColumnType("jsonb");

        // Indexes
        builder.HasIndex(a => a.UserId);
        builder.HasIndex(a => a.CreatedAt);
        builder.HasIndex(a => new { a.EntityType, a.EntityId });
    }
}
