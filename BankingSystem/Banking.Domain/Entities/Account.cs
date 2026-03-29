using Banking.Domain.Enums;

namespace Banking.Domain.Entities;

/// <summary>
/// Entity สำหรับเก็บข้อมูลบัญชีธนาคาร
/// สืบทอดจาก BaseEntity เพื่อได้รับ Id, CreatedAt, UpdatedAt, IsDeleted
/// บัญชี 1 บัญชีเป็นของผู้ใช้ 1 คน (Many-to-One กับ User)
/// บัญชี 1 บัญชีมีได้หลายธุรกรรม (One-to-Many กับ Transaction)
/// </summary>
public class Account : BaseEntity
{
    /// <summary>
    /// รหัสผู้ใช้ที่เป็นเจ้าของบัญชีนี้ — Foreign Key เชื่อมไปยังตาราง Users
    /// ใช้ Guid เพื่อให้ตรงกับ Id ของ User
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// เลขที่บัญชี เช่น "1234-5678-9012"
    /// มี unique index ใน database — เลขบัญชีห้ามซ้ำกัน
    /// </summary>
    public string AccountNumber { get; set; } = string.Empty;

    /// <summary>
    /// ประเภทบัญชี — Savings (ออมทรัพย์), Checking (กระแสรายวัน), FixedDeposit (ฝากประจำ)
    /// AccountType.Savings — ค่าเริ่มต้นเป็นบัญชีออมทรัพย์
    /// </summary>
    public AccountType Type { get; set; } = AccountType.Savings;

    /// <summary>
    /// สกุลเงินของบัญชี เช่น "THB" (บาทไทย), "USD" (ดอลลาร์สหรัฐ)
    /// ค่าเริ่มต้นเป็น "THB" เพราะเป็นระบบธนาคารไทย
    /// </summary>
    public string Currency { get; set; } = "THB";

    /// <summary>
    /// ยอดเงินคงเหลือจริงในบัญชี (Actual Balance)
    /// ค่าเริ่มต้นเป็น 0 — บัญชีใหม่ยังไม่มีเงิน
    /// ใช้ decimal เพื่อความแม่นยำในการคำนวณเงิน (ไม่ใช้ float/double เพราะมีปัญหาทศนิยม)
    /// </summary>
    public decimal Balance { get; set; } = 0;

    /// <summary>
    /// ยอดเงินที่ใช้ได้จริง (Available Balance)
    /// อาจน้อยกว่า Balance เพราะบางส่วนอาจถูกอายัด (hold) จากธุรกรรมที่อยู่ระหว่างดำเนินการ
    /// เช่น โอนเงิน 1,000 บาทแต่ยังไม่สำเร็จ — Balance = 10,000 แต่ AvailableBalance = 9,000
    /// </summary>
    public decimal AvailableBalance { get; set; } = 0;

    /// <summary>
    /// วงเงินถอนสูงสุดต่อวัน (Daily Withdrawal Limit)
    /// ค่าเริ่มต้น 50,000 บาท — ป้องกันการถอนเงินจำนวนมากในกรณีบัญชีถูกขโมย
    /// ใช้ underscore (50_000) เป็น digit separator ช่วยให้อ่านง่ายขึ้น
    /// </summary>
    public decimal DailyWithdrawalLimit { get; set; } = 50_000;  // 50,000 THB default

    /// <summary>
    /// สถานะของบัญชี — Active (ใช้งานได้), Frozen (ถูกระงับ), Closed (ปิดแล้ว)
    /// AccountStatus.Active — ค่าเริ่มต้นเป็น Active เพราะบัญชีใหม่พร้อมใช้งาน
    /// </summary>
    public AccountStatus Status { get; set; } = AccountStatus.Active;

    // === Navigation Properties ===
    // ใช้บอก EF Core ว่า Account มีความสัมพันธ์กับ Entity อื่น

    /// <summary>
    /// ผู้ใช้ที่เป็นเจ้าของบัญชีนี้ — Navigation Property แบบ Many-to-One
    /// null! — บอก compiler ว่าจะไม่เป็น null เพราะ EF Core จะโหลดให้
    /// ใช้เข้าถึงข้อมูลผู้ใช้ เช่น account.User.FullName
    /// </summary>
    public User User { get; set; } = null!;

    /// <summary>
    /// รายการธุรกรรมทั้งหมดของบัญชีนี้ — Navigation Property แบบ One-to-Many
    /// Account 1 บัญชีมีได้หลาย Transaction (ฝาก, ถอน, โอน ฯลฯ)
    /// new List&lt;Transaction&gt;() — สร้าง list ว่างเริ่มต้นเพื่อป้องกัน null reference
    /// </summary>
    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
}
