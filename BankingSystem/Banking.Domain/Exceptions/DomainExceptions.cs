namespace Banking.Domain.Exceptions;

/// <summary>
/// Exception สำหรับกรณีค้นหาข้อมูลไม่เจอ (เช่น User หรือ Account ที่ระบุ Id ไม่มีในระบบ)
/// สืบทอดจาก Exception เพื่อใช้เป็น custom exception ของระบบ
/// ใช้ throw เมื่อเรียก GetById แล้วไม่พบ record ที่ต้องการ
/// </summary>
public class NotFoundException : Exception
{
    /// <summary>
    /// Constructor รับชื่อ Entity และ Id ที่ค้นหา แล้วสร้างข้อความ error อัตโนมัติ
    /// base($"...") — ส่งข้อความไปให้ constructor ของ Exception (parent class)
    /// string interpolation ($"") — แทรกค่า entity และ id ลงในข้อความ
    /// เช่น NotFoundException("User", guid) → "User with id 'xxx' was not found."
    /// </summary>
    /// <param name="entity">ชื่อ Entity ที่ค้นหา เช่น "User", "Account"</param>
    /// <param name="id">รหัสที่ใช้ค้นหา (Guid หรือค่าอื่น)</param>
    public NotFoundException(string entity, object id)
        : base($"{entity} with id '{id}' was not found.") { }
}

/// <summary>
/// Exception สำหรับกรณีเงินไม่พอ (ถอนหรือโอนเกินยอดคงเหลือ)
/// ใช้ throw เมื่อ Amount ที่ร้องขอมากกว่า Balance ที่มี
/// </summary>
public class InsufficientFundsException : Exception
{
    /// <summary>
    /// Constructor รับยอดคงเหลือและจำนวนที่ร้องขอ แล้วสร้างข้อความ error อัตโนมัติ
    /// base($"...") — ส่งข้อความไปให้ constructor ของ Exception
    /// {balance:N2} — format ตัวเลขให้มี comma คั่นหลักพัน และทศนิยม 2 ตำแหน่ง
    /// เช่น 10000 → "10,000.00"
    /// </summary>
    /// <param name="balance">ยอดเงินคงเหลือในบัญชี</param>
    /// <param name="amount">จำนวนเงินที่ร้องขอถอน/โอน</param>
    public InsufficientFundsException(decimal balance, decimal amount)
        : base($"Insufficient funds. Balance: {balance:N2}, Requested: {amount:N2}") { }
}

/// <summary>
/// Exception สำหรับกรณีบัญชีถูกระงับ (Frozen) — ห้ามทำธุรกรรมใดๆ
/// ใช้ throw เมื่อพยายามทำธุรกรรมกับบัญชีที่มีสถานะ Frozen
/// บัญชีอาจถูก Freeze เนื่องจากต้องสงสัยทุจริต หรือมีปัญหาทางกฎหมาย
/// </summary>
public class AccountFrozenException : Exception
{
    /// <summary>
    /// Constructor รับเลขบัญชีที่ถูกระงับ แล้วสร้างข้อความ error อัตโนมัติ
    /// </summary>
    /// <param name="accountNumber">เลขที่บัญชีที่ถูกระงับ</param>
    public AccountFrozenException(string accountNumber)
        : base($"Account '{accountNumber}' is frozen.") { }
}

/// <summary>
/// Exception สำหรับกรณีถอนเกินวงเงินต่อวัน (Daily Withdrawal Limit)
/// ใช้ throw เมื่อยอดถอนรวมวันนี้ + จำนวนที่ร้องขอ เกินวงเงินที่กำหนด
/// เป็นมาตรการป้องกันความปลอดภัย — จำกัดความเสียหายกรณีบัญชีถูกขโมย
/// </summary>
public class DailyLimitExceededException : Exception
{
    /// <summary>
    /// Constructor รับวงเงินต่อวัน, ยอดที่ถอนไปแล้ววันนี้, และจำนวนที่ร้องขอ
    /// แล้วสร้างข้อความ error อัตโนมัติ
    /// </summary>
    /// <param name="limit">วงเงินถอนสูงสุดต่อวัน</param>
    /// <param name="todayTotal">ยอดรวมที่ถอนไปแล้ววันนี้</param>
    /// <param name="requested">จำนวนเงินที่ร้องขอถอน</param>
    public DailyLimitExceededException(decimal limit, decimal todayTotal, decimal requested)
        : base($"Daily limit exceeded. Limit: {limit:N2}, Today: {todayTotal:N2}, Requested: {requested:N2}") { }
}

/// <summary>
/// Exception สำหรับกรณีข้อมูลซ้ำ (Duplicate)
/// ใช้ throw เมื่อพยายามสร้างข้อมูลที่ซ้ำกับที่มีอยู่แล้ว เช่น อีเมลซ้ำ, เลขบัญชีซ้ำ
/// </summary>
public class DuplicateException : Exception
{
    /// <summary>
    /// Constructor รับข้อความ error ที่กำหนดเอง
    /// base(message) — ส่งข้อความไปให้ constructor ของ Exception
    /// </summary>
    /// <param name="message">ข้อความอธิบายว่าข้อมูลอะไรซ้ำ</param>
    public DuplicateException(string message) : base(message) { }
}

/// <summary>
/// Exception สำหรับกรณีบัญชีถูกล็อค (Locked)
/// ใช้ throw เมื่อผู้ใช้พยายาม login แต่บัญชีถูกล็อคเนื่องจากกรอกรหัสผ่านผิดหลายครั้ง
/// </summary>
public class AccountLockedException : Exception
{
    /// <summary>
    /// Constructor ไม่รับ parameter — ใช้ข้อความ error คงที่
    /// base("...") — ส่งข้อความ "Account is locked due to too many failed attempts." ไปให้ Exception
    /// </summary>
    public AccountLockedException() : base("Account is locked due to too many failed attempts.") { }
}
