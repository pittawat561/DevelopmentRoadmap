# More about Databases — ฐานข้อมูลขั้นสูง

> ครอบคลุม Transactions, ACID, ORMs, Normalization, Failure Modes, Profiling Performance

---

## 1. Transactions — ธุรกรรมฐานข้อมูล

### Transaction คืออะไร

Transaction คือ **กลุ่มของ operations ที่ต้องสำเร็จทั้งหมดหรือไม่สำเร็จเลย** (all or nothing)

```
ตัวอย่าง: โอนเงิน 1000 บาท จาก A ไป B

ต้องทำ 2 อย่างพร้อมกัน:
1. หัก A  -1000 บาท
2. เพิ่ม B +1000 บาท

ถ้าข้อ 1 สำเร็จ แต่ข้อ 2 ล้มเหลว → เงิน A หายไป 1000 แต่ B ไม่ได้!
Transaction ป้องกันปัญหานี้ — ถ้าอันใดอันหนึ่งล้มเหลว → ย้อนทั้งหมดกลับ
```

### SQL Transactions

```sql
-- เริ่ม Transaction
BEGIN TRANSACTION;

-- หัก A
UPDATE accounts SET balance = balance - 1000 WHERE id = 1;

-- เพิ่ม B
UPDATE accounts SET balance = balance + 1000 WHERE id = 2;

-- ถ้าทุกอย่าง OK → ยืนยัน
COMMIT;

-- ถ้ามีปัญหา → ยกเลิกทั้งหมด
-- ROLLBACK;
```

### Code Examples

```javascript
// Node.js (Knex.js)
async function transferMoney(fromId, toId, amount) {
  const trx = await knex.transaction()  // เริ่ม transaction

  try {
    // หัก A
    await trx('accounts')
      .where({ id: fromId })
      .decrement('balance', amount)

    // เช็คว่า A มีเงินพอไหม
    const sender = await trx('accounts').where({ id: fromId }).first()
    if (sender.balance < 0) {
      throw new Error('Insufficient funds')
    }

    // เพิ่ม B
    await trx('accounts')
      .where({ id: toId })
      .increment('balance', amount)

    // บันทึกประวัติ
    await trx('transactions').insert({
      from_account: fromId,
      to_account: toId,
      amount: amount,
      created_at: new Date()
    })

    await trx.commit()    // ✅ ยืนยันทั้งหมด
    return { success: true }

  } catch (error) {
    await trx.rollback()  // ❌ ยกเลิกทั้งหมด
    throw error
  }
}
```

```csharp
// C# Entity Framework
public async Task TransferMoney(int fromId, int toId, decimal amount)
{
    using var transaction = await _context.Database.BeginTransactionAsync();

    try
    {
        var sender = await _context.Accounts.FindAsync(fromId);
        var receiver = await _context.Accounts.FindAsync(toId);

        if (sender.Balance < amount)
            throw new InvalidOperationException("Insufficient funds");

        sender.Balance -= amount;
        receiver.Balance += amount;

        _context.Transactions.Add(new Transaction
        {
            FromAccountId = fromId,
            ToAccountId = toId,
            Amount = amount,
            CreatedAt = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();
        await transaction.CommitAsync();    // ✅
    }
    catch
    {
        await transaction.RollbackAsync();  // ❌
        throw;
    }
}
```

---

## 2. ACID — คุณสมบัติของ Transaction

```
A — Atomicity (ทำทั้งหมดหรือไม่ทำเลย)
    โอนเงิน: ต้องหักและเพิ่มทั้งคู่ ถ้าอันใดพลาด = ยกเลิกทั้งหมด

C — Consistency (ข้อมูลถูกต้องเสมอ)
    เงินรวมในระบบก่อนและหลังโอนต้องเท่ากัน
    กฎทุกข้อ (constraints) ต้องผ่านก่อน commit

I — Isolation (แต่ละ transaction ไม่เห็นกัน)
    ถ้า 2 คนโอนเงินพร้อมกัน → แต่ละคนจะทำงานเหมือนเป็นคนเดียวในระบบ

D — Durability (บันทึกแล้วไม่หาย)
    เมื่อ COMMIT สำเร็จ → แม้ไฟดับ ข้อมูลยังอยู่

Isolation Levels (จากเข้มงวดน้อย → มาก):

| Level              | Dirty Read | Non-Repeatable | Phantom |
|-------------------|-----------|---------------|---------|
| Read Uncommitted  | ✅ เกิดได้  | ✅ เกิดได้     | ✅ เกิดได้ |
| Read Committed    | ❌ ป้องกัน  | ✅ เกิดได้     | ✅ เกิดได้ |
| Repeatable Read   | ❌ ป้องกัน  | ❌ ป้องกัน     | ✅ เกิดได้ |
| Serializable      | ❌ ป้องกัน  | ❌ ป้องกัน     | ❌ ป้องกัน |

เข้มงวดมาก = ปลอดภัย แต่ช้า
Default ส่วนใหญ่: Read Committed (สมดุลดี)
```

---

## 3. ORMs — Object-Relational Mapping

### ORM คืออะไร

ORM แปลงระหว่าง **objects ในโค้ด** กับ **tables ใน database** อัตโนมัติ ไม่ต้องเขียน SQL เอง

```
ไม่มี ORM (Raw SQL):
const result = db.query('SELECT * FROM users WHERE id = ?', [1])

มี ORM:
const user = await User.findById(1)    // เขียนเป็นโค้ดปกติ
user.name = "John"
await user.save()                       // ORM สร้าง SQL ให้
```

### ORMs ที่นิยม

```
| ภาษา          | ORM                  | หมายเหตุ             |
|---------------|---------------------|---------------------|
| C# / .NET     | Entity Framework Core| ⭐ มาตรฐาน .NET     |
| JavaScript    | Prisma               | ⭐ Type-safe, ง่าย   |
| JavaScript    | Sequelize            | เก่าแก่, ครบ         |
| JavaScript    | Knex.js              | Query builder (ไม่ใช่ ORM เต็ม) |
| Python        | SQLAlchemy           | ⭐ มาตรฐาน Python   |
| Python        | Django ORM           | มากับ Django         |
| Java          | Hibernate            | ⭐ มาตรฐาน Java     |
```

### ตัวอย่าง: Entity Framework Core

```csharp
// 1. Define Model
public class User
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Email { get; set; }
    public List<Order> Orders { get; set; }  // Navigation property
}

public class Order
{
    public int Id { get; set; }
    public decimal Total { get; set; }
    public int UserId { get; set; }
    public User User { get; set; }
}

// 2. DbContext
public class AppDbContext : DbContext
{
    public DbSet<User> Users { get; set; }
    public DbSet<Order> Orders { get; set; }
}

// 3. CRUD Operations
// Create
var user = new User { Name = "John", Email = "john@test.com" };
context.Users.Add(user);
await context.SaveChangesAsync();

// Read
var user = await context.Users
    .Include(u => u.Orders)            // Eager loading
    .FirstOrDefaultAsync(u => u.Id == 1);

// Update
user.Name = "Jane";
await context.SaveChangesAsync();

// Delete
context.Users.Remove(user);
await context.SaveChangesAsync();

// Query with LINQ
var activeUsers = await context.Users
    .Where(u => u.Orders.Any())
    .OrderBy(u => u.Name)
    .Select(u => new { u.Name, OrderCount = u.Orders.Count })
    .ToListAsync();
```

### ORM vs Raw SQL

```
| หัวข้อ          | ORM                    | Raw SQL               |
|----------------|------------------------|-----------------------|
| ง่าย            | ✅ เขียนเป็นโค้ดปกติ     | ❌ ต้องรู้ SQL         |
| Type Safety     | ✅ compile-time check   | ❌ runtime errors      |
| Performance     | ❌ ช้ากว่าเล็กน้อย      | ✅ ควบคุมได้เต็มที่     |
| Complex Query   | ❌ ยากกับ query ซับซ้อน  | ✅ เขียนได้ทุกอย่าง    |
| Migration       | ✅ มี built-in          | ❌ ต้องจัดการเอง       |
| Portability     | ✅ เปลี่ยน DB ง่าย       | ❌ SQL อาจต่างกัน     |

แนะนำ: ใช้ ORM เป็นหลัก + Raw SQL สำหรับ query ซับซ้อน
```

---

## 4. Normalization — การออกแบบ Database ให้ดี

### Normalization คืออะไร

กระบวนการ **จัดโครงสร้างตารางเพื่อลดข้อมูลซ้ำ** และป้องกัน anomalies

```
❌ ก่อน Normalization (ข้อมูลซ้ำ):
| OrderId | Customer | CustomerEmail    | Product | Price |
|---------|----------|------------------|---------|-------|
| 1       | John     | john@test.com    | Laptop  | 30000 |
| 2       | John     | john@test.com    | Mouse   | 500   |
| 3       | Jane     | jane@test.com    | Laptop  | 30000 |

ปัญหา:
- "John" กับ "john@test.com" ซ้ำ 2 แถว
- ถ้าแก้ email → ต้องแก้ทุกแถว (อาจลืม!)
- ลบ order 3 → ข้อมูล Jane หายไปด้วย

✅ หลัง Normalization:

Users Table:
| UserId | Name | Email           |
|--------|------|-----------------|
| 1      | John | john@test.com   |
| 2      | Jane | jane@test.com   |

Products Table:
| ProductId | Name   | Price |
|-----------|--------|-------|
| 1         | Laptop | 30000 |
| 2         | Mouse  | 500   |

Orders Table:
| OrderId | UserId | ProductId |
|---------|--------|-----------|
| 1       | 1      | 1         |
| 2       | 1      | 2         |
| 3       | 2      | 1         |

→ ไม่มีข้อมูลซ้ำ!
→ แก้ email ที่เดียว ทุก order ได้รับผลอัตโนมัติ
```

### Normal Forms

```
1NF (First Normal Form):
- แต่ละ column มีค่าเดียว (ไม่ใช่ list)
- ❌ tags: "backend, nodejs, redis"
- ✅ แยกเป็นตาราง tags

2NF (Second Normal Form):
- ผ่าน 1NF
- ทุก column ต้องขึ้นกับ Primary Key ทั้งหมด
- ❌ {OrderId, ProductId} → CustomerName (ไม่ขึ้นกับ ProductId)
- ✅ แยก Customer ออกเป็นตารางใหม่

3NF (Third Normal Form):
- ผ่าน 2NF
- ไม่มี transitive dependency
- ❌ UserId → DepartmentId → DepartmentName
- ✅ แยก Department เป็นตารางต่างหาก

ส่วนใหญ่: ทำถึง 3NF ก็เพียงพอ
```

---

## 5. Failure Modes & Profiling Performance

### Database Failure Modes

```
| ปัญหา               | คำอธิบาย                      | วิธีรับมือ                |
|---------------------|------------------------------|--------------------------|
| Connection Timeout  | เชื่อมต่อ DB ไม่ได้            | Connection pooling, retry |
| Deadlock            | 2 transactions รอกันเอง       | ลำดับ operations ให้ตรงกัน |
| Slow Query          | Query ใช้เวลานาน              | Index, optimize query     |
| Disk Full           | พื้นที่เก็บข้อมูลเต็ม           | Monitoring, cleanup       |
| Replication Lag     | Replica ข้อมูลตามไม่ทัน Master | Read from master สำหรับข้อมูลสำคัญ |
| Data Corruption     | ข้อมูลเสียหาย                 | Backup, checksums         |
```

### Profiling & Monitoring

```sql
-- ดู Slow Queries (MySQL)
SET GLOBAL slow_query_log = 'ON';
SET GLOBAL long_query_time = 1;  -- queries ที่ช้ากว่า 1 วินาที

-- ดู Execution Plan (อธิบายว่า DB ทำงานอย่างไร)
EXPLAIN ANALYZE
SELECT u.name, COUNT(o.id) as order_count
FROM users u
LEFT JOIN orders o ON u.id = o.user_id
GROUP BY u.id;

-- ผลลัพธ์บอกว่า:
-- ใช้ index หรือ full table scan?
-- Join แบบไหน?
-- ใช้เวลาเท่าไหร่?

-- สร้าง Index เพื่อเร่งความเร็ว
CREATE INDEX idx_orders_user_id ON orders(user_id);
-- → ค้นหาจาก O(n) เป็น O(log n)
```

```
เครื่องมือ Monitoring:
- pgAdmin (PostgreSQL)
- MySQL Workbench
- Azure Data Studio
- Datadog / New Relic (APM)

สิ่งที่ต้อง monitor:
├── Query execution time
├── Connection pool usage
├── Slow queries
├── Lock/Deadlock count
├── Disk usage
├── Replication lag
└── Cache hit ratio
```
