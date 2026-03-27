# Scaling Databases — Indexes, Replication, Sharding, CAP Theorem

---

## 1. Database Indexes

### Index คืออะไร

Index คือ **โครงสร้างข้อมูลเสริมที่ช่วยให้ค้นหาเร็วขึ้น** เหมือนดัชนีท้ายเล่มหนังสือ

```
ไม่มี Index:
SELECT * FROM users WHERE email = 'john@test.com'
→ ต้องอ่านทุกแถว (Full Table Scan) → O(n) → ช้ามากเมื่อข้อมูลเยอะ

มี Index:
CREATE INDEX idx_users_email ON users(email);
→ ใช้ B-Tree ค้นหา → O(log n) → เร็วมาก!

1 ล้านแถว:
- ไม่มี Index: สแกน 1,000,000 แถว
- มี Index: อ่านแค่ ~20 nodes (log₂ 1,000,000 ≈ 20)
```

### ประเภท Index

```sql
-- Single Column Index
CREATE INDEX idx_users_email ON users(email);

-- Composite Index (หลาย columns)
CREATE INDEX idx_orders_user_date ON orders(user_id, created_at);
-- ลำดับสำคัญ! ใช้ได้กับ WHERE user_id = ? AND created_at > ?
-- ใช้ได้กับ WHERE user_id = ?
-- ใช้ไม่ได้กับ WHERE created_at > ? (ต้องมี user_id ก่อน)

-- Unique Index
CREATE UNIQUE INDEX idx_users_email ON users(email);

-- Partial Index (เฉพาะบาง rows)
CREATE INDEX idx_active_users ON users(email) WHERE is_active = true;

-- เมื่อไหร่ควรสร้าง Index:
-- ✅ columns ที่ใช้ใน WHERE บ่อย
-- ✅ columns ที่ใช้ใน JOIN
-- ✅ columns ที่ใช้ใน ORDER BY

-- ❌ ไม่ควรสร้าง Index:
-- ❌ ตารางเล็กมาก (< 1000 rows)
-- ❌ columns ที่มีค่าซ้ำเยอะ (เช่น gender)
-- ❌ ตารางที่ write เยอะมาก (index ทำให้ write ช้า)
```

---

## 2. Data Replication

```
Replication = สำเนาข้อมูลไปหลายเครื่อง

Master-Replica:
┌────────┐     ┌──────────┐
│ Master │ ──→ │ Replica 1│  ← อ่านจาก Replica (scale reads)
│ (Write)│ ──→ │ Replica 2│
│        │ ──→ │ Replica 3│
└────────┘     └──────────┘

การทำงาน:
- Write ไปที่ Master เท่านั้น
- Master ส่งข้อมูลไปทุก Replicas (async หรือ sync)
- Read จาก Replicas (กระจาย load)

ข้อดี:
✅ Scale reads (เพิ่ม replicas)
✅ High availability (master ล่ม → promote replica)
✅ Backup อัตโนมัติ

ข้อเสีย:
❌ Replication lag (replica อาจตามไม่ทัน)
❌ Write ยัง bottleneck ที่ master
❌ ซับซ้อนขึ้น
```

---

## 3. Sharding (Partitioning)

```
Sharding = แบ่งข้อมูลออกเป็นส่วนๆ เก็บคนละเครื่อง

ก่อน Sharding:
┌──────────────────┐
│  1 Database      │  ← ข้อมูล 100 ล้านแถว (ช้า!)
│  100M rows       │
└──────────────────┘

หลัง Sharding:
┌──────────┐  ┌──────────┐  ┌──────────┐
│ Shard 1  │  │ Shard 2  │  │ Shard 3  │
│ User A-H │  │ User I-P │  │ User Q-Z │
│ 33M rows │  │ 33M rows │  │ 34M rows │
└──────────┘  └──────────┘  └──────────┘

วิธี Shard:
1. Range-based: user_id 1-1M → Shard 1, 1M-2M → Shard 2
   ❌ อาจ uneven (shard 1 ข้อมูลเยอะกว่า)

2. Hash-based: hash(user_id) % 3 → Shard 0/1/2
   ✅ กระจายสม่ำเสมอ
   ❌ เพิ่ม shard ยาก (ต้อง rehash)

3. Directory-based: lookup table บอกว่า key ไหนอยู่ shard ไหน
   ✅ ยืดหยุ่น
   ❌ lookup table เป็น single point of failure

ข้อเสีย Sharding:
❌ JOIN ข้าม shards ยากมาก
❌ Transaction ข้าม shards ซับซ้อน
❌ เพิ่ม/ลด shards ลำบาก
❌ ซับซ้อนในการจัดการ

กฎ: หลีกเลี่ยง sharding ให้นานที่สุด
ลอง: indexing, caching, read replicas, vertical scaling ก่อน
```

---

## 4. CAP Theorem

```
ระบบ distributed สามารถมีได้แค่ 2 ใน 3:

C — Consistency:  ทุก node เห็นข้อมูลเหมือนกัน ณ เวลาเดียวกัน
A — Availability: ทุก request ได้รับ response (แม้ข้อมูลอาจเก่า)
P — Partition Tolerance: ระบบทำงานได้แม้ network ระหว่าง nodes มีปัญหา

       C
      ╱ ╲
     ╱   ╲
   CA    CP
   ╱      ╲
  A ────── P
      AP

ตัวอย่าง:
- CP (Consistency + Partition): MongoDB, Redis, HBase
  → ถ้า network มีปัญหา → ปฏิเสธ request (ไม่ available)
  → แต่ข้อมูลที่ได้ถูกต้องเสมอ

- AP (Availability + Partition): Cassandra, DynamoDB, CouchDB
  → ตอบทุก request เสมอ
  → แต่ข้อมูลอาจเก่า (eventually consistent)

- CA (Consistency + Availability): PostgreSQL (single node)
  → ข้อมูลถูกต้อง + ตอบทุกครั้ง
  → แต่ถ้า network แบ่ง → ทำงานไม่ได้

ในความเป็นจริง:
P (Partition Tolerance) หลีกเลี่ยงไม่ได้ในระบบ distributed
จึงต้องเลือกระหว่าง CP หรือ AP:
- ข้อมูลต้องถูกต้องเสมอ (banking)? → CP
- ต้อง available ตลอด (social media)? → AP
```
