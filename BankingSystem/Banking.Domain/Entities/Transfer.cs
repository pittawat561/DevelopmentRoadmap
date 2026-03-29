using Banking.Domain.Enums;

namespace Banking.Domain.Entities;

/// <summary>
/// Entity สำหรับเก็บข้อมูลการโอนเงินระหว่างบัญชี
/// สืบทอดจาก BaseEntity เพื่อได้รับ Id, CreatedAt, UpdatedAt, IsDeleted
/// Transfer 1 รายการจะสร้าง Transaction 2 รายการ:
///   1. DebitTransaction — ธุรกรรมหักเงินจากบัญชีต้นทาง (TransferOut)
///   2. CreditTransaction — ธุรกรรมเพิ่มเงินเข้าบัญชีปลายทาง (TransferIn)
/// </summary>
public class Transfer : BaseEntity
{
    /// <summary>
    /// รหัสบัญชีต้นทาง (ผู้โอน) — Foreign Key เชื่อมไปยังตาราง Accounts
    /// </summary>
    public Guid FromAccountId { get; set; }

    /// <summary>
    /// รหัสบัญชีปลายทาง (ผู้รับโอน) — Foreign Key เชื่อมไปยังตาราง Accounts
    /// </summary>
    public Guid ToAccountId { get; set; }

    /// <summary>
    /// จำนวนเงินที่โอน
    /// ใช้ decimal เพื่อความแม่นยำในการคำนวณเงิน
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// ค่าธรรมเนียมการโอน
    /// ค่าเริ่มต้นเป็น 0 — โอนภายในธนาคารเดียวกันอาจไม่มีค่าธรรมเนียม
    /// กรณีโอนข้ามธนาคารหรือข้ามประเทศจะมีค่าธรรมเนียม
    /// </summary>
    public decimal Fee { get; set; } = 0;

    /// <summary>
    /// สถานะของการโอน — Pending (รอ), Processing (กำลังทำ), Completed (สำเร็จ),
    /// Failed (ล้มเหลว), Reversed (ย้อนกลับ)
    /// TransactionStatus.Pending — ค่าเริ่มต้นเป็น "รอดำเนินการ"
    /// </summary>
    public TransactionStatus Status { get; set; } = TransactionStatus.Pending;

    /// <summary>
    /// รหัสธุรกรรมฝั่งหักเงิน (Debit) — Foreign Key เชื่อมไปยังตาราง Transactions
    /// เป็น nullable เพราะ Transaction จะถูกสร้างทีหลังตอนประมวลผลการโอน
    /// </summary>
    public Guid? DebitTransactionId { get; set; }

    /// <summary>
    /// รหัสธุรกรรมฝั่งเพิ่มเงิน (Credit) — Foreign Key เชื่อมไปยังตาราง Transactions
    /// เป็น nullable เพราะ Transaction จะถูกสร้างทีหลังตอนประมวลผลการโอน
    /// </summary>
    public Guid? CreditTransactionId { get; set; }

    // === Navigation Properties ===

    /// <summary>
    /// บัญชีต้นทาง (ผู้โอน) — Navigation Property
    /// null! — บอก compiler ว่าจะไม่เป็น null เพราะ EF Core จะโหลดให้
    /// ใช้เข้าถึงข้อมูลบัญชีต้นทาง เช่น transfer.FromAccount.AccountNumber
    /// </summary>
    public Account FromAccount { get; set; } = null!;

    /// <summary>
    /// บัญชีปลายทาง (ผู้รับโอน) — Navigation Property
    /// null! — บอก compiler ว่าจะไม่เป็น null เพราะ EF Core จะโหลดให้
    /// ใช้เข้าถึงข้อมูลบัญชีปลายทาง เช่น transfer.ToAccount.AccountNumber
    /// </summary>
    public Account ToAccount { get; set; } = null!;

    /// <summary>
    /// ธุรกรรมฝั่งหักเงิน (Debit Transaction) — Navigation Property
    /// เชื่อมไปยัง Transaction ที่หักเงินจากบัญชีต้นทาง (ประเภท TransferOut)
    /// เป็น nullable เพราะอาจยังไม่ได้สร้าง Transaction ตอนสร้าง Transfer
    /// </summary>
    public Transaction? DebitTransaction { get; set; }

    /// <summary>
    /// ธุรกรรมฝั่งเพิ่มเงิน (Credit Transaction) — Navigation Property
    /// เชื่อมไปยัง Transaction ที่เพิ่มเงินเข้าบัญชีปลายทาง (ประเภท TransferIn)
    /// เป็น nullable เพราะอาจยังไม่ได้สร้าง Transaction ตอนสร้าง Transfer
    /// </summary>
    public Transaction? CreditTransaction { get; set; }
}
