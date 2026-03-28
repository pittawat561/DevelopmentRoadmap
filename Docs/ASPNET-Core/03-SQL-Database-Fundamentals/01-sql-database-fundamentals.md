# SQL & Database Fundamentals สำหรับ Enterprise API

> พื้นฐาน SQL ที่ต้องรู้ก่อนใช้ Entity Framework — อ่านจบแล้วเขียน query ได้

---

## 1. Relational Database คืออะไร

```
Database = ที่เก็บข้อมูล
Relational Database = เก็บข้อมูลเป็นตาราง (tables) ที่สัมพันธ์กัน

ตัวอย่าง:
┌─ Users Table ──────────────────┐  ┌─ Orders Table ────────────────────┐
│ Id │ Name  │ Email             │  │ Id │ UserId │ Total  │ Status    │
├────┼───────┼───────────────────┤  ├────┼────────┼────────┼───────────┤
│  1 │ John  │ john@test.com     │  │  1 │      1 │ 500.00 │ Completed │
│  2 │ Jane  │ jane@test.com     │  │  2 │      1 │ 300.00 │ Pending   │
│  3 │ Bob   │ bob@test.com      │  │  3 │      2 │ 150.00 │ Completed │
└────┴───────┴───────────────────┘  └────┴────────┴────────┴───────────┘
                                     ↑ UserId อ้างอิง Users.Id (Foreign Key)

ตัวเลือก Database สำหรับ .NET:
- SQL Server   ← แนะนำ (Microsoft ecosystem, ใช้กับ .NET ดีที่สุด)
- PostgreSQL   ← แนะนำ (open-source, ฟีเจอร์เยอะ)
- MySQL/MariaDB
- SQLite       ← เหมาะ dev/testing (ไฟล์เดียว ไม่ต้องติดตั้ง)
```

---

## 2. SQL Basics — CRUD Operations

### CREATE TABLE

```sql
-- สร้างตาราง Users
CREATE TABLE Users (
    Id          INT           IDENTITY(1,1) PRIMARY KEY,  -- Auto increment
    Name        NVARCHAR(100) NOT NULL,
    Email       NVARCHAR(255) NOT NULL UNIQUE,             -- ห้ามซ้ำ
    Password    NVARCHAR(255) NOT NULL,
    Role        NVARCHAR(50)  NOT NULL DEFAULT 'User',     -- ค่า default
    IsActive    BIT           NOT NULL DEFAULT 1,           -- boolean
    CreatedAt   DATETIME2     NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt   DATETIME2     NULL
);

-- สร้างตาราง Orders (มี Foreign Key อ้างอิง Users)
CREATE TABLE Orders (
    Id          INT           IDENTITY(1,1) PRIMARY KEY,
    UserId      INT           NOT NULL,
    OrderNumber NVARCHAR(50)  NOT NULL UNIQUE,
    TotalAmount DECIMAL(18,2) NOT NULL,
    Status      NVARCHAR(50)  NOT NULL DEFAULT 'Pending',
    Notes       NVARCHAR(MAX) NULL,                        -- text ยาวมาก
    CreatedAt   DATETIME2     NOT NULL DEFAULT GETUTCDATE(),

    -- Foreign Key
    CONSTRAINT FK_Orders_Users FOREIGN KEY (UserId)
        REFERENCES Users(Id) ON DELETE CASCADE
);

-- สร้างตาราง OrderItems
CREATE TABLE OrderItems (
    Id          INT           IDENTITY(1,1) PRIMARY KEY,
    OrderId     INT           NOT NULL,
    ProductName NVARCHAR(200) NOT NULL,
    Quantity    INT           NOT NULL CHECK (Quantity > 0),   -- ต้อง > 0
    UnitPrice   DECIMAL(18,2) NOT NULL CHECK (UnitPrice >= 0),
    Total       AS (Quantity * UnitPrice),                      -- Computed column

    CONSTRAINT FK_OrderItems_Orders FOREIGN KEY (OrderId)
        REFERENCES Orders(Id) ON DELETE CASCADE
);
```

### INSERT (เพิ่มข้อมูล)

```sql
-- เพิ่ม 1 แถว
INSERT INTO Users (Name, Email, Password, Role)
VALUES ('John Doe', 'john@test.com', 'hashed_password_123', 'Admin');

-- เพิ่มหลายแถว
INSERT INTO Users (Name, Email, Password) VALUES
    ('Jane Smith', 'jane@test.com', 'hash_456'),
    ('Bob Wilson', 'bob@test.com', 'hash_789'),
    ('Alice Brown', 'alice@test.com', 'hash_abc');

-- เพิ่มแล้วคืน Id (SQL Server)
INSERT INTO Users (Name, Email, Password)
OUTPUT INSERTED.Id, INSERTED.CreatedAt
VALUES ('New User', 'new@test.com', 'hash_def');

-- เพิ่ม Order
INSERT INTO Orders (UserId, OrderNumber, TotalAmount, Status)
VALUES (1, 'ORD-2024-001', 500.00, 'Pending');
```

### SELECT (อ่านข้อมูล)

```sql
-- อ่านทั้งหมด
SELECT * FROM Users;

-- เลือก columns
SELECT Id, Name, Email FROM Users;

-- ===== WHERE (กรอง) =====
SELECT * FROM Users WHERE IsActive = 1;
SELECT * FROM Users WHERE Role = 'Admin';
SELECT * FROM Users WHERE Name LIKE '%john%';     -- ค้นหาชื่อที่มี "john"
SELECT * FROM Users WHERE Id IN (1, 2, 3);
SELECT * FROM Users WHERE CreatedAt >= '2024-01-01';
SELECT * FROM Users WHERE Email IS NOT NULL;

-- หลายเงื่อนไข
SELECT * FROM Users
WHERE IsActive = 1
  AND Role = 'Admin'
  AND CreatedAt >= '2024-01-01';

SELECT * FROM Users
WHERE Role = 'Admin' OR Role = 'Manager';

-- ===== ORDER BY (เรียงลำดับ) =====
SELECT * FROM Users ORDER BY Name ASC;              -- A-Z
SELECT * FROM Users ORDER BY CreatedAt DESC;         -- ใหม่สุดก่อน
SELECT * FROM Users ORDER BY Role, Name;             -- เรียงตาม Role แล้วตาม Name

-- ===== LIMIT / OFFSET (Pagination) =====
-- SQL Server:
SELECT * FROM Users
ORDER BY Id
OFFSET 0 ROWS FETCH NEXT 10 ROWS ONLY;              -- Page 1 (10 per page)

SELECT * FROM Users
ORDER BY Id
OFFSET 10 ROWS FETCH NEXT 10 ROWS ONLY;             -- Page 2

-- PostgreSQL:
SELECT * FROM Users ORDER BY Id LIMIT 10 OFFSET 0;  -- Page 1

-- ===== Aggregate Functions =====
SELECT COUNT(*) AS TotalUsers FROM Users;
SELECT COUNT(*) AS ActiveUsers FROM Users WHERE IsActive = 1;
SELECT SUM(TotalAmount) AS TotalRevenue FROM Orders WHERE Status = 'Completed';
SELECT AVG(TotalAmount) AS AverageOrder FROM Orders;
SELECT MAX(TotalAmount) AS LargestOrder FROM Orders;
SELECT MIN(TotalAmount) AS SmallestOrder FROM Orders;

-- ===== GROUP BY =====
-- นับ users ตาม Role
SELECT Role, COUNT(*) AS Count
FROM Users
GROUP BY Role;
-- | Role    | Count |
-- | Admin   | 2     |
-- | User    | 15    |
-- | Manager | 3     |

-- ยอดขายรายเดือน
SELECT
    YEAR(CreatedAt) AS Year,
    MONTH(CreatedAt) AS Month,
    COUNT(*) AS OrderCount,
    SUM(TotalAmount) AS Revenue
FROM Orders
WHERE Status = 'Completed'
GROUP BY YEAR(CreatedAt), MONTH(CreatedAt)
ORDER BY Year DESC, Month DESC;

-- HAVING (กรองหลัง GROUP BY)
SELECT Role, COUNT(*) AS Count
FROM Users
GROUP BY Role
HAVING COUNT(*) > 5;    -- เฉพาะ role ที่มีคนมากกว่า 5
```

### UPDATE (แก้ไขข้อมูล)

```sql
-- แก้ไข 1 record
UPDATE Users
SET Name = 'John Updated', UpdatedAt = GETUTCDATE()
WHERE Id = 1;

-- แก้ไขหลาย records
UPDATE Users
SET IsActive = 0, UpdatedAt = GETUTCDATE()
WHERE CreatedAt < '2023-01-01';

-- ⚠️ อันตราย! ไม่มี WHERE = แก้ทุกแถว!
-- UPDATE Users SET IsActive = 0;  -- ทุกคนจะ inactive!
```

### DELETE (ลบข้อมูล)

```sql
-- ลบ 1 record
DELETE FROM Users WHERE Id = 5;

-- Soft Delete (แนะนำ — ไม่ลบจริง แค่ mark)
UPDATE Users SET IsActive = 0, UpdatedAt = GETUTCDATE() WHERE Id = 5;

-- ⚠️ ไม่มี WHERE = ลบทุกแถว!
-- DELETE FROM Users;  -- ลบทุกคน!
```

---

## 3. JOINs — เชื่อมตาราง

```sql
-- ===== INNER JOIN (เฉพาะที่ match ทั้ง 2 ตาราง) =====
-- ดึง orders พร้อมชื่อ user
SELECT
    o.Id AS OrderId,
    o.OrderNumber,
    o.TotalAmount,
    o.Status,
    u.Name AS CustomerName,
    u.Email AS CustomerEmail
FROM Orders o
INNER JOIN Users u ON o.UserId = u.Id;

-- ===== LEFT JOIN (เอาทุกแถวจากตารางซ้าย แม้ไม่ match) =====
-- ดึง users ทั้งหมด + orders (ถ้ามี)
SELECT
    u.Name,
    u.Email,
    COUNT(o.Id) AS OrderCount,
    COALESCE(SUM(o.TotalAmount), 0) AS TotalSpent
FROM Users u
LEFT JOIN Orders o ON u.Id = o.UserId
GROUP BY u.Name, u.Email;
-- Users ที่ไม่มี orders จะมี OrderCount = 0

-- ===== JOIN หลายตาราง =====
SELECT
    o.OrderNumber,
    u.Name AS Customer,
    oi.ProductName,
    oi.Quantity,
    oi.UnitPrice,
    oi.Total AS LineTotal,
    o.TotalAmount AS OrderTotal
FROM Orders o
INNER JOIN Users u ON o.UserId = u.Id
INNER JOIN OrderItems oi ON o.Id = oi.OrderId
WHERE o.Status = 'Completed'
ORDER BY o.CreatedAt DESC;
```

---

## 4. Subqueries & Common Table Expressions (CTE)

```sql
-- ===== Subquery =====
-- หา users ที่สั่งซื้อมากกว่า 3 ครั้ง
SELECT * FROM Users
WHERE Id IN (
    SELECT UserId FROM Orders
    GROUP BY UserId
    HAVING COUNT(*) > 3
);

-- ===== CTE (อ่านง่ายกว่า subquery) =====
WITH ActiveCustomers AS (
    SELECT
        UserId,
        COUNT(*) AS OrderCount,
        SUM(TotalAmount) AS TotalSpent
    FROM Orders
    WHERE Status = 'Completed'
    GROUP BY UserId
    HAVING COUNT(*) > 3
)
SELECT
    u.Name,
    u.Email,
    ac.OrderCount,
    ac.TotalSpent
FROM Users u
INNER JOIN ActiveCustomers ac ON u.Id = ac.UserId
ORDER BY ac.TotalSpent DESC;
```

---

## 5. Indexes — ทำให้ Query เร็วขึ้น

```sql
-- Index = สารบัญหนังสือ — ช่วยหาข้อมูลเร็วขึ้นโดยไม่ต้องอ่านทุกแถว

-- ===== สร้าง Index =====
-- Columns ที่ควรมี Index:
-- 1. Primary Key (มี index อัตโนมัติ)
-- 2. Foreign Keys
-- 3. Columns ที่ใช้ใน WHERE บ่อย
-- 4. Columns ที่ใช้ ORDER BY บ่อย

CREATE INDEX IX_Users_Email ON Users(Email);
CREATE INDEX IX_Orders_UserId ON Orders(UserId);
CREATE INDEX IX_Orders_Status ON Orders(Status);
CREATE INDEX IX_Orders_CreatedAt ON Orders(CreatedAt DESC);

-- Composite Index (หลาย columns)
CREATE INDEX IX_Orders_Status_CreatedAt ON Orders(Status, CreatedAt DESC);

-- Unique Index
CREATE UNIQUE INDEX IX_Users_Email_Unique ON Users(Email);

-- ===== เมื่อไหร่ไม่ควรใส่ Index =====
-- ❌ ตารางเล็ก (< 1000 rows) — ไม่จำเป็น
-- ❌ Columns ที่ INSERT/UPDATE บ่อยมาก — index ทำให้เขียนช้าลง
-- ❌ Columns ที่มีค่าไม่หลากหลาย (เช่น IsActive: 0/1)
```

---

## 6. Database Design — ออกแบบ Database

### Normalization (ทำให้ไม่ซ้ำซ้อน)

```
❌ ไม่ดี (ข้อมูลซ้ำ):
┌─ Orders ──────────────────────────────────────────────┐
│ OrderId │ CustomerName │ CustomerEmail  │ ProductName  │
├─────────┼──────────────┼────────────────┼──────────────┤
│ 1       │ John         │ john@test.com  │ Laptop       │
│ 2       │ John         │ john@test.com  │ Mouse        │ ← ชื่อ + email ซ้ำ!
│ 3       │ Jane         │ jane@test.com  │ Keyboard     │
└─────────┴──────────────┴────────────────┴──────────────┘

✅ ดี (Normalized — แยกตาราง):
Users: Id, Name, Email
Products: Id, Name, Price
Orders: Id, UserId (FK), CreatedAt
OrderItems: Id, OrderId (FK), ProductId (FK), Quantity
```

### Relationships

```
1-to-1:    User ↔ UserProfile     (1 user มี 1 profile)
1-to-Many: User → Orders          (1 user มีหลาย orders)
Many-to-Many: Users ↔ Roles       (1 user มีหลาย roles, 1 role มีหลาย users)

-- Many-to-Many ใช้ junction table:
CREATE TABLE UserRoles (
    UserId INT NOT NULL,
    RoleId INT NOT NULL,
    PRIMARY KEY (UserId, RoleId),
    FOREIGN KEY (UserId) REFERENCES Users(Id),
    FOREIGN KEY (RoleId) REFERENCES Roles(Id)
);
```

### ตัวอย่าง Enterprise Schema

```sql
-- ===== ระบบ Enterprise ทั่วไป =====

-- Users & Authentication
CREATE TABLE Users (
    Id          INT IDENTITY(1,1) PRIMARY KEY,
    Email       NVARCHAR(255) NOT NULL UNIQUE,
    PasswordHash NVARCHAR(255) NOT NULL,
    FirstName   NVARCHAR(100) NOT NULL,
    LastName    NVARCHAR(100) NOT NULL,
    IsActive    BIT NOT NULL DEFAULT 1,
    CreatedAt   DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt   DATETIME2 NULL
);

CREATE TABLE Roles (
    Id          INT IDENTITY(1,1) PRIMARY KEY,
    Name        NVARCHAR(50) NOT NULL UNIQUE,
    Description NVARCHAR(255) NULL
);

CREATE TABLE UserRoles (
    UserId  INT NOT NULL REFERENCES Users(Id),
    RoleId  INT NOT NULL REFERENCES Roles(Id),
    PRIMARY KEY (UserId, RoleId)
);

-- Products & Categories
CREATE TABLE Categories (
    Id          INT IDENTITY(1,1) PRIMARY KEY,
    Name        NVARCHAR(100) NOT NULL,
    ParentId    INT NULL REFERENCES Categories(Id),  -- self-referencing (tree)
    SortOrder   INT NOT NULL DEFAULT 0
);

CREATE TABLE Products (
    Id          INT IDENTITY(1,1) PRIMARY KEY,
    CategoryId  INT NOT NULL REFERENCES Categories(Id),
    SKU         NVARCHAR(50) NOT NULL UNIQUE,
    Name        NVARCHAR(200) NOT NULL,
    Description NVARCHAR(MAX) NULL,
    Price       DECIMAL(18,2) NOT NULL,
    Stock       INT NOT NULL DEFAULT 0,
    IsActive    BIT NOT NULL DEFAULT 1,
    CreatedAt   DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt   DATETIME2 NULL
);

-- Orders
CREATE TABLE Orders (
    Id          INT IDENTITY(1,1) PRIMARY KEY,
    UserId      INT NOT NULL REFERENCES Users(Id),
    OrderNumber NVARCHAR(50) NOT NULL UNIQUE,
    Status      NVARCHAR(50) NOT NULL DEFAULT 'Pending',
    SubTotal    DECIMAL(18,2) NOT NULL,
    Tax         DECIMAL(18,2) NOT NULL DEFAULT 0,
    Total       DECIMAL(18,2) NOT NULL,
    Notes       NVARCHAR(MAX) NULL,
    CreatedAt   DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt   DATETIME2 NULL
);

CREATE TABLE OrderItems (
    Id          INT IDENTITY(1,1) PRIMARY KEY,
    OrderId     INT NOT NULL REFERENCES Orders(Id) ON DELETE CASCADE,
    ProductId   INT NOT NULL REFERENCES Products(Id),
    Quantity    INT NOT NULL,
    UnitPrice   DECIMAL(18,2) NOT NULL,
    Total       AS (Quantity * UnitPrice)
);

-- Audit Log (ติดตามว่าใครทำอะไรเมื่อไหร่)
CREATE TABLE AuditLogs (
    Id          BIGINT IDENTITY(1,1) PRIMARY KEY,
    UserId      INT NULL REFERENCES Users(Id),
    Action      NVARCHAR(50) NOT NULL,      -- Create, Update, Delete
    EntityType  NVARCHAR(100) NOT NULL,     -- User, Order, Product
    EntityId    INT NOT NULL,
    OldValues   NVARCHAR(MAX) NULL,         -- JSON
    NewValues   NVARCHAR(MAX) NULL,         -- JSON
    CreatedAt   DATETIME2 NOT NULL DEFAULT GETUTCDATE()
);
```

---

## 7. Stored Procedures & Triggers

```sql
-- ===== Stored Procedure (ใช้บ้างใน enterprise) =====
CREATE PROCEDURE GetUserOrders
    @UserId INT,
    @Status NVARCHAR(50) = NULL    -- optional parameter
AS
BEGIN
    SELECT
        o.Id, o.OrderNumber, o.Total, o.Status, o.CreatedAt,
        COUNT(oi.Id) AS ItemCount
    FROM Orders o
    LEFT JOIN OrderItems oi ON o.Id = oi.OrderId
    WHERE o.UserId = @UserId
      AND (@Status IS NULL OR o.Status = @Status)
    GROUP BY o.Id, o.OrderNumber, o.Total, o.Status, o.CreatedAt
    ORDER BY o.CreatedAt DESC;
END;

-- เรียกใช้:
EXEC GetUserOrders @UserId = 1;
EXEC GetUserOrders @UserId = 1, @Status = 'Completed';

-- ===== Triggers (ทำอัตโนมัติเมื่อมีการเปลี่ยนแปลง) =====
-- อัปเดต UpdatedAt อัตโนมัติ
CREATE TRIGGER TR_Users_UpdateTimestamp
ON Users
AFTER UPDATE
AS
BEGIN
    UPDATE Users
    SET UpdatedAt = GETUTCDATE()
    FROM Users u
    INNER JOIN inserted i ON u.Id = i.Id;
END;

-- ⚠️ ใน Enterprise สมัยใหม่:
-- มักจัดการ logic ใน Application layer (C#) แทน Triggers
-- เพราะ debug ง่ายกว่า, test ง่ายกว่า, ย้าย database ง่ายกว่า
```

---

## 8. Transactions — ความปลอดภัยของข้อมูล

```sql
-- Transaction = ทำหลายคำสั่งเป็น 1 unit
-- ถ้าอันใดอันหนึ่ง fail → ยกเลิกทั้งหมด (rollback)

-- ตัวอย่าง: สร้าง Order + OrderItems ต้องสำเร็จทั้งคู่
BEGIN TRANSACTION;

BEGIN TRY
    -- สร้าง Order
    INSERT INTO Orders (UserId, OrderNumber, SubTotal, Tax, Total)
    VALUES (1, 'ORD-2024-100', 1000.00, 70.00, 1070.00);

    DECLARE @OrderId INT = SCOPE_IDENTITY();

    -- สร้าง OrderItems
    INSERT INTO OrderItems (OrderId, ProductId, Quantity, UnitPrice)
    VALUES
        (@OrderId, 1, 2, 300.00),
        (@OrderId, 2, 1, 400.00);

    -- อัปเดต stock
    UPDATE Products SET Stock = Stock - 2 WHERE Id = 1;
    UPDATE Products SET Stock = Stock - 1 WHERE Id = 2;

    COMMIT TRANSACTION;    -- สำเร็จทั้งหมด → บันทึก
    PRINT 'Order created successfully';
END TRY
BEGIN CATCH
    ROLLBACK TRANSACTION;  -- fail → ยกเลิกทั้งหมด
    PRINT 'Error: ' + ERROR_MESSAGE();
END CATCH;

-- ใน C# (Entity Framework) จัดการ transaction ให้อัตโนมัติ:
-- SaveChangesAsync() = 1 transaction
-- ถ้า error → rollback ทั้งหมด
```

---

## 9. สรุป SQL ที่ต้องรู้ก่อนไป Entity Framework

```
✅ ต้องเข้าใจ:
├── CRUD: SELECT, INSERT, UPDATE, DELETE
├── WHERE, ORDER BY, GROUP BY, HAVING
├── JOINs: INNER JOIN, LEFT JOIN
├── Aggregate: COUNT, SUM, AVG, MAX, MIN
├── Pagination: OFFSET/FETCH, LIMIT
├── Data Types: INT, NVARCHAR, DECIMAL, DATETIME2, BIT
├── Constraints: PRIMARY KEY, FOREIGN KEY, UNIQUE, NOT NULL, CHECK
├── Indexes: เมื่อไหร่ควรสร้าง
├── Relationships: 1-to-1, 1-to-Many, Many-to-Many
├── Transactions: BEGIN/COMMIT/ROLLBACK
└── Database Design: Normalization, Schema ออกแบบ

เมื่อใช้ Entity Framework:
- ไม่ต้องเขียน SQL ด้วยมือ (EF สร้างให้)
- แต่ต้องอ่าน SQL ที่ EF สร้างได้ (debug performance)
- บาง query ซับซ้อน อาจต้องเขียน raw SQL
```
