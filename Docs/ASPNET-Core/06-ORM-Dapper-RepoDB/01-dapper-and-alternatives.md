# Dapper & ORM ทางเลือก — Micro-ORM สำหรับ Performance

> เมื่อ EF Core ช้าเกินไป หรือต้องการควบคุม SQL เอง

---

## 1. ทำไมต้องใช้ Dapper

```
EF Core:
✅ ง่าย, LINQ, Migrations, Change Tracking
❌ ช้ากว่า raw SQL, สร้าง SQL ที่ไม่ efficient บางครั้ง

Dapper:
✅ เร็วมาก (ใกล้ raw ADO.NET), เขียน SQL เอง, เบามาก
❌ ไม่มี Migrations, ไม่มี Change Tracking, เขียน SQL เอง

ใช้เมื่อไหร่:
- Reports/Dashboard → query ซับซ้อน เขียน SQL เองดีกว่า
- High-performance reads → ต้องการความเร็ว
- Legacy database → schema แปลก EF Core map ยาก
- Stored Procedures → เรียกง่ายกว่า

แนะนำ: ใช้ EF Core เป็นหลัก + Dapper เสริมเมื่อต้องการ
```

### ติดตั้ง

```bash
dotnet add package Dapper
```

---

## 2. Dapper พื้นฐาน

```csharp
using Dapper;
using Microsoft.Data.SqlClient;

public class DapperUserRepository
{
    private readonly string _connectionString;

    public DapperUserRepository(IConfiguration config)
    {
        _connectionString = config.GetConnectionString("DefaultConnection")!;
    }

    private IDbConnection CreateConnection() => new SqlConnection(_connectionString);

    // ===== Query — ดึงข้อมูล =====

    // ดึงทั้งหมด
    public async Task<IEnumerable<UserDto>> GetAllAsync()
    {
        using var db = CreateConnection();
        return await db.QueryAsync<UserDto>(
            "SELECT Id, FirstName, LastName, Email, IsActive, CreatedAt FROM Users WHERE IsDeleted = 0");
    }

    // ดึง 1 record
    public async Task<UserDto?> GetByIdAsync(int id)
    {
        using var db = CreateConnection();
        return await db.QueryFirstOrDefaultAsync<UserDto>(
            "SELECT Id, FirstName, LastName, Email, IsActive, CreatedAt FROM Users WHERE Id = @Id",
            new { Id = id });   // ← parameterized query (ป้องกัน SQL Injection!)
    }

    // ค้นหา + Pagination
    public async Task<(IEnumerable<UserDto> Items, int TotalCount)> SearchAsync(
        string? search, int page, int pageSize)
    {
        using var db = CreateConnection();

        var sql = """
            SELECT Id, FirstName, LastName, Email, IsActive, CreatedAt
            FROM Users
            WHERE IsDeleted = 0
              AND (@Search IS NULL OR FirstName LIKE '%' + @Search + '%'
                   OR LastName LIKE '%' + @Search + '%'
                   OR Email LIKE '%' + @Search + '%')
            ORDER BY CreatedAt DESC
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;

            SELECT COUNT(*)
            FROM Users
            WHERE IsDeleted = 0
              AND (@Search IS NULL OR FirstName LIKE '%' + @Search + '%'
                   OR LastName LIKE '%' + @Search + '%'
                   OR Email LIKE '%' + @Search + '%');
            """;

        using var multi = await db.QueryMultipleAsync(sql, new
        {
            Search = search,
            Offset = (page - 1) * pageSize,
            PageSize = pageSize
        });

        var items = await multi.ReadAsync<UserDto>();
        var totalCount = await multi.ReadSingleAsync<int>();

        return (items, totalCount);
    }

    // ===== Execute — แก้ไขข้อมูล =====

    // Insert + return Id
    public async Task<int> CreateAsync(CreateUserRequest request)
    {
        using var db = CreateConnection();
        var id = await db.QuerySingleAsync<int>("""
            INSERT INTO Users (FirstName, LastName, Email, PasswordHash, CreatedAt)
            OUTPUT INSERTED.Id
            VALUES (@FirstName, @LastName, @Email, @PasswordHash, GETUTCDATE())
            """,
            new
            {
                request.FirstName,
                request.LastName,
                request.Email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password)
            });
        return id;
    }

    // Update
    public async Task<bool> UpdateAsync(int id, UpdateUserRequest request)
    {
        using var db = CreateConnection();
        var affected = await db.ExecuteAsync("""
            UPDATE Users SET
                FirstName = COALESCE(@FirstName, FirstName),
                LastName = COALESCE(@LastName, LastName),
                Email = COALESCE(@Email, Email),
                UpdatedAt = GETUTCDATE()
            WHERE Id = @Id AND IsDeleted = 0
            """,
            new { Id = id, request.FirstName, request.LastName, request.Email });
        return affected > 0;
    }

    // ===== JOIN Queries =====
    public async Task<IEnumerable<OrderWithUserDto>> GetOrdersWithUsersAsync()
    {
        using var db = CreateConnection();
        return await db.QueryAsync<OrderWithUserDto>("""
            SELECT
                o.Id, o.OrderNumber, o.Total, o.Status,
                o.CreatedAt AS OrderDate,
                u.FirstName + ' ' + u.LastName AS CustomerName,
                u.Email AS CustomerEmail
            FROM Orders o
            INNER JOIN Users u ON o.UserId = u.Id
            WHERE o.IsDeleted = 0
            ORDER BY o.CreatedAt DESC
            """);
    }

    // ===== Complex mapping (1-to-Many) =====
    public async Task<OrderDetailDto?> GetOrderDetailAsync(int orderId)
    {
        using var db = CreateConnection();
        var sql = """
            SELECT o.*, u.FirstName, u.LastName, u.Email
            FROM Orders o
            INNER JOIN Users u ON o.UserId = u.Id
            WHERE o.Id = @OrderId;

            SELECT oi.*, p.Name AS ProductName
            FROM OrderItems oi
            INNER JOIN Products p ON oi.ProductId = p.Id
            WHERE oi.OrderId = @OrderId;
            """;

        using var multi = await db.QueryMultipleAsync(sql, new { OrderId = orderId });

        var order = await multi.ReadFirstOrDefaultAsync<OrderDetailDto>();
        if (order is null) return null;

        var items = await multi.ReadAsync<OrderItemDto>();
        order.Items = items.ToList();

        return order;
    }

    // ===== Stored Procedures =====
    public async Task<IEnumerable<SalesReportDto>> GetSalesReportAsync(DateTime from, DateTime to)
    {
        using var db = CreateConnection();
        return await db.QueryAsync<SalesReportDto>(
            "sp_GetSalesReport",
            new { FromDate = from, ToDate = to },
            commandType: CommandType.StoredProcedure);
    }

    // ===== Transaction =====
    public async Task CreateOrderAsync(int userId, List<CartItem> items)
    {
        using var db = CreateConnection();
        db.Open();
        using var transaction = db.BeginTransaction();

        try
        {
            var orderId = await db.QuerySingleAsync<int>("""
                INSERT INTO Orders (UserId, OrderNumber, Total, CreatedAt)
                OUTPUT INSERTED.Id
                VALUES (@UserId, @OrderNumber, @Total, GETUTCDATE())
                """,
                new { UserId = userId, OrderNumber = $"ORD-{DateTime.Now:yyyyMMddHHmmss}",
                      Total = items.Sum(i => i.Quantity * i.Price) },
                transaction);

            foreach (var item in items)
            {
                await db.ExecuteAsync("""
                    INSERT INTO OrderItems (OrderId, ProductId, Quantity, UnitPrice)
                    VALUES (@OrderId, @ProductId, @Quantity, @UnitPrice)
                    """,
                    new { OrderId = orderId, item.ProductId, item.Quantity, UnitPrice = item.Price },
                    transaction);
            }

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }
}
```

---

## 3. EF Core + Dapper ใช้ร่วมกัน

```csharp
// ===== ใช้ EF Core connection กับ Dapper =====
public class HybridUserRepository : IUserRepository
{
    private readonly AppDbContext _context;

    public HybridUserRepository(AppDbContext context)
    {
        _context = context;
    }

    // CRUD ปกติ → ใช้ EF Core
    public async Task<User?> GetByIdAsync(int id)
        => await _context.Users.FindAsync(id);

    public async Task<User> CreateAsync(User user)
    {
        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        return user;
    }

    // Reports / Complex queries → ใช้ Dapper
    public async Task<IEnumerable<DashboardStats>> GetDashboardAsync()
    {
        var connection = _context.Database.GetDbConnection();
        return await connection.QueryAsync<DashboardStats>("""
            SELECT
                (SELECT COUNT(*) FROM Users WHERE IsActive = 1) AS TotalUsers,
                (SELECT COUNT(*) FROM Orders WHERE CreatedAt >= DATEADD(day, -30, GETUTCDATE())) AS MonthlyOrders,
                (SELECT SUM(Total) FROM Orders WHERE Status = 'Completed'
                    AND CreatedAt >= DATEADD(day, -30, GETUTCDATE())) AS MonthlyRevenue,
                (SELECT COUNT(*) FROM Products WHERE Stock < 10) AS LowStockProducts
            """);
    }
}

// ลงทะเบียน:
builder.Services.AddScoped<IUserRepository, HybridUserRepository>();
```

---

## 4. RepoDB (ทางเลือกอื่น)

```csharp
// RepoDB = hybrid ORM ระหว่าง Dapper กับ EF Core
// ง่ายกว่า Dapper, เร็วกว่า EF Core
// dotnet add package RepoDb.SqlServer

// ตัวอย่าง:
using var db = new SqlConnection(connectionString);

// Query
var users = await db.QueryAllAsync<User>();
var user = await db.QueryAsync<User>(u => u.Id == 1);

// Insert
var id = await db.InsertAsync(new User { Name = "John", Email = "john@test.com" });

// Update
await db.UpdateAsync(user);

// Delete
await db.DeleteAsync<User>(1);

// Batch
await db.InsertAllAsync(users);
await db.UpdateAllAsync(users);
```

---

## 5. เมื่อไหร่ใช้อะไร

```
| สถานการณ์                    | ใช้            |
|------------------------------|---------------|
| CRUD ปกติ                    | EF Core ✅    |
| Migration / Schema           | EF Core ✅    |
| Complex reports              | Dapper ✅     |
| Stored Procedures            | Dapper ✅     |
| High-performance reads       | Dapper ✅     |
| Bulk operations              | EF Core 7+ (ExecuteUpdate/Delete) |
| Rapid prototyping            | EF Core ✅    |
| Legacy database              | Dapper ✅     |

สรุป:
Enterprise API → EF Core เป็นหลัก (90%)
               → Dapper เสริมสำหรับ reports/performance (10%)
```
