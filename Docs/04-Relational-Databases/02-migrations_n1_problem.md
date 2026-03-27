# Database Migrations & N+1 Problem

> เนื้อหาเสริมจาก Relational Databases Roadmap

---

## 1. Database Migrations คืออะไร

Migration คือ **การจัดการการเปลี่ยนแปลงโครงสร้างฐานข้อมูล** แบบเป็นระบบ เหมือน Version Control สำหรับ Database Schema

```
ปัญหาถ้าไม่มี Migration:
- "ใครรัน ALTER TABLE เมื่อวาน?"
- "Database บน dev กับ production ต่างกัน!"
- "ต้อง setup database ใหม่ทั้งหมดสำหรับ developer ใหม่"

มี Migration:
- ทุกการเปลี่ยนแปลงถูกบันทึกเป็นไฟล์
- ใครก็รัน migration ได้ → ได้ database เหมือนกัน
- ย้อนกลับ (rollback) ได้
- ติดตามว่า migration ไหนรันไปแล้ว
```

### Migration ทำงานอย่างไร

```
migration ทุกตัวมี 2 ส่วน:
├── Up()   — ทำการเปลี่ยนแปลง (เช่น สร้างตาราง)
└── Down() — ยกเลิกการเปลี่ยนแปลง (เช่น ลบตาราง)

ลำดับ Migration:
001_create_users_table.sql          ← รันก่อน
002_add_email_to_users.sql
003_create_orders_table.sql
004_add_index_on_orders_user_id.sql ← รันทีหลัง

แต่ละ migration รันทีละอัน ตามลำดับ
Database เก็บว่า migration ไหนรันไปแล้วใน migration table
```

### ตัวอย่าง Migration

#### SQL แบบดิบ

```sql
-- 001_create_users_table.sql

-- Up
CREATE TABLE users (
    id INT PRIMARY KEY AUTO_INCREMENT,
    username VARCHAR(50) NOT NULL UNIQUE,
    email VARCHAR(100) NOT NULL UNIQUE,
    password_hash VARCHAR(255) NOT NULL,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP
);

-- Down
DROP TABLE users;
```

```sql
-- 002_add_profile_to_users.sql

-- Up
ALTER TABLE users
ADD COLUMN first_name VARCHAR(50),
ADD COLUMN last_name VARCHAR(50),
ADD COLUMN avatar_url VARCHAR(255);

-- Down
ALTER TABLE users
DROP COLUMN first_name,
DROP COLUMN last_name,
DROP COLUMN avatar_url;
```

#### Entity Framework Core (C# / .NET)

```csharp
// Models/User.cs
public class User
{
    public int Id { get; set; }
    public string Username { get; set; }
    public string Email { get; set; }
    public string PasswordHash { get; set; }
    public DateTime CreatedAt { get; set; }
}

// สร้าง migration
// dotnet ef migrations add CreateUsersTable

// Migration ที่ถูกสร้าง:
public partial class CreateUsersTable : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Users",
            columns: table => new
            {
                Id = table.Column<int>(nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                Username = table.Column<string>(maxLength: 50, nullable: false),
                Email = table.Column<string>(maxLength: 100, nullable: false),
                PasswordHash = table.Column<string>(maxLength: 255, nullable: false),
                CreatedAt = table.Column<DateTime>(nullable: false,
                    defaultValueSql: "GETUTCDATE()")
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Users", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Users_Email",
            table: "Users",
            column: "Email",
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "Users");
    }
}

// รัน migration
// dotnet ef database update

// Rollback
// dotnet ef database update PreviousMigrationName

// ดู migration ที่มี
// dotnet ef migrations list
```

#### Knex.js (Node.js)

```javascript
// migrations/20240101_create_users.js
exports.up = function(knex) {
  return knex.schema.createTable('users', (table) => {
    table.increments('id').primary()
    table.string('username', 50).notNullable().unique()
    table.string('email', 100).notNullable().unique()
    table.string('password_hash', 255).notNullable()
    table.timestamps(true, true)  // created_at, updated_at
  })
}

exports.down = function(knex) {
  return knex.schema.dropTable('users')
}

// คำสั่ง:
// npx knex migrate:latest      — รันทุก migration ที่ยังไม่ได้รัน
// npx knex migrate:rollback    — ย้อนกลับ migration ล่าสุด
// npx knex migrate:status      — ดูสถานะ
```

### Migration Best Practices

```
✅ ควรทำ:
- 1 migration = 1 การเปลี่ยนแปลง (เล็กและชัดเจน)
- เขียน Down() เสมอ (เพื่อ rollback ได้)
- ทดสอบ migration บน dev ก่อน production
- ใส่ migration ใน version control (Git)
- ทำ backup ก่อนรัน migration บน production

❌ ไม่ควรทำ:
- แก้ไข migration ที่รันไปแล้ว (สร้างใหม่แทน!)
- ลบ migration files
- รัน migration แบบ manual (ใช้ CLI tools)
- ใส่ข้อมูลจำนวนมากใน migration (ใช้ seed แทน)
```

### Seeds (ข้อมูลเริ่มต้น)

```javascript
// seeds/01_default_users.js
exports.seed = function(knex) {
  return knex('users').del()  // ลบข้อมูลเก่า
    .then(() => {
      return knex('users').insert([
        { username: 'admin', email: 'admin@example.com', password_hash: '...' },
        { username: 'demo', email: 'demo@example.com', password_hash: '...' },
      ])
    })
}

// npx knex seed:run
```

---

## 2. N+1 Problem

### N+1 Problem คืออะไร

ปัญหาที่ทำให้ **query จำนวนมากเกินจำเป็น** เกิดขึ้นเมื่อ:
1. Query ครั้งแรก ดึง list ของ records (1 query)
2. สำหรับแต่ละ record → query อีกครั้งเพื่อดึงข้อมูลที่เกี่ยวข้อง (N queries)

**รวมเป็น N + 1 queries** (ทั้งที่ควรใช้แค่ 1-2 queries)

### ตัวอย่าง N+1

```
สมมติมี 100 posts, แต่ละ post มี 1 author

❌ N+1 Problem (101 queries!):

// Query 1: ดึง posts ทั้งหมด
SELECT * FROM posts;                          -- 1 query

// Query 2-101: ดึง author ของแต่ละ post
SELECT * FROM users WHERE id = 1;             -- post 1
SELECT * FROM users WHERE id = 2;             -- post 2
SELECT * FROM users WHERE id = 3;             -- post 3
...
SELECT * FROM users WHERE id = 100;           -- post 100
                                               -- รวม 101 queries! 🐌
```

### โค้ดที่ทำให้เกิด N+1

```javascript
// ❌ N+1 Problem
async function getPostsWithAuthors() {
  const posts = await db.query('SELECT * FROM posts')  // 1 query

  for (const post of posts) {
    // N queries (1 ต่อ post!)
    post.author = await db.query(
      'SELECT * FROM users WHERE id = ?',
      [post.author_id]
    )
  }

  return posts
}
// ถ้ามี 100 posts = 101 queries 🐌
```

```csharp
// ❌ N+1 ใน Entity Framework (Lazy Loading)
var posts = context.Posts.ToList();  // 1 query

foreach (var post in posts)
{
    // ทุกครั้งที่เข้าถึง Author → EF query ให้อัตโนมัติ
    Console.WriteLine(post.Author.Name);  // N queries!
}
```

### วิธีแก้ N+1 Problem

#### วิธี 1: Eager Loading (โหลดล่วงหน้า)

```csharp
// ✅ Entity Framework — Include()
var posts = context.Posts
    .Include(p => p.Author)      // JOIN ในที่เดียว
    .ToList();
// SQL: SELECT p.*, u.* FROM Posts p LEFT JOIN Users u ON p.AuthorId = u.Id
// = 1 query เท่านั้น! ✅

// Nested include
var posts = context.Posts
    .Include(p => p.Author)
    .Include(p => p.Comments)
        .ThenInclude(c => c.User)
    .ToList();
```

```javascript
// ✅ Knex.js — JOIN
const posts = await knex('posts')
  .join('users', 'posts.author_id', 'users.id')
  .select('posts.*', 'users.name as author_name')
// 1 query ✅
```

#### วิธี 2: Batch Loading (โหลดเป็น batch)

```javascript
// ✅ ดึง posts ก่อน แล้วดึง authors ทีเดียว
async function getPostsWithAuthors() {
  const posts = await db.query('SELECT * FROM posts')  // 1 query

  // ดึง author IDs ทั้งหมด (ไม่ซ้ำ)
  const authorIds = [...new Set(posts.map(p => p.author_id))]

  // ดึง authors ทีเดียว
  const authors = await db.query(
    'SELECT * FROM users WHERE id IN (?)',
    [authorIds]
  )  // 1 query

  // Map authors กลับเข้า posts
  const authorMap = new Map(authors.map(a => [a.id, a]))
  posts.forEach(p => {
    p.author = authorMap.get(p.author_id)
  })

  return posts
}
// รวม 2 queries เท่านั้น ✅ (ไม่ว่าจะมีกี่ posts)
```

#### วิธี 3: DataLoader Pattern (GraphQL)

```javascript
// ✅ DataLoader — batch + cache อัตโนมัติ
const DataLoader = require('dataloader')

const userLoader = new DataLoader(async (userIds) => {
  // รวม IDs ทั้งหมดมา query ทีเดียว
  const users = await db.query(
    'SELECT * FROM users WHERE id IN (?)',
    [userIds]
  )

  // คืนค่าตามลำดับ IDs ที่ส่งมา
  const userMap = new Map(users.map(u => [u.id, u]))
  return userIds.map(id => userMap.get(id))
})

// ใช้งาน (ไม่ว่าจะเรียกกี่ครั้ง → query ทีเดียว)
const author1 = await userLoader.load(1)
const author2 = await userLoader.load(2)
const author3 = await userLoader.load(3)
// SQL: SELECT * FROM users WHERE id IN (1, 2, 3)  ← 1 query!
```

#### วิธี 4: Subquery / JOIN ใน SQL

```sql
-- ✅ JOIN
SELECT
    p.id,
    p.title,
    p.content,
    u.name AS author_name,
    u.email AS author_email
FROM posts p
INNER JOIN users u ON p.author_id = u.id
ORDER BY p.created_at DESC;

-- ✅ Subquery
SELECT
    p.*,
    (SELECT u.name FROM users u WHERE u.id = p.author_id) AS author_name
FROM posts p;
```

### ตรวจจับ N+1 Problem

```
วิธีตรวจจับ:

1. Database Query Logger
   ดู query log → ถ้ามี query ซ้ำๆ จำนวนมาก = N+1

2. ORM Profiler
   - Entity Framework: MiniProfiler, EF Core logging
   - Sequelize: logging: console.log
   - Knex: .on('query', data => console.log(data))

3. APM Tools
   - New Relic, Datadog, Application Insights
   - แสดง query count per request

// Entity Framework — เปิด logging
optionsBuilder.LogTo(Console.WriteLine, LogLevel.Information);

// Knex — เปิด query log
const knex = require('knex')({
  client: 'pg',
  connection: '...',
  debug: true    // ← log ทุก query
})
```

### สรุป N+1

```
| สถานการณ์              | วิธีแก้                      | จำนวน Query |
|-----------------------|-----------------------------|-------------|
| ❌ N+1 (lazy load)    | ไม่แก้                       | N + 1       |
| ✅ Eager Loading      | JOIN / Include               | 1           |
| ✅ Batch Loading      | WHERE IN (ids)               | 2           |
| ✅ DataLoader         | Batch + Cache                | 1-2         |
| ✅ Raw SQL JOIN       | เขียน JOIN เอง                | 1           |

กฎง่ายๆ:
- ถ้าดึง list + ข้อมูลที่เกี่ยวข้อง → ใช้ Eager Loading
- ถ้าใช้ GraphQL → ใช้ DataLoader
- ถ้า ORM ทำไม่ได้ → เขียน SQL JOIN เอง
- เปิด query log เสมอในตอน dev!
```
