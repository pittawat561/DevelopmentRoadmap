using Banking.Domain.Entities;

namespace Banking.Domain.Interfaces;

/// <summary>
/// Generic Repository Interface — กำหนด method พื้นฐานสำหรับ CRUD operation
/// ใช้ Generic Type T ที่ต้องเป็น BaseEntity (where T : BaseEntity)
/// ทุก Repository เช่น IUserRepository, IAccountRepository จะสืบทอดจาก interface นี้
/// เพื่อให้มี method มาตรฐาน (GetById, GetAll, Add, Update, Remove) ครบทุกตัว
/// </summary>
/// <typeparam name="T">ประเภท Entity ที่ต้องสืบทอดจาก BaseEntity</typeparam>
public interface IRepository<T> where T : BaseEntity
{
    /// <summary>
    /// ค้นหา Entity ตาม Id (Primary Key)
    /// คืนค่า T? (nullable) — ถ้าไม่เจอจะคืน null
    /// เป็น async method (Task) เพราะต้องรอ query จาก database
    /// CancellationToken ct — ใช้ยกเลิก operation ได้ถ้าผู้ใช้ cancel request
    /// </summary>
    /// <param name="id">รหัส Guid ของ Entity ที่ต้องการค้นหา</param>
    /// <param name="ct">CancellationToken สำหรับยกเลิก operation</param>
    /// <returns>Entity ที่พบ หรือ null ถ้าไม่พบ</returns>
    Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// ดึง Entity ทั้งหมดจาก database (ที่ไม่ถูก Soft Delete)
    /// คืนค่าเป็น List&lt;T&gt; — รายการ Entity ทั้งหมด
    /// QueryFilter ของ EF Core จะกรอง IsDeleted == true ออกอัตโนมัติ
    /// </summary>
    /// <param name="ct">CancellationToken สำหรับยกเลิก operation</param>
    /// <returns>รายการ Entity ทั้งหมด</returns>
    Task<List<T>> GetAllAsync(CancellationToken ct = default);

    /// <summary>
    /// เพิ่ม Entity ใหม่เข้าไปใน database
    /// ยังไม่บันทึกจริงจนกว่าจะเรียก SaveChangesAsync() ผ่าน UnitOfWork
    /// </summary>
    /// <param name="entity">Entity ที่ต้องการเพิ่ม</param>
    /// <param name="ct">CancellationToken สำหรับยกเลิก operation</param>
    Task AddAsync(T entity, CancellationToken ct = default);

    /// <summary>
    /// อัปเดต Entity ที่มีอยู่แล้วใน database
    /// EF Core จะ track การเปลี่ยนแปลงและบันทึกเมื่อเรียก SaveChangesAsync()
    /// ไม่เป็น async เพราะแค่ mark Entity ว่า Modified (ไม่ได้ query database)
    /// </summary>
    /// <param name="entity">Entity ที่ต้องการอัปเดต</param>
    void Update(T entity);

    /// <summary>
    /// ลบ Entity ออกจาก database (อาจเป็น Soft Delete หรือ Hard Delete ขึ้นกับ implementation)
    /// ไม่เป็น async เพราะแค่ mark Entity ว่า Deleted (ไม่ได้ query database)
    /// </summary>
    /// <param name="entity">Entity ที่ต้องการลบ</param>
    void Remove(T entity);
}

/// <summary>
/// Repository Interface เฉพาะสำหรับ User — สืบทอดจาก IRepository&lt;User&gt;
/// นอกจาก method พื้นฐาน (GetById, GetAll, Add, Update, Remove) แล้ว
/// ยังมี method เฉพาะสำหรับค้นหาและตรวจสอบผู้ใช้
/// </summary>
public interface IUserRepository : IRepository<User>
{
    /// <summary>
    /// ค้นหาผู้ใช้จากอีเมล — ใช้ตอน login หรือตรวจสอบว่ามีอีเมลนี้ในระบบหรือไม่
    /// คืนค่า User? (nullable) — ถ้าไม่เจอจะคืน null
    /// </summary>
    /// <param name="email">อีเมลที่ต้องการค้นหา</param>
    /// <param name="ct">CancellationToken สำหรับยกเลิก operation</param>
    /// <returns>User ที่พบ หรือ null ถ้าไม่พบ</returns>
    Task<User?> GetByEmailAsync(string email, CancellationToken ct = default);

    /// <summary>
    /// ตรวจสอบว่ามีอีเมลนี้ในระบบแล้วหรือไม่ — ใช้ตอนสมัครสมาชิกใหม่
    /// คืนค่า bool — true = มีอยู่แล้ว (ซ้ำ), false = ยังไม่มี (ใช้ได้)
    /// เร็วกว่า GetByEmailAsync เพราะไม่ต้องโหลดข้อมูลทั้งหมด แค่เช็ค EXISTS
    /// </summary>
    /// <param name="email">อีเมลที่ต้องการตรวจสอบ</param>
    /// <param name="ct">CancellationToken สำหรับยกเลิก operation</param>
    /// <returns>true ถ้ามีอีเมลนี้ในระบบแล้ว</returns>
    Task<bool> EmailExistsAsync(string email, CancellationToken ct = default);

    /// <summary>
    /// ตรวจสอบว่ามีเบอร์โทรนี้ในระบบแล้วหรือไม่ — ใช้ตอนสมัครสมาชิกใหม่
    /// คืนค่า bool — true = มีอยู่แล้ว (ซ้ำ), false = ยังไม่มี (ใช้ได้)
    /// </summary>
    /// <param name="phone">เบอร์โทรที่ต้องการตรวจสอบ</param>
    /// <param name="ct">CancellationToken สำหรับยกเลิก operation</param>
    /// <returns>true ถ้ามีเบอร์โทรนี้ในระบบแล้ว</returns>
    Task<bool> PhoneExistsAsync(string phone, CancellationToken ct = default);
}

/// <summary>
/// Repository Interface เฉพาะสำหรับ Account — สืบทอดจาก IRepository&lt;Account&gt;
/// มี method เฉพาะสำหรับค้นหาบัญชีจากเลขบัญชี, รหัสผู้ใช้, และ lock สำหรับ update
/// </summary>
public interface IAccountRepository : IRepository<Account>
{
    /// <summary>
    /// ค้นหาบัญชีจากเลขบัญชี เช่น "1234-5678-9012"
    /// ใช้ตอนโอนเงิน (ผู้ใช้กรอกเลขบัญชีปลายทาง)
    /// </summary>
    /// <param name="accountNumber">เลขที่บัญชีที่ต้องการค้นหา</param>
    /// <param name="ct">CancellationToken สำหรับยกเลิก operation</param>
    /// <returns>Account ที่พบ หรือ null ถ้าไม่พบ</returns>
    Task<Account?> GetByAccountNumberAsync(string accountNumber, CancellationToken ct = default);

    /// <summary>
    /// ดึงบัญชีทั้งหมดของผู้ใช้คนหนึ่ง — ใช้แสดงรายการบัญชีในหน้า Dashboard
    /// คืนค่าเป็น List เพราะผู้ใช้ 1 คนมีได้หลายบัญชี
    /// </summary>
    /// <param name="userId">รหัสผู้ใช้ที่ต้องการดูบัญชี</param>
    /// <param name="ct">CancellationToken สำหรับยกเลิก operation</param>
    /// <returns>รายการบัญชีทั้งหมดของผู้ใช้</returns>
    Task<List<Account>> GetByUserIdAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// ดึงบัญชีพร้อม lock สำหรับ update — ป้องกัน Race Condition
    /// ใช้ตอนทำธุรกรรม (ฝาก, ถอน, โอน) เพื่อให้แน่ใจว่าไม่มี request อื่นแก้ไข Balance พร้อมกัน
    /// ใช้ database-level lock (SELECT ... FOR UPDATE) เพื่อความปลอดภัย
    /// </summary>
    /// <param name="id">รหัสบัญชีที่ต้องการ lock</param>
    /// <param name="ct">CancellationToken สำหรับยกเลิก operation</param>
    /// <returns>Account ที่ถูก lock หรือ null ถ้าไม่พบ</returns>
    Task<Account?> GetByIdForUpdateAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// ตรวจสอบว่ามีเลขบัญชีนี้ในระบบแล้วหรือไม่ — ใช้ตอนสร้างบัญชีใหม่
    /// ป้องกันเลขบัญชีซ้ำ
    /// </summary>
    /// <param name="accountNumber">เลขบัญชีที่ต้องการตรวจสอบ</param>
    /// <param name="ct">CancellationToken สำหรับยกเลิก operation</param>
    /// <returns>true ถ้ามีเลขบัญชีนี้ในระบบแล้ว</returns>
    Task<bool> AccountNumberExistsAsync(string accountNumber, CancellationToken ct = default);
}

/// <summary>
/// Repository Interface เฉพาะสำหรับ Transaction — สืบทอดจาก IRepository&lt;Transaction&gt;
/// มี method เฉพาะสำหรับดึงธุรกรรมแบบ pagination และคำนวณยอดถอนรวมวันนี้
/// </summary>
public interface ITransactionRepository : IRepository<Transaction>
{
    /// <summary>
    /// ดึงรายการธุรกรรมของบัญชีแบบแบ่งหน้า (Pagination)
    /// ใช้แสดง Statement หรือประวัติธุรกรรมในหน้า UI
    /// page และ pageSize — กำหนดว่าจะดึงหน้าที่เท่าไหร่ และหน้าละกี่รายการ
    /// เช่น page=1, pageSize=20 → ดึง 20 รายการแรก
    /// </summary>
    /// <param name="accountId">รหัสบัญชีที่ต้องการดูธุรกรรม</param>
    /// <param name="page">หมายเลขหน้า (เริ่มจาก 1)</param>
    /// <param name="pageSize">จำนวนรายการต่อหน้า</param>
    /// <param name="ct">CancellationToken สำหรับยกเลิก operation</param>
    /// <returns>รายการธุรกรรมในหน้าที่ระบุ</returns>
    Task<List<Transaction>> GetByAccountIdAsync(Guid accountId, int page, int pageSize, CancellationToken ct = default);

    /// <summary>
    /// นับจำนวนธุรกรรมทั้งหมดของบัญชี — ใช้คำนวณจำนวนหน้าสำหรับ Pagination
    /// เช่น มี 100 ธุรกรรม, pageSize=20 → จำนวนหน้า = 100/20 = 5 หน้า
    /// </summary>
    /// <param name="accountId">รหัสบัญชีที่ต้องการนับธุรกรรม</param>
    /// <param name="ct">CancellationToken สำหรับยกเลิก operation</param>
    /// <returns>จำนวนธุรกรรมทั้งหมด</returns>
    Task<int> GetCountByAccountIdAsync(Guid accountId, CancellationToken ct = default);

    /// <summary>
    /// คำนวณยอดถอนเงินรวมของวันนี้ — ใช้ตรวจสอบว่าเกินวงเงินถอนต่อวันหรือไม่
    /// รวมเฉพาะธุรกรรมประเภท Withdrawal และ TransferOut ที่สำเร็จ (Completed) ของวันนี้
    /// ถ้ายอดรวม + จำนวนที่ร้องขอ > DailyWithdrawalLimit → throw DailyLimitExceededException
    /// </summary>
    /// <param name="accountId">รหัสบัญชีที่ต้องการตรวจสอบ</param>
    /// <param name="ct">CancellationToken สำหรับยกเลิก operation</param>
    /// <returns>ยอดเงินรวมที่ถอนไปแล้ววันนี้</returns>
    Task<decimal> GetTodayWithdrawalTotalAsync(Guid accountId, CancellationToken ct = default);
}

/// <summary>
/// Unit of Work Interface — รวม Repository ทั้งหมดไว้ในที่เดียว
/// ใช้จัดการ Database Transaction เพื่อให้การทำงานหลาย operation เป็น Atomic
/// (สำเร็จทั้งหมดหรือล้มเหลวทั้งหมด — ไม่ทำครึ่งๆ กลางๆ)
///
/// ตัวอย่าง: การโอนเงินต้อง
///   1. หักเงินจากบัญชี A
///   2. เพิ่มเงินเข้าบัญชี B
///   3. สร้าง Transaction 2 รายการ
///   4. สร้าง Transfer 1 รายการ
/// ถ้า step 2 ล้มเหลว → Rollback ทั้งหมด (เงินบัญชี A กลับมาเหมือนเดิม)
/// </summary>
public interface IUnitOfWork
{
    /// <summary>
    /// Repository สำหรับจัดการข้อมูล User
    /// ใช้เข้าถึง method เช่น GetByEmailAsync, EmailExistsAsync
    /// </summary>
    IUserRepository Users { get; }

    /// <summary>
    /// Repository สำหรับจัดการข้อมูล Account
    /// ใช้เข้าถึง method เช่น GetByAccountNumberAsync, GetByIdForUpdateAsync
    /// </summary>
    IAccountRepository Accounts { get; }

    /// <summary>
    /// Repository สำหรับจัดการข้อมูล Transaction
    /// ใช้เข้าถึง method เช่น GetByAccountIdAsync, GetTodayWithdrawalTotalAsync
    /// </summary>
    ITransactionRepository Transactions { get; }

    /// <summary>
    /// บันทึกการเปลี่ยนแปลงทั้งหมดลง database
    /// EF Core จะรวบรวมทุก Add/Update/Remove ที่ทำไว้แล้วส่งไป database ทีเดียว
    /// คืนค่า int — จำนวน record ที่ถูกเปลี่ยนแปลง
    /// </summary>
    /// <param name="ct">CancellationToken สำหรับยกเลิก operation</param>
    /// <returns>จำนวน record ที่ถูกเปลี่ยนแปลง</returns>
    Task<int> SaveChangesAsync(CancellationToken ct = default);

    /// <summary>
    /// เริ่ม Database Transaction — ทุก operation หลังจากนี้จะอยู่ใน transaction เดียวกัน
    /// ต้องเรียก CommitTransactionAsync() เมื่อสำเร็จ หรือ RollbackTransactionAsync() เมื่อล้มเหลว
    /// </summary>
    /// <param name="ct">CancellationToken สำหรับยกเลิก operation</param>
    Task BeginTransactionAsync(CancellationToken ct = default);

    /// <summary>
    /// ยืนยัน (Commit) Database Transaction — บันทึกทุกอย่างลง database อย่างถาวร
    /// เรียกเมื่อทุก operation ใน transaction สำเร็จทั้งหมด
    /// </summary>
    /// <param name="ct">CancellationToken สำหรับยกเลิก operation</param>
    Task CommitTransactionAsync(CancellationToken ct = default);

    /// <summary>
    /// ยกเลิก (Rollback) Database Transaction — ย้อนกลับทุกอย่างที่ทำใน transaction นี้
    /// เรียกเมื่อมี operation ใด operation หนึ่งล้มเหลว
    /// ทุกการเปลี่ยนแปลงจะถูกยกเลิก เหมือนไม่เคยเกิดขึ้น
    /// </summary>
    /// <param name="ct">CancellationToken สำหรับยกเลิก operation</param>
    Task RollbackTransactionAsync(CancellationToken ct = default);
}
