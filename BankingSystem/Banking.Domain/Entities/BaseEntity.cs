namespace Banking.Domain.Entities;

/// <summary>
/// คลาสฐาน (Base Class) สำหรับ Entity ทั้งหมดในระบบธนาคาร
/// ทุก Entity เช่น User, Account, Transaction จะสืบทอด (inherit) จากคลาสนี้
/// เพื่อให้มี property พื้นฐานที่ใช้ร่วมกัน เช่น Id, CreatedAt, UpdatedAt, IsDeleted
/// ใช้ abstract class เพราะไม่ต้องการให้สร้าง instance ของ BaseEntity โดยตรง
/// </summary>
public abstract class BaseEntity
{
    /// <summary>
    /// รหัสเฉพาะ (Primary Key) ของแต่ละ Entity
    /// ใช้ Guid เพื่อให้ไม่ซ้ำกันแม้สร้างจากหลายเครื่อง
    /// Guid.NewGuid() — สร้างค่า Guid ใหม่อัตโนมัติทุกครั้งที่สร้าง Entity
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// วันเวลาที่สร้าง record นี้ในฐานข้อมูล
    /// DateTime.UtcNow — ดึงเวลาปัจจุบันแบบ UTC (เวลาสากล ไม่ขึ้นกับ timezone)
    /// ใช้ UTC เพราะระบบธนาคารอาจมีผู้ใช้หลาย timezone
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// วันเวลาที่แก้ไข record นี้ล่าสุด
    /// เป็น nullable (DateTime?) เพราะตอนสร้างใหม่ยังไม่เคยถูกแก้ไข จึงเป็น null
    /// จะถูกอัปเดตอัตโนมัติโดย AppDbContext.SaveChangesAsync() เมื่อ Entity ถูก modified
    /// </summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// สถานะการลบแบบ Soft Delete — ไม่ลบข้อมูลจริงออกจากฐานข้อมูล
    /// false = ยังใช้งานอยู่, true = ถูกลบแล้ว (แต่ข้อมูลยังอยู่ใน DB)
    /// ระบบธนาคารต้องเก็บประวัติทุกอย่าง จึงใช้ Soft Delete แทนการลบจริง
    /// EF Core จะใช้ QueryFilter กรอง IsDeleted == true ออกจาก query อัตโนมัติ
    /// </summary>
    public bool IsDeleted { get; set; } = false;
}
