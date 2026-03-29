
namespace Banking.Domain.Entities;

/// <summary>
/// Entity สำหรับเก็บบันทึกการกระทำ (Audit Log) ในระบบ
/// ไม่ได้สืบทอดจาก BaseEntity เพราะใช้ Id แบบ long (auto-increment) แทน Guid
/// และไม่ต้องการ Soft Delete เพราะ Audit Log ห้ามลบ — ต้องเก็บไว้ตลอด
/// ใช้ติดตามว่า "ใคร ทำอะไร กับข้อมูลอะไร เมื่อไหร่ จาก IP ไหน"
/// สำคัญมากสำหรับระบบธนาคาร เพราะต้องตรวจสอบย้อนหลังได้ (Compliance)
/// </summary>
public class AuditLog
{
    /// <summary>
    /// รหัส Audit Log — Primary Key แบบ auto-increment (ใช้ long เพราะจะมีจำนวนมาก)
    /// ใช้ UseIdentityAlwaysColumn() ใน PostgreSQL — database จะสร้างค่าให้อัตโนมัติ
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// รหัสผู้ใช้ที่ทำ action นี้
    /// เป็น nullable เพราะบาง action อาจเกิดจากระบบ (ไม่มี user) เช่น scheduled job
    /// </summary>
    public Guid? UserId { get; set; }

    /// <summary>
    /// ชื่อ action ที่ทำ เช่น "Create", "Update", "Delete", "Login", "Transfer"
    /// ใช้บอกว่าเกิดเหตุการณ์อะไรขึ้น
    /// </summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>
    /// ประเภท Entity ที่ถูกกระทำ เช่น "User", "Account", "Transaction"
    /// ใช้ร่วมกับ EntityId เพื่อระบุว่า action เกิดกับ record ไหน
    /// </summary>
    public string EntityType { get; set; } = string.Empty;

    /// <summary>
    /// รหัสของ Entity ที่ถูกกระทำ (เก็บเป็น string เพราะ Id อาจเป็น Guid หรือ long)
    /// เป็น nullable เพราะบาง action ไม่ได้กระทำกับ Entity ใดเป็นเฉพาะ เช่น Login
    /// </summary>
    public string? EntityId { get; set; }

    /// <summary>
    /// ค่าเดิมก่อนถูกแก้ไข — เก็บเป็น JSON string (PostgreSQL ใช้ jsonb column)
    /// เช่น {"Balance": 10000} ก่อนถอนเงิน
    /// เป็น nullable เพราะ action แบบ Create ไม่มีค่าเดิม
    /// </summary>
    public string? OldValues { get; set; }

    /// <summary>
    /// ค่าใหม่หลังถูกแก้ไข — เก็บเป็น JSON string (PostgreSQL ใช้ jsonb column)
    /// เช่น {"Balance": 5000} หลังถอนเงิน
    /// เป็น nullable เพราะ action แบบ Delete ไม่มีค่าใหม่
    /// </summary>
    public string? NewValues { get; set; }

    /// <summary>
    /// IP Address ของผู้ทำ action — ใช้ตรวจสอบว่าทำรายการจากที่ไหน
    /// เป็น nullable เพราะบาง action เกิดจากระบบ ไม่มี IP
    /// </summary>
    public string? IpAddress { get; set; }

    /// <summary>
    /// User Agent ของ browser/app ที่ใช้ — เช่น "Mozilla/5.0 (Windows NT 10.0...)"
    /// ใช้ตรวจสอบว่าใช้อุปกรณ์อะไรทำรายการ
    /// เป็น nullable เพราะบาง action เกิดจากระบบ
    /// </summary>
    public string? UserAgent { get; set; }

    /// <summary>
    /// วันเวลาที่เกิด action นี้
    /// DateTime.UtcNow — ดึงเวลาปัจจุบันแบบ UTC (เวลาสากล)
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
