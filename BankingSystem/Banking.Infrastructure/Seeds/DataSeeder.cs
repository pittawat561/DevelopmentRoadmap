using Banking.Domain.Entities;
using Banking.Domain.Enums;
using Banking.Infrastructure.Data;

namespace Banking.Infrastructure.Seeds;

/// <summary>
/// คลาสสำหรับ Seed (เติม) ข้อมูลเริ่มต้นลง database
/// ใช้ตอน development/testing เพื่อให้มีข้อมูลตัวอย่างสำหรับทดสอบ
/// เป็น static class เพราะไม่ต้องสร้าง instance — เรียกใช้ผ่าน DataSeeder.SeedAsync() ได้เลย
/// </summary>
public static class DataSeeder
{
    /// <summary>
    /// เติมข้อมูลเริ่มต้นลง database — สร้าง User, Account, และ Transaction ตัวอย่าง
    /// เป็น async method (Task) เพราะต้อง query และบันทึกข้อมูลลง database
    ///
    /// ขั้นตอนการทำงาน:
    /// 1. context.Users.Any() — ตรวจสอบว่ามี User อยู่แล้วหรือไม่
    ///    ถ้ามีแล้ว (return true) → จะ return ออกทันที (ไม่ seed ซ้ำ)
    ///    ป้องกันการสร้างข้อมูลซ้ำเมื่อเรียก SeedAsync() หลายครั้ง
    ///
    /// 2. สร้าง Admin User:
    ///    - Guid.NewGuid() — สร้าง Id ใหม่
    ///    - BCrypt.Net.BCrypt.HashPassword("Admin123!") — hash รหัสผ่านด้วย BCrypt
    ///      BCrypt จะสร้าง salt อัตโนมัติและรวมเข้ากับ hash → ปลอดภัยกว่า MD5/SHA
    ///    - KycStatus = KycStatus.Verified — Admin ผ่าน KYC แล้ว
    ///
    /// 3. สร้าง Demo User (สมชาย ใจดี):
    ///    - ข้อมูลตัวอย่างสำหรับทดสอบ ผ่าน KYC แล้วเช่นกัน
    ///
    /// 4. context.Users.AddRange(admin, demoUser) — เพิ่ม User หลายตัวพร้อมกัน
    ///    AddRange() เร็วกว่า Add() ทีละตัว เพราะส่ง INSERT เดียว
    ///    await context.SaveChangesAsync() — บันทึกลง database
    ///
    /// 5. สร้าง Accounts:
    ///    - savingsAccount — บัญชีออมทรัพย์ของสมชาย ยอดเริ่มต้น 100,000 บาท
    ///    - checkingAccount — บัญชีกระแสรายวันของสมชาย ยอดเริ่มต้น 50,000 บาท
    ///    - UserId = demoUser.Id — เชื่อม Account กับ User ผ่าน Foreign Key
    ///    context.Accounts.AddRange() — เพิ่ม Account ทั้ง 2 ตัวพร้อมกัน
    ///
    /// 6. สร้าง Transactions ตัวอย่าง:
    ///    - TXN-SEED-001 — ธุรกรรมฝากเงิน 100,000 บาทเข้าบัญชีออมทรัพย์
    ///    - TXN-SEED-002 — ธุรกรรมฝากเงิน 50,000 บาทเข้าบัญชีกระแสรายวัน
    ///    - BalanceBefore = 0, BalanceAfter = Amount — เป็นการฝากครั้งแรก
    ///    - Status = TransactionStatus.Completed — ธุรกรรมสำเร็จแล้ว
    ///    context.Transactions.AddRange() — เพิ่ม Transaction ทั้งหมดพร้อมกัน
    /// </summary>
    /// <param name="context">AppDbContext สำหรับเข้าถึง database</param>
    public static async Task SeedAsync(AppDbContext context)
    {
        if (context.Users.Any()) return;  // ถ้ามีข้อมูลแล้ว ข้าม

        // สร้าง Admin user
        var admin = new User
        {
            Id = Guid.NewGuid(),
            FirstName = "Admin",
            LastName = "System",
            Email = "admin@banking.com",
            Phone = "0800000001",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("1"),
            KycStatus = KycStatus.Verified,
            IsActive = true
        };

        // สร้าง Demo user
        var demoUser = new User
        {
            Id = Guid.NewGuid(),
            FirstName = "สมชาย",
            LastName = "ใจดี",
            Email = "somchai@demo.com",
            Phone = "0812345678",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Demo123!"),
            KycStatus = KycStatus.Verified,
            IsActive = true
        };

        context.Users.AddRange(admin, demoUser);
        await context.SaveChangesAsync();

        // สร้าง Accounts
        var savingsAccount = new Account
        {
            Id = Guid.NewGuid(),
            UserId = demoUser.Id,
            AccountNumber = "1234-5678-9012",
            Type = AccountType.Savings,
            Currency = "THB",
            Balance = 100_000,
            AvailableBalance = 100_000,
            DailyWithdrawalLimit = 50_000,
            Status = AccountStatus.Active
        };

        var checkingAccount = new Account
        {
            Id = Guid.NewGuid(),
            UserId = demoUser.Id,
            AccountNumber = "9876-5432-1098",
            Type = AccountType.Checking,
            Currency = "THB",
            Balance = 50_000,
            AvailableBalance = 50_000,
            DailyWithdrawalLimit = 100_000,
            Status = AccountStatus.Active
        };

        context.Accounts.AddRange(savingsAccount, checkingAccount);
        await context.SaveChangesAsync();

        // สร้าง Transactions ตัวอย่าง
        var transactions = new List<Transaction>
        {
            new()
            {
                ReferenceNumber = "TXN-SEED-001",
                AccountId = savingsAccount.Id,
                Type = TransactionType.Deposit,
                Amount = 100_000,
                BalanceBefore = 0,
                BalanceAfter = 100_000,
                Status = TransactionStatus.Completed,
                Description = "Initial deposit"
            },
            new()
            {
                ReferenceNumber = "TXN-SEED-002",
                AccountId = checkingAccount.Id,
                Type = TransactionType.Deposit,
                Amount = 50_000,
                BalanceBefore = 0,
                BalanceAfter = 50_000,
                Status = TransactionStatus.Completed,
                Description = "Initial deposit"
            }
        };

        context.Transactions.AddRange(transactions);
        await context.SaveChangesAsync();
    }
}
