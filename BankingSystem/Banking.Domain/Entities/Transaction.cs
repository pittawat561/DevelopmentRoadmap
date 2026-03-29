using Banking.Domain.Enums;

namespace Banking.Domain.Entities;

/// <summary>
/// Entity สำหรับเก็บข้อมูลธุรกรรมทางการเงิน (ฝาก, ถอน, โอน, ค่าธรรมเนียม, ดอกเบี้ย)
/// สืบทอดจาก BaseEntity เพื่อได้รับ Id, CreatedAt, UpdatedAt, IsDeleted
/// ทุกการเคลื่อนไหวของเงินจะถูกบันทึกเป็น Transaction — เป็นหัวใจของระบบธนาคาร
/// ธุรกรรม 1 รายการเชื่อมกับ Account 1 บัญชี (Many-to-One)
/// </summary>
public class Transaction : BaseEntity
{
    /// <summary>
    /// เลขอ้างอิงธุรกรรม เช่น "TXN-20260329-001"
    /// ใช้เป็น unique identifier ที่มนุษย์อ่านได้ (แตกต่างจาก Id ที่เป็น Guid)
    /// มี unique index ใน database — เลขอ้างอิงห้ามซ้ำกัน
    /// </summary>
    public string ReferenceNumber { get; set; } = string.Empty;

    /// <summary>
    /// รหัสบัญชีที่เกี่ยวข้องกับธุรกรรมนี้ — Foreign Key เชื่อมไปยังตาราง Accounts
    /// </summary>
    public Guid AccountId { get; set; }

    /// <summary>
    /// ประเภทธุรกรรม — Deposit (ฝาก), Withdrawal (ถอน), TransferIn (รับโอน),
    /// TransferOut (โอนออก), Fee (ค่าธรรมเนียม), Interest (ดอกเบี้ย)
    /// </summary>
    public TransactionType Type { get; set; }

    /// <summary>
    /// จำนวนเงินของธุรกรรม (เป็นค่าบวกเสมอ)
    /// ใช้ decimal เพื่อความแม่นยำในการคำนวณเงิน
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// ยอดเงินคงเหลือ "ก่อน" ทำธุรกรรม
    /// เก็บไว้เพื่อใช้ตรวจสอบย้อนหลัง (Audit Trail)
    /// เช่น BalanceBefore = 10,000, Amount = 5,000 (ฝาก) → BalanceAfter = 15,000
    /// </summary>
    public decimal BalanceBefore { get; set; }

    /// <summary>
    /// ยอดเงินคงเหลือ "หลัง" ทำธุรกรรม
    /// BalanceAfter = BalanceBefore ± Amount (ขึ้นอยู่กับประเภทธุรกรรม)
    /// </summary>
    public decimal BalanceAfter { get; set; }

    /// <summary>
    /// สถานะของธุรกรรม — Pending (รอ), Processing (กำลังทำ), Completed (สำเร็จ),
    /// Failed (ล้มเหลว), Reversed (ย้อนกลับ)
    /// TransactionStatus.Pending — ค่าเริ่มต้นเป็น "รอดำเนินการ"
    /// </summary>
    public TransactionStatus Status { get; set; } = TransactionStatus.Pending;

    /// <summary>
    /// คำอธิบายธุรกรรม เช่น "ฝากเงินสด", "โอนค่าสินค้า"
    /// เป็น nullable เพราะไม่จำเป็นต้องมีคำอธิบายทุกครั้ง
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// รหัสธุรกรรมที่เกี่ยวข้อง — ใช้เชื่อมคู่ธุรกรรมในกรณีโอนเงิน
    /// เช่น โอนเงิน = TransferOut (ฝั่งผู้โอน) + TransferIn (ฝั่งผู้รับ)
    /// TransferOut จะมี RelatedTransactionId ชี้ไปที่ TransferIn และกลับกัน
    /// เป็น nullable เพราะธุรกรรมบางประเภท (ฝาก, ถอน) ไม่มีคู่
    /// </summary>
    public Guid? RelatedTransactionId { get; set; }

    /// <summary>
    /// ข้อมูลเพิ่มเติมในรูปแบบ JSON string
    /// ใช้เก็บข้อมูลที่ไม่มีโครงสร้างตายตัว เช่น ช่องทางการทำรายการ, ข้อมูลเครื่อง ATM
    /// เป็น nullable เพราะไม่จำเป็นต้องมีข้อมูลเพิ่มเติมทุกครั้ง
    /// </summary>
    public string? Metadata { get; set; }

    /// <summary>
    /// IP Address ของผู้ทำรายการ — ใช้ตรวจสอบความปลอดภัยและติดตามย้อนหลัง
    /// เป็น nullable เพราะบางธุรกรรม (เช่น ดอกเบี้ย) เกิดจากระบบ ไม่มี IP
    /// </summary>
    public string? IpAddress { get; set; }

    // === Navigation Properties ===

    /// <summary>
    /// บัญชีที่เป็นเจ้าของธุรกรรมนี้ — Navigation Property แบบ Many-to-One
    /// null! — บอก compiler ว่าจะไม่เป็น null เพราะ EF Core จะโหลดให้
    /// ใช้เข้าถึงข้อมูลบัญชี เช่น transaction.Account.AccountNumber
    /// </summary>
    public Account Account { get; set; } = null!;

    /// <summary>
    /// ธุรกรรมคู่ที่เกี่ยวข้อง — Navigation Property สำหรับ Self-Referencing Relationship
    /// ใช้ในกรณีโอนเงิน: TransferOut ↔ TransferIn เชื่อมกันผ่าน property นี้
    /// เป็น nullable เพราะไม่ใช่ทุกธุรกรรมจะมีคู่
    /// </summary>
    public Transaction? RelatedTransaction { get; set; }
}
