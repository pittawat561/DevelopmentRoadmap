using Banking.Domain.Enums;

namespace Banking.Domain.Entities;

/// <summary>
/// Entity สำหรับเก็บข้อมูลผู้ใช้งาน (ลูกค้าธนาคาร)
/// สืบทอดจาก BaseEntity เพื่อได้รับ Id, CreatedAt, UpdatedAt, IsDeleted
/// ผู้ใช้ 1 คนสามารถมีหลายบัญชี (Accounts) — ความสัมพันธ์แบบ One-to-Many
/// </summary>
public class User : BaseEntity
{
    /// <summary>
    /// ชื่อจริงของผู้ใช้ เช่น "สมชาย"
    /// string.Empty — กำหนดค่าเริ่มต้นเป็นข้อความว่างเพื่อป้องกัน null reference
    /// </summary>
    public string FirstName { get; set; } = string.Empty;

    /// <summary>
    /// นามสกุลของผู้ใช้ เช่น "ใจดี"
    /// </summary>
    public string LastName { get; set; } = string.Empty;

    /// <summary>
    /// อีเมลของผู้ใช้ — ใช้เป็น unique identifier สำหรับ login
    /// มี unique index ใน database ป้องกันอีเมลซ้ำ
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// เบอร์โทรศัพท์ของผู้ใช้ เช่น "0812345678"
    /// มี unique index ใน database ป้องกันเบอร์ซ้ำ
    /// </summary>
    public string Phone { get; set; } = string.Empty;

    /// <summary>
    /// รหัสผ่านที่ถูก hash แล้ว (ไม่เก็บรหัสผ่านจริง)
    /// ใช้ BCrypt ในการ hash เพื่อความปลอดภัย ป้องกันการดูรหัสผ่านจริงแม้ DB ถูกเจาะ
    /// </summary>
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>
    /// เลขบัตรประชาชนที่ถูก hash แล้ว — ใช้ยืนยันตัวตน (KYC)
    /// เป็น nullable เพราะอาจยังไม่ได้กรอกตอนสมัคร
    /// </summary>
    public string? NationalIdHash { get; set; }

    /// <summary>
    /// สถานะการยืนยันตัวตน KYC (Know Your Customer)
    /// Pending = รอตรวจสอบ, Verified = ผ่านแล้ว, Rejected = ไม่ผ่าน
    /// KycStatus.Pending — ค่าเริ่มต้นคือ "รอตรวจสอบ" เพราะผู้ใช้ใหม่ยังไม่ผ่าน KYC
    /// </summary>
    public KycStatus KycStatus { get; set; } = KycStatus.Pending;

    /// <summary>
    /// สถานะว่าผู้ใช้ยังใช้งานได้หรือไม่
    /// true = ใช้งานได้ปกติ, false = ถูกปิดการใช้งาน (เช่น ลูกค้าขอยกเลิก)
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// สถานะการล็อคบัญชี — ถูกล็อคเมื่อกรอกรหัสผ่านผิดเกินจำนวนครั้งที่กำหนด
    /// true = ถูกล็อค (ห้าม login), false = ปกติ
    /// </summary>
    public bool IsLocked { get; set; } = false;

    /// <summary>
    /// จำนวนครั้งที่ login ผิดติดต่อกัน
    /// เมื่อถึงจำนวนที่กำหนด (เช่น 5 ครั้ง) ระบบจะล็อคบัญชี (IsLocked = true)
    /// เมื่อ login สำเร็จจะ reset กลับเป็น 0
    /// </summary>
    public int FailedLoginAttempts { get; set; } = 0;

    /// <summary>
    /// วันเวลาที่ login สำเร็จครั้งล่าสุด
    /// เป็น nullable เพราะผู้ใช้ใหม่ที่ยังไม่เคย login จะเป็น null
    /// ใช้ตรวจสอบว่าบัญชีถูกใช้งานล่าสุดเมื่อไหร่
    /// </summary>
    public DateTime? LastLoginAt { get; set; }

    /// <summary>
    /// ชื่อ-นามสกุลเต็มของผู้ใช้ (Computed Property)
    /// ใช้ string interpolation ($"") รวม FirstName กับ LastName เข้าด้วยกัน
    /// เป็น read-only property (มีแค่ get) — ไม่ได้เก็บใน database แต่คำนวณตอนเรียกใช้
    /// </summary>
    public string FullName => $"{FirstName} {LastName}";

    // === Navigation Properties ===
    // ใช้บอก EF Core ว่า User มีความสัมพันธ์กับ Entity อื่น

    /// <summary>
    /// รายการบัญชีทั้งหมดของผู้ใช้คนนี้ — ความสัมพันธ์แบบ One-to-Many
    /// User 1 คน มีได้หลาย Account (ออมทรัพย์, กระแสรายวัน, ฝากประจำ)
    /// new List&lt;Account&gt;() — สร้าง list ว่างเริ่มต้นเพื่อป้องกัน null reference
    /// EF Core จะโหลดข้อมูลจริงจาก database เมื่อเรียกใช้ (Lazy/Eager Loading)
    /// </summary>
    public ICollection<Account> Accounts { get; set; } = new List<Account>();

    /// <summary>
    /// PIN 6 หลัก — Hash ด้วย BCrypt เหมือน password
    /// ห้ามเก็บ plaintext!
    /// null = ยังไม่ตั้ง PIN (บังคับตั้งก่อนทำธุรกรรมแรก)
    /// </summary>
    public string? PinHash { get; set; }

    /// <summary>
    /// นับจำนวน PIN ผิดติดต่อกัน
    /// ถึง 3 ครั้ง → ล็อกธุรกรรม (ต้อง reset PIN)
    /// </summary>
    public int FailedPinAttempts { get; set; } = 0;

    /// <summary>
    /// ธุรกรรมถูกล็อก (PIN ผิดเกิน)
    /// ต่างจาก IsLocked (login ถูกล็อก)
    /// </summary>
    public bool IsTransactionLocked { get; set; } = false;
}
