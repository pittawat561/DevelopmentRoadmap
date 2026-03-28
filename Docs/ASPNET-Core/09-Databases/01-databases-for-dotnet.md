# Databases สำหรับ .NET Enterprise API

> SQL Server, PostgreSQL, MongoDB — เมื่อไหร่ใช้อะไร

---

## 1. SQL Server (แนะนำสำหรับ .NET)

```
ทำไม SQL Server กับ .NET:
- Microsoft ecosystem → integration ดีที่สุด
- EF Core support เต็ม 100%
- Azure SQL = managed service
- SSMS (Management Studio) = เครื่องมือจัดการ GUI ที่ดีมาก

ติดตั้ง:
- Windows: SQL Server Express (ฟรี)
- Docker: docker run -e ACCEPT_EULA=Y -e SA_PASSWORD=YourPass123! -p 1433:1433 mcr.microsoft.com/mssql/server:2022-latest
- Azure: Azure SQL Database (managed)
```

```csharp
// Connection String
"Server=localhost;Database=MyApp;Trusted_Connection=true;TrustServerCertificate=true;"

// Docker:
"Server=localhost,1433;Database=MyApp;User Id=sa;Password=YourPass123!;TrustServerCertificate=true;"

// EF Core registration
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(connectionString, sql =>
    {
        sql.EnableRetryOnFailure(3);
        sql.CommandTimeout(30);
        sql.MigrationsAssembly("MyApp.Api");
    }));
```

---

## 2. PostgreSQL (Open-Source ที่ดีที่สุด)

```
ทำไม PostgreSQL:
- ฟรี 100% open-source
- Features มากกว่า SQL Server ในหลายด้าน (JSONB, Full-text search, Arrays)
- Performance ดีมาก
- Cloud support: AWS RDS, Azure, GCP
```

```bash
dotnet add package Npgsql.EntityFrameworkCore.PostgreSQL
```

```csharp
// Connection String
"Host=localhost;Database=MyApp;Username=postgres;Password=YourPass123!"

// EF Core
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString, npgsql =>
    {
        npgsql.EnableRetryOnFailure(3);
        npgsql.CommandTimeout(30);
    }));

// PostgreSQL-specific: JSONB columns
public class Product
{
    public int Id { get; set; }
    public string Name { get; set; }
    public JsonDocument? Metadata { get; set; }    // เก็บ JSON ใน column!
}

modelBuilder.Entity<Product>()
    .Property(p => p.Metadata)
    .HasColumnType("jsonb");
```

---

## 3. MongoDB (NoSQL)

```
ใช้เมื่อ:
- ข้อมูล schema ไม่ตายตัว (flexible schema)
- Document-based data (JSON-like)
- High write throughput
- Logs, events, IoT data
```

```bash
dotnet add package MongoDB.Driver
```

```csharp
// Model
public class AuditLog
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public int EntityId { get; set; }
    public BsonDocument? OldValues { get; set; }
    public BsonDocument? NewValues { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

// Service
public class AuditLogService
{
    private readonly IMongoCollection<AuditLog> _collection;

    public AuditLogService(IConfiguration config)
    {
        var client = new MongoClient(config.GetConnectionString("MongoDB"));
        var database = client.GetDatabase("MyApp");
        _collection = database.GetCollection<AuditLog>("audit_logs");
    }

    public async Task LogAsync(string action, string entityType, int entityId, object? oldValues, object? newValues)
    {
        await _collection.InsertOneAsync(new AuditLog
        {
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            OldValues = oldValues?.ToBsonDocument(),
            NewValues = newValues?.ToBsonDocument()
        });
    }

    public async Task<List<AuditLog>> GetByEntityAsync(string entityType, int entityId)
    {
        return await _collection
            .Find(l => l.EntityType == entityType && l.EntityId == entityId)
            .SortByDescending(l => l.CreatedAt)
            .Limit(50)
            .ToListAsync();
    }
}

// Program.cs
builder.Services.AddSingleton<AuditLogService>();
```

---

## 4. เลือก Database

```
| สถานการณ์                          | Database      |
|------------------------------------|---------------|
| .NET Enterprise API (default)      | SQL Server ✅ |
| Open-source / budget               | PostgreSQL ✅ |
| Flexible schema / logs             | MongoDB       |
| Cache / session                    | Redis         |
| Embedded / testing                 | SQLite        |
| Enterprise + JSON flexibility      | PostgreSQL ✅ |

Enterprise API ทั่วไป:
- Primary: SQL Server หรือ PostgreSQL
- Cache: Redis
- Logs/Audit: MongoDB หรือ Elasticsearch
- Search: Elasticsearch
```
