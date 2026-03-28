
namespace Banking.Domain.Enums;

// ประเภทบัญชี — ลูกค้าเปิดบัญชีอะไรได้บ้าง
public enum AccountType
{
    Savings,        // ออมทรัพย์ — ดอกเบี้ยสูง, ถอนจำกัด
    Checking,       // กระแสรายวัน — ไม่มีดอกเบี้ย, ถอนไม่จำกัด
    FixedDeposit    // ฝากประจำ — ดอกเบี้ยสูงสุด, ถอนก่อนกำหนดเสียค่าปรับ
}

// สถานะบัญชี
public enum AccountStatus
{
    Active,   // ใช้งานปกติ
    Frozen,   // ถูกระงับ (ต้องสอบ, หรือมีปัญหาทางกฎหมาย)
    Closed    // ปิดบัญชีแล้ว
}

// ประเภทธุรกรรม — ทุกการเคลื่อนไหวของเงิน
public enum TransactionType
{
    Deposit,        // ฝากเงิน (เงินเข้า)
    Withdrawal,     // ถอนเงิน (เงินออก)
    TransferIn,     // รับโอน (เงินเข้าจากบัญชีอื่น)
    TransferOut,    // โอนออก (เงินออกไปบัญชีอื่น)
    Fee,            // ค่าธรรมเนียม (ถูกหัก)
    Interest        // ดอกเบี้ย (ได้รับ)
}

// สถานะธุรกรรม — ติดตามว่าธุรกรรมอยู่ step ไหน
public enum TransactionStatus
{
    Pending,      // รอดำเนินการ (เพิ่งสร้าง)
    Processing,   // กำลังประมวลผล (อยู่ระหว่างทำ)
    Completed,    // สำเร็จ
    Failed,       // ล้มเหลว (เงินไม่พอ, ระบบ error)
    Reversed      // ถูกย้อนกลับ (ยกเลิกธุรกรรม)
}

// สถานะยืนยันตัวตน (KYC = Know Your Customer)
// ระบบธนาคารจริงต้องยืนยันตัวตนก่อนใช้งาน (กฎหมาย!)
public enum KycStatus
{
    Pending,    // รอตรวจสอบ
    Verified,   // ผ่านแล้ว
    Rejected    // ไม่ผ่าน
}