# Entity Framework Core Deep Dive — ORM ฉบับเต็ม

> อ่านจบแล้วใช้ EF Core จัดการ database ใน Enterprise API ได้จริง

---

## 1. EF Core คืออะไร

```
EF Core = Object-Relational Mapper (ORM)
แปลง C# objects ↔ Database tables อัตโนมัติ

ไม่มี ORM:
- เขียน SQL เอง
- จับ results ใส่ object เอง
- จัดการ connection เอง

มี EF Core:
- เขียน C# (LINQ) → EF Core แปลงเป็น SQL ให้
- ผลลัพธ์แปลงเป็น C# objects ให้อัตโนมัติ
- จัดการ connection, transaction ให้

// แทนที่เขียน SQL:
// SELECT * FROM Users WHERE IsActive = 1 ORDER BY Name

// เขียน C#:
var users = await _context.Users
    .Where(u => u.IsActive)
    .OrderBy(u => u.Name)
    .ToListAsync();
```

### ติดตั้ง

```bash
# NuGet packages
dotnet add package Microsoft.EntityFrameworkCore
dotnet add package Microsoft.EntityFrameworkCore.SqlServer      # SQL Server
dotnet add package Microsoft.EntityFrameworkCore.Tools           # Migrations CLI
dotnet add package Microsoft.EntityFrameworkCore.Design          # Design-time

# ถ้าใช้ PostgreSQL แทน:
dotnet add package Npgsql.EntityFrameworkCore.PostgreSQL

# ถ้าใช้ SQLite (dev/testing):
dotnet add package Microsoft.EntityFrameworkCore.Sqlite

# EF Core CLI tool
dotnet tool install --global dotnet-ef
```

---

## 2. DbContext — หัวใจของ EF Core

```csharp
// Data/AppDbContext.cs
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    // ===== DbSets = ตาราง =====
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    // ===== Configuration =====
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // โหลด config ทั้งหมดจาก Assembly (แนะนำ!)
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // Global Query Filter — ทุก query จะมี WHERE IsDeleted = false อัตโนมัติ
        modelBuilder.Entity<User>().HasQueryFilter(u => !u.IsDeleted);
        modelBuilder.Entity<Product>().HasQueryFilter(p => !p.IsDeleted);
        modelBuilder.Entity<Order>().HasQueryFilter(o => !o.IsDeleted);
    }

    // ===== Override SaveChanges สำหรับ Audit =====
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // อัปเดต timestamps อัตโนมัติ
        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = DateTime.UtcNow;
                    break;
                case EntityState.Modified:
                    entry.Entity.UpdatedAt = DateTime.UtcNow;
                    break;
            }
        }

        return await base.SaveChangesAsync(cancellationToken);
    }
}

// ===== ลงทะเบียนใน Program.cs =====
builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sqlOptions =>
        {
            sqlOptions.EnableRetryOnFailure(3);      // retry ถ้า connection fail
            sqlOptions.CommandTimeout(30);            // timeout 30 วินาที
        });

    // Development: เปิด sensitive data logging
    if (builder.Environment.IsDevelopment())
    {
        options.EnableSensitiveDataLogging();
        options.EnableDetailedErrors();
    }
});
```

---

## 3. Entity Configuration — กำหนดโครงสร้างตาราง

### แบบ Fluent API (แนะนำ! — แยก config ออกจาก Entity)

```csharp
// ===== Base Entity =====
public abstract class BaseEntity
{
    public int Id { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; } = false;    // Soft delete
}

// ===== Entities =====
public class User : BaseEntity
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    // Navigation Properties
    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    public ICollection<Order> Orders { get; set; } = new List<Order>();

    // Computed
    public string FullName => $"{FirstName} {LastName}";
}

public class Role : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
}

// Junction table สำหรับ Many-to-Many
public class UserRole
{
    public int UserId { get; set; }
    public int RoleId { get; set; }
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;
    public Role Role { get; set; } = null!;
}

public class Category : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public int? ParentId { get; set; }
    public int SortOrder { get; set; }

    public Category? Parent { get; set; }
    public ICollection<Category> Children { get; set; } = new List<Category>();
    public ICollection<Product> Products { get; set; } = new List<Product>();
}

public class Product : BaseEntity
{
    public int CategoryId { get; set; }
    public string SKU { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public int Stock { get; set; }
    public bool IsActive { get; set; } = true;

    public Category Category { get; set; } = null!;
    public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
}

public class Order : BaseEntity
{
    public int UserId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public OrderStatus Status { get; set; } = OrderStatus.Pending;
    public decimal SubTotal { get; set; }
    public decimal Tax { get; set; }
    public decimal Total { get; set; }
    public string? Notes { get; set; }

    public User User { get; set; } = null!;
    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
}

public enum OrderStatus
{
    Pending,
    Processing,
    Shipped,
    Completed,
    Cancelled
}

public class OrderItem
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public int ProductId { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Total => Quantity * UnitPrice;

    public Order Order { get; set; } = null!;
    public Product Product { get; set; } = null!;
}

// ===== Fluent API Configurations (แยกไฟล์) =====
// Data/Configurations/UserConfiguration.cs
public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");

        builder.HasKey(u => u.Id);

        builder.Property(u => u.FirstName)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(u => u.LastName)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(u => u.Email)
            .HasMaxLength(255)
            .IsRequired();

        builder.HasIndex(u => u.Email)
            .IsUnique();

        builder.Property(u => u.PasswordHash)
            .HasMaxLength(255)
            .IsRequired();

        // Soft delete filter
        builder.HasQueryFilter(u => !u.IsDeleted);
    }
}

// Data/Configurations/OrderConfiguration.cs
public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("Orders");

        builder.HasIndex(o => o.OrderNumber).IsUnique();

        builder.Property(o => o.SubTotal).HasPrecision(18, 2);
        builder.Property(o => o.Tax).HasPrecision(18, 2);
        builder.Property(o => o.Total).HasPrecision(18, 2);

        builder.Property(o => o.Status)
            .HasConversion<string>()             // เก็บ enum เป็น string ใน DB
            .HasMaxLength(50);

        // Relationships
        builder.HasOne(o => o.User)
            .WithMany(u => u.Orders)
            .HasForeignKey(o => o.UserId)
            .OnDelete(DeleteBehavior.Restrict);  // ลบ User ไม่ได้ถ้ามี Orders

        builder.HasMany(o => o.Items)
            .WithOne(i => i.Order)
            .HasForeignKey(i => i.OrderId)
            .OnDelete(DeleteBehavior.Cascade);   // ลบ Order → ลบ Items ด้วย

        builder.HasQueryFilter(o => !o.IsDeleted);
    }
}

// Data/Configurations/UserRoleConfiguration.cs
public class UserRoleConfiguration : IEntityTypeConfiguration<UserRole>
{
    public void Configure(EntityTypeBuilder<UserRole> builder)
    {
        builder.ToTable("UserRoles");

        // Composite Primary Key
        builder.HasKey(ur => new { ur.UserId, ur.RoleId });

        builder.HasOne(ur => ur.User)
            .WithMany(u => u.UserRoles)
            .HasForeignKey(ur => ur.UserId);

        builder.HasOne(ur => ur.Role)
            .WithMany(r => r.UserRoles)
            .HasForeignKey(ur => ur.RoleId);
    }
}
```

---

## 4. Migrations — จัดการ Database Schema

```bash
# ===== สร้าง Migration แรก =====
dotnet ef migrations add InitialCreate
# → สร้างไฟล์ใน Migrations/ folder

# ===== Apply Migration (สร้าง/อัปเดต DB) =====
dotnet ef database update

# ===== เพิ่ม column/table ใหม่ =====
# 1. แก้ Entity (เช่น เพิ่ม PhoneNumber ใน User)
# 2. สร้าง migration:
dotnet ef migrations add AddUserPhoneNumber
# 3. Apply:
dotnet ef database update

# ===== ดู Migrations ทั้งหมด =====
dotnet ef migrations list

# ===== Rollback =====
dotnet ef database update InitialCreate    # กลับไป migration นี้
dotnet ef database update 0               # rollback ทั้งหมด

# ===== ลบ Migration ล่าสุด (ที่ยังไม่ apply) =====
dotnet ef migrations remove

# ===== สร้าง SQL Script (สำหรับ production) =====
dotnet ef migrations script --idempotent -o migration.sql
# --idempotent = รันซ้ำได้ไม่พัง
```

### Seed Data (ข้อมูลเริ่มต้น)

```csharp
// ใน Configuration หรือ OnModelCreating:
public class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.HasData(
            new Role { Id = 1, Name = "Admin", Description = "System Administrator" },
            new Role { Id = 2, Name = "Manager", Description = "Department Manager" },
            new Role { Id = 3, Name = "User", Description = "Regular User" }
        );
    }
}

// หรือใน DbContext:
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<User>().HasData(new User
    {
        Id = 1,
        FirstName = "System",
        LastName = "Admin",
        Email = "admin@company.com",
        PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin123!"),
        IsActive = true
    });
}
```

---

## 5. Querying — ดึงข้อมูล

### Basic Queries

```csharp
// ===== ดึงทั้งหมด =====
var users = await _context.Users.ToListAsync();

// ===== กรอง =====
var activeUsers = await _context.Users
    .Where(u => u.IsActive)
    .ToListAsync();

// ===== หา 1 record =====
var user = await _context.Users.FindAsync(id);                    // by PK (เร็วสุด — ดู cache ก่อน)
var user2 = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);  // by condition
var user3 = await _context.Users.SingleOrDefaultAsync(u => u.Id == id);       // ต้องมี 0 หรือ 1

// ===== เรียงลำดับ + Pagination =====
var pagedUsers = await _context.Users
    .Where(u => u.IsActive)
    .OrderByDescending(u => u.CreatedAt)
    .Skip((page - 1) * pageSize)
    .Take(pageSize)
    .ToListAsync();

var totalCount = await _context.Users.CountAsync(u => u.IsActive);

// ===== Projection (Select เฉพาะ columns ที่ต้องการ — เร็วกว่า!) =====
var userDtos = await _context.Users
    .Where(u => u.IsActive)
    .Select(u => new UserDto(
        u.Id,
        u.FirstName,
        u.LastName,
        u.Email,
        u.UserRoles.Select(ur => ur.Role.Name).FirstOrDefault() ?? "User",
        u.IsActive,
        u.CreatedAt
    ))
    .ToListAsync();
// → SELECT Id, FirstName, LastName, Email, ... FROM Users WHERE IsActive = 1
// ไม่ดึง PasswordHash, Orders → เร็วกว่า + ปลอดภัยกว่า!

// ===== เช็คว่ามีไหม (ไม่ต้องดึงข้อมูลทั้งหมด) =====
bool emailExists = await _context.Users.AnyAsync(u => u.Email == email);
```

### Include — Loading Related Data

```csharp
// ===== Eager Loading (โหลดพร้อมกัน — ใช้บ่อยที่สุด) =====
// ดึง User + Orders
var user = await _context.Users
    .Include(u => u.Orders)
    .FirstOrDefaultAsync(u => u.Id == id);

// ดึง User + Orders + OrderItems
var user2 = await _context.Users
    .Include(u => u.Orders)
        .ThenInclude(o => o.Items)          // nested include
            .ThenInclude(i => i.Product)    // deeper nested
    .FirstOrDefaultAsync(u => u.Id == id);

// ดึง User + Roles (Many-to-Many)
var user3 = await _context.Users
    .Include(u => u.UserRoles)
        .ThenInclude(ur => ur.Role)
    .FirstOrDefaultAsync(u => u.Id == id);

// ⚠️ Include หลายชั้นทำให้ query หนัก!
// ใช้ Select/Projection แทนเมื่อทำได้:
var userDetail = await _context.Users
    .Where(u => u.Id == id)
    .Select(u => new UserDetailDto(
        u.Id,
        u.FirstName,
        u.LastName,
        u.Email,
        u.UserRoles.Select(ur => ur.Role.Name).ToList(),
        u.IsActive,
        u.CreatedAt,
        u.Orders
            .OrderByDescending(o => o.CreatedAt)
            .Take(5)
            .Select(o => new OrderSummaryDto(o.Id, o.OrderNumber, o.Total, o.Status.ToString()))
            .ToList()
    ))
    .FirstOrDefaultAsync();

// ===== Lazy Loading (โหลดเมื่อเข้าถึง — ไม่แนะนำ!) =====
// ทำให้เกิด N+1 problem
// ❌ var user = await _context.Users.FindAsync(1);
//    foreach (var order in user.Orders)  ← query DB ทุกรอบ!

// ===== Explicit Loading (เลือกโหลดทีหลัง) =====
var user4 = await _context.Users.FindAsync(id);
await _context.Entry(user4!).Collection(u => u.Orders).LoadAsync();
await _context.Entry(user4!).Reference(u => u.Department).LoadAsync();
```

### Advanced Queries

```csharp
// ===== Search (Full-text search แบบง่าย) =====
var searchResults = await _context.Products
    .Where(p => p.Name.Contains(searchTerm) ||
                p.Description!.Contains(searchTerm) ||
                p.SKU.Contains(searchTerm))
    .OrderByDescending(p => p.Name.StartsWith(searchTerm))  // exact match ก่อน
    .Take(20)
    .ToListAsync();

// ===== Group By =====
var ordersByStatus = await _context.Orders
    .GroupBy(o => o.Status)
    .Select(g => new
    {
        Status = g.Key,
        Count = g.Count(),
        TotalRevenue = g.Sum(o => o.Total)
    })
    .ToListAsync();

// ===== Raw SQL (เมื่อ LINQ ไม่พอ) =====
var users = await _context.Users
    .FromSqlRaw("SELECT * FROM Users WHERE DATEDIFF(day, CreatedAt, GETUTCDATE()) < 30")
    .ToListAsync();

// Raw SQL with parameters (ป้องกัน SQL Injection!)
var users2 = await _context.Users
    .FromSqlInterpolated($"SELECT * FROM Users WHERE Email = {email}")
    .ToListAsync();

// Execute non-query
await _context.Database.ExecuteSqlInterpolatedAsync(
    $"UPDATE Products SET Stock = Stock - {quantity} WHERE Id = {productId}");

// ===== AsNoTracking (เร็วกว่าสำหรับ read-only!) =====
var users = await _context.Users
    .AsNoTracking()                      // ไม่ track changes → เร็วกว่า 30-50%
    .Where(u => u.IsActive)
    .ToListAsync();
// ใช้เมื่อ: อ่านอย่างเดียว ไม่แก้ไข
```

---

## 6. Change Tracker — ติดตามการเปลี่ยนแปลง

```csharp
// EF Core ติดตาม entity ที่ดึงมาจาก DB
// เมื่อเรียก SaveChanges → ดูว่ามีอะไรเปลี่ยน → สร้าง SQL

// ===== States =====
// Added     → INSERT
// Modified  → UPDATE
// Deleted   → DELETE
// Unchanged → ไม่ทำอะไร
// Detached  → ไม่ track

// ===== Create =====
var user = new User { FirstName = "John", Email = "john@test.com" };
_context.Users.Add(user);                   // State: Added
await _context.SaveChangesAsync();           // → INSERT INTO Users ...
// user.Id ถูก set อัตโนมัติ!

// ===== Update =====
var user = await _context.Users.FindAsync(1);  // State: Unchanged
user!.FirstName = "Jane";                       // State: Modified (auto-detect!)
await _context.SaveChangesAsync();               // → UPDATE Users SET FirstName = 'Jane' WHERE Id = 1

// ===== Delete =====
var user = await _context.Users.FindAsync(1);
_context.Users.Remove(user!);                    // State: Deleted
await _context.SaveChangesAsync();                // → DELETE FROM Users WHERE Id = 1

// ===== Bulk Update (.NET 7+) =====
// อัปเดตโดยไม่ต้องโหลด entities (เร็วมาก!)
await _context.Users
    .Where(u => u.CreatedAt < DateTime.UtcNow.AddYears(-2))
    .ExecuteUpdateAsync(u => u
        .SetProperty(x => x.IsActive, false)
        .SetProperty(x => x.UpdatedAt, DateTime.UtcNow));
// → UPDATE Users SET IsActive = 0, UpdatedAt = '...' WHERE CreatedAt < '...'

// ===== Bulk Delete (.NET 7+) =====
await _context.AuditLogs
    .Where(l => l.CreatedAt < DateTime.UtcNow.AddMonths(-6))
    .ExecuteDeleteAsync();
// → DELETE FROM AuditLogs WHERE CreatedAt < '...'
```

---

## 7. Transactions

```csharp
// ===== SaveChanges = 1 Transaction อัตโนมัติ =====
// ทุกการเปลี่ยนแปลงใน 1 SaveChanges จะอยู่ใน 1 transaction
var order = new Order { UserId = 1, OrderNumber = "ORD-001", Total = 500 };
_context.Orders.Add(order);

var item1 = new OrderItem { Order = order, ProductId = 1, Quantity = 2, UnitPrice = 200 };
var item2 = new OrderItem { Order = order, ProductId = 2, Quantity = 1, UnitPrice = 100 };
_context.OrderItems.AddRange(item1, item2);

await _context.SaveChangesAsync();  // ทั้งหมดสำเร็จ หรือ ทั้งหมด rollback

// ===== Manual Transaction (หลาย SaveChanges) =====
using var transaction = await _context.Database.BeginTransactionAsync();
try
{
    // 1. สร้าง Order
    var order = new Order { UserId = userId, OrderNumber = GenerateOrderNumber() };
    _context.Orders.Add(order);
    await _context.SaveChangesAsync();

    // 2. สร้าง OrderItems + ลด Stock
    foreach (var item in cartItems)
    {
        _context.OrderItems.Add(new OrderItem
        {
            OrderId = order.Id,
            ProductId = item.ProductId,
            Quantity = item.Quantity,
            UnitPrice = item.Price
        });

        var product = await _context.Products.FindAsync(item.ProductId);
        if (product!.Stock < item.Quantity)
            throw new InvalidOperationException($"Insufficient stock for {product.Name}");

        product.Stock -= item.Quantity;
    }
    await _context.SaveChangesAsync();

    // 3. คำนวณ Total
    order.Total = cartItems.Sum(i => i.Quantity * i.Price);
    await _context.SaveChangesAsync();

    await transaction.CommitAsync();     // สำเร็จทั้งหมด
}
catch
{
    await transaction.RollbackAsync();   // ยกเลิกทั้งหมด
    throw;
}
```

---

## 8. Performance Tips

```csharp
// ===== 1. ใช้ AsNoTracking สำหรับ read-only =====
var users = await _context.Users.AsNoTracking().ToListAsync();

// ===== 2. ใช้ Select/Projection แทน Include =====
// ❌ ช้า — ดึงทุก column + relations
var user = await _context.Users
    .Include(u => u.Orders)
    .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
    .FirstOrDefaultAsync(u => u.Id == id);

// ✅ เร็ว — ดึงเฉพาะที่ต้องการ
var userDto = await _context.Users
    .Where(u => u.Id == id)
    .Select(u => new { u.Id, u.FirstName, u.Email, OrderCount = u.Orders.Count() })
    .FirstOrDefaultAsync();

// ===== 3. ใช้ ExecuteUpdate/ExecuteDelete สำหรับ bulk =====
// ❌ ช้า — โหลดทุก entity แล้ว update ทีละตัว
var oldUsers = await _context.Users.Where(u => !u.IsActive).ToListAsync();
foreach (var u in oldUsers) _context.Users.Remove(u);
await _context.SaveChangesAsync();

// ✅ เร็ว — SQL เดียว
await _context.Users.Where(u => !u.IsActive).ExecuteDeleteAsync();

// ===== 4. ระวัง N+1 Problem =====
// ❌ N+1 — query 1 ครั้งสำหรับ users + N ครั้งสำหรับ orders
var users = await _context.Users.ToListAsync();
foreach (var user in users)
{
    var orders = user.Orders.ToList();  // query ทุกรอบ!
}

// ✅ ใช้ Include หรือ Select
var users = await _context.Users.Include(u => u.Orders).ToListAsync();

// ===== 5. SplitQuery สำหรับ multiple Includes =====
var orders = await _context.Orders
    .Include(o => o.Items)
    .Include(o => o.User)
    .AsSplitQuery()              // แยกเป็นหลาย SQL queries แทน 1 query ใหญ่
    .ToListAsync();

// ===== 6. ดู SQL ที่ EF Core สร้าง =====
// ใน appsettings.Development.json:
// "Logging": { "LogLevel": { "Microsoft.EntityFrameworkCore.Database.Command": "Information" } }
// → จะเห็น SQL ใน console ทุก query
```
