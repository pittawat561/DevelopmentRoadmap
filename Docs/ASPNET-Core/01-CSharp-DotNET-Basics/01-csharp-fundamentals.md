# C# Fundamentals สำหรับ Enterprise API

> พื้นฐาน C# ที่ต้องรู้ก่อนสร้าง API — อ่านจบแล้วเขียน C# ได้จริง

---

## 1. C# คืออะไร

```
C# (อ่านว่า "ซี-ชาร์ป") คือภาษา programming ของ Microsoft
- สร้างโดย Anders Hejlsberg (คนเดียวกับที่สร้าง TypeScript)
- ทำงานบน .NET platform
- ใช้สร้าง: Web APIs, Desktop apps, Mobile apps, Games (Unity)
- Type-safe, Object-Oriented, ทันสมัย

.NET คืออะไร:
- Platform สำหรับรัน C# (เหมือน JVM สำหรับ Java)
- .NET = runtime + libraries + tools
- Cross-platform: Windows, Linux, macOS

Timeline:
.NET Framework (Windows only) → .NET Core (cross-platform) → .NET 5/6/7/8/9 (unified)
ปัจจุบัน: .NET 9 (2024) — ใช้ตัวนี้!
```

### ติดตั้ง

```bash
# ดาวน์โหลดจาก https://dotnet.microsoft.com/download
# หรือ

# Windows (winget)
winget install Microsoft.DotNet.SDK.9

# macOS
brew install dotnet-sdk

# ตรวจสอบ
dotnet --version        # 9.x.x
dotnet --list-sdks      # ดู SDK ที่ติดตั้ง
```

### .NET CLI — คำสั่งที่ใช้ทุกวัน

```bash
# สร้างโปรเจกต์ใหม่
dotnet new console -n MyApp              # Console app
dotnet new webapi -n MyApi               # Web API ⭐
dotnet new classlib -n MyLibrary         # Class library
dotnet new sln -n MySolution             # Solution file

# จัดการ Solution
dotnet sln add MyApi/MyApi.csproj
dotnet sln add MyLibrary/MyLibrary.csproj

# รันโปรเจกต์
dotnet run                               # รัน project ปัจจุบัน
dotnet run --project MyApi               # รัน project ที่ระบุ
dotnet watch run                         # Hot reload (auto restart เมื่อแก้ code)

# Build
dotnet build                             # build debug
dotnet build -c Release                  # build release
dotnet publish -c Release -o ./publish   # publish สำหรับ deploy

# NuGet packages (เหมือน npm)
dotnet add package Newtonsoft.Json
dotnet add package Microsoft.EntityFrameworkCore
dotnet remove package Newtonsoft.Json
dotnet restore                           # restore packages

# Test
dotnet test                              # รัน tests ทั้งหมด
```

---

## 2. Syntax พื้นฐาน C#

### Variables & Data Types

```csharp
// ===== Value Types (เก็บค่าตรงๆ) =====
int age = 30;                    // จำนวนเต็ม (-2.1 พันล้าน ถึง 2.1 พันล้าน)
long bigNumber = 9999999999L;    // จำนวนเต็มใหญ่
double price = 99.99;            // ทศนิยม (15-16 digits)
decimal money = 99.99m;          // ทศนิยมแม่นยำ (28-29 digits) ← ใช้กับเงิน!
float temperature = 36.5f;       // ทศนิยม (7 digits)
bool isActive = true;            // true / false
char grade = 'A';                // ตัวอักษรเดียว
byte status = 255;               // 0-255

// ===== Reference Types (เก็บ reference ไปยังข้อมูล) =====
string name = "สมชาย";           // ข้อความ
string? nullableName = null;     // nullable string (อาจเป็น null)
object anything = 42;            // เก็บอะไรก็ได้
int[] numbers = { 1, 2, 3 };    // array

// ===== var — ให้ compiler กำหนด type เอง =====
var count = 10;                  // compiler รู้ว่าเป็น int
var message = "hello";           // compiler รู้ว่าเป็น string
var users = new List<string>();  // compiler รู้ว่าเป็น List<string>

// ===== const & readonly =====
const double PI = 3.14159;       // ค่าคงที่ (compile-time)
readonly string serverName;      // กำหนดค่าได้ใน constructor เท่านั้น

// ===== Nullable Types =====
int? nullableAge = null;         // int ที่เป็น null ได้
if (nullableAge.HasValue)
{
    Console.WriteLine(nullableAge.Value);
}
int actualAge = nullableAge ?? 0;  // ถ้า null ใช้ 0 แทน (null coalescing)
```

### String Operations

```csharp
// String Interpolation (วิธีที่ดีที่สุด)
string name = "John";
int age = 30;
string greeting = $"Hello {name}, you are {age} years old";
string multiline = $"""
    Hello {name},
    Welcome to our system.
    Your age is {age}.
    """;

// String Methods ที่ใช้บ่อย
string text = "  Hello World  ";
text.Trim()                      // "Hello World" (ตัด whitespace)
text.ToUpper()                   // "  HELLO WORLD  "
text.ToLower()                   // "  hello world  "
text.Contains("World")           // true
text.StartsWith("  Hello")       // true
text.Replace("World", "C#")      // "  Hello C#  "
text.Split(' ')                  // ["", "", "Hello", "World", "", ""]
text.Substring(2, 5)             // "Hello"

string.IsNullOrEmpty(text)       // false
string.IsNullOrWhiteSpace("")    // true

// String Builder (สำหรับต่อ string จำนวนมาก — เร็วกว่า +)
var sb = new StringBuilder();
sb.Append("Hello");
sb.Append(" ");
sb.AppendLine("World");
sb.AppendLine($"Count: {42}");
string result = sb.ToString();
```

### Control Flow

```csharp
// ===== if / else =====
if (age >= 18)
{
    Console.WriteLine("ผู้ใหญ่");
}
else if (age >= 13)
{
    Console.WriteLine("วัยรุ่น");
}
else
{
    Console.WriteLine("เด็ก");
}

// Ternary operator
string status = age >= 18 ? "ผู้ใหญ่" : "เด็ก";

// ===== Switch (modern pattern matching) =====
string GetDiscount(string customerType) => customerType switch
{
    "VIP"      => "30% off",
    "Premium"  => "20% off",
    "Regular"  => "10% off",
    _          => "No discount"      // default
};

// Switch with conditions
string GetCategory(int score) => score switch
{
    >= 90 => "A",
    >= 80 => "B",
    >= 70 => "C",
    >= 60 => "D",
    _     => "F"
};

// ===== Loops =====
// for
for (int i = 0; i < 10; i++)
{
    Console.WriteLine(i);
}

// foreach (ใช้บ่อยที่สุด)
var names = new List<string> { "John", "Jane", "Bob" };
foreach (var n in names)
{
    Console.WriteLine(n);
}

// while
int count = 0;
while (count < 5)
{
    count++;
}
```

### Collections

```csharp
// ===== List<T> — ใช้บ่อยที่สุด =====
var users = new List<string>();
users.Add("John");
users.Add("Jane");
users.AddRange(new[] { "Bob", "Alice" });
users.Remove("Bob");
users.RemoveAt(0);                // ลบ index 0
bool hasJane = users.Contains("Jane");
int count = users.Count;

// Initialize
var numbers = new List<int> { 1, 2, 3, 4, 5 };

// ===== Dictionary<TKey, TValue> — key-value =====
var config = new Dictionary<string, string>
{
    ["host"] = "localhost",
    ["port"] = "5432",
    ["database"] = "mydb"
};

config["timeout"] = "30";         // เพิ่ม/แก้ไข
string host = config["host"];     // อ่าน (throw ถ้าไม่มี key)

// ปลอดภัยกว่า:
if (config.TryGetValue("host", out string? value))
{
    Console.WriteLine(value);
}

// ===== HashSet<T> — ไม่ซ้ำ =====
var tags = new HashSet<string> { "csharp", "api", "dotnet" };
tags.Add("csharp");               // ไม่เพิ่ม (มีแล้ว)
tags.Contains("api");             // true — O(1) เร็วมาก!

// ===== Array =====
int[] scores = new int[5];        // ขนาดคงที่
string[] names = { "A", "B", "C" };
```

---

## 3. OOP — Object-Oriented Programming

### Classes & Objects

```csharp
// ===== Class พื้นฐาน =====
public class User
{
    // Properties (ใช้แทน fields)
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Read-only property
    public string DisplayName => $"{Name} ({Email})";

    // Private field
    private string _passwordHash = string.Empty;

    // Constructor
    public User() { }

    public User(string name, string email)
    {
        Name = name;
        Email = email;
    }

    // Methods
    public void SetPassword(string password)
    {
        _passwordHash = BCrypt.Net.BCrypt.HashPassword(password);
    }

    public bool VerifyPassword(string password)
    {
        return BCrypt.Net.BCrypt.Verify(password, _passwordHash);
    }

    // Override ToString
    public override string ToString() => $"User: {Name} <{Email}>";
}

// ใช้งาน:
var user = new User("John", "john@example.com");
var user2 = new User { Id = 1, Name = "Jane", Email = "jane@example.com" };
```

### Inheritance & Interfaces

```csharp
// ===== Interface — สัญญาว่า class ต้องมีอะไรบ้าง =====
public interface IUserRepository
{
    Task<User?> GetByIdAsync(int id);
    Task<List<User>> GetAllAsync();
    Task<User> CreateAsync(User user);
    Task UpdateAsync(User user);
    Task DeleteAsync(int id);
}

public interface IAuditable
{
    DateTime CreatedAt { get; set; }
    DateTime? UpdatedAt { get; set; }
    string CreatedBy { get; set; }
}

// ===== Abstract Class — class ที่ instantiate ตรงๆ ไม่ได้ =====
public abstract class BaseEntity
{
    public int Id { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // Abstract method — ต้อง implement ใน subclass
    public abstract string GetDisplayName();

    // Virtual method — override ได้ แต่ไม่บังคับ
    public virtual string GetEntityType() => GetType().Name;
}

// ===== Implement =====
public class User : BaseEntity, IAuditable
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string CreatedBy { get; set; } = string.Empty;

    public override string GetDisplayName() => $"{Name} ({Email})";
}

public class Product : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public decimal Price { get; set; }

    public override string GetDisplayName() => $"{Title} - {Price:C}";
}

// ===== Implement Interface =====
public class UserRepository : IUserRepository
{
    private readonly AppDbContext _context;

    public UserRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<User?> GetByIdAsync(int id)
        => await _context.Users.FindAsync(id);

    public async Task<List<User>> GetAllAsync()
        => await _context.Users.ToListAsync();

    public async Task<User> CreateAsync(User user)
    {
        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        return user;
    }

    public async Task UpdateAsync(User user)
    {
        _context.Users.Update(user);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var user = await GetByIdAsync(id);
        if (user is not null)
        {
            _context.Users.Remove(user);
            await _context.SaveChangesAsync();
        }
    }
}
```

### Records (สำหรับ DTOs — ใช้เยอะใน API)

```csharp
// Record = immutable class ที่เหมาะกับ data transfer
// auto-generate: Equals, GetHashCode, ToString

// ===== Record class (reference type) =====
public record UserDto(int Id, string Name, string Email);

// ใช้:
var dto = new UserDto(1, "John", "john@example.com");
var dto2 = dto with { Name = "Jane" };  // สร้างใหม่ เปลี่ยนแค่ Name

// ===== Record สำหรับ API Requests =====
public record CreateUserRequest(string Name, string Email, string Password);
public record UpdateUserRequest(string? Name, string? Email);
public record LoginRequest(string Email, string Password);

// ===== Record สำหรับ API Responses =====
public record UserResponse(int Id, string Name, string Email, DateTime CreatedAt);
public record ApiResponse<T>(bool Success, T? Data, string? Error = null);
public record PaginatedResponse<T>(
    List<T> Items,
    int TotalCount,
    int Page,
    int PageSize
);
```

---

## 4. Async/Await — สำคัญมากสำหรับ API

```csharp
// API ทุกตัวใช้ async เพราะ:
// - Database queries ใช้เวลา
// - HTTP calls ใช้เวลา
// - File I/O ใช้เวลา
// async ทำให้ server รับ request อื่นได้ขณะรอ

// ===== พื้นฐาน =====
// ❌ Synchronous (block thread)
public User GetUser(int id)
{
    return _context.Users.Find(id);      // block! thread ทำอย่างอื่นไม่ได้
}

// ✅ Asynchronous (ไม่ block thread)
public async Task<User?> GetUserAsync(int id)
{
    return await _context.Users.FindAsync(id);  // ปล่อย thread ไปทำอย่างอื่น
}

// ===== กฎ Async =====
// 1. method ที่มี await → ต้องใส่ async
// 2. return type: Task (ไม่มีค่าคืน), Task<T> (มีค่าคืน)
// 3. ชื่อ method ลงท้ายด้วย Async (convention)

// คืนค่า
public async Task<string> GetGreetingAsync(string name)
{
    var user = await _userRepo.GetByNameAsync(name);
    return $"Hello, {user.Name}!";
}

// ไม่คืนค่า
public async Task SendEmailAsync(string to, string subject)
{
    await _emailService.SendAsync(to, subject);
}

// ===== หลายงานพร้อมกัน =====
// รัน parallel (เร็วกว่า!)
public async Task<DashboardData> GetDashboardAsync()
{
    // เริ่มทั้ง 3 งานพร้อมกัน
    var usersTask = _userRepo.GetCountAsync();
    var ordersTask = _orderRepo.GetTodayCountAsync();
    var revenueTask = _orderRepo.GetTodayRevenueAsync();

    // รอทั้ง 3 เสร็จ
    await Task.WhenAll(usersTask, ordersTask, revenueTask);

    return new DashboardData
    {
        UserCount = await usersTask,
        OrderCount = await ordersTask,
        Revenue = await revenueTask
    };
}

// ===== Error Handling =====
public async Task<User> CreateUserAsync(CreateUserRequest request)
{
    try
    {
        var user = new User { Name = request.Name, Email = request.Email };
        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();
        return user;
    }
    catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("duplicate") == true)
    {
        throw new ConflictException($"Email {request.Email} already exists");
    }
}
```

---

## 5. LINQ — Query ข้อมูลแบบ C#

```csharp
// LINQ = Language Integrated Query
// ใช้ query ข้อมูลจาก collections, database, XML, etc.
// ⭐ ใช้ทุกวันเมื่อทำ API

var users = new List<User>
{
    new() { Id = 1, Name = "John",  Email = "john@test.com",  Age = 30, IsActive = true },
    new() { Id = 2, Name = "Jane",  Email = "jane@test.com",  Age = 25, IsActive = true },
    new() { Id = 3, Name = "Bob",   Email = "bob@test.com",   Age = 35, IsActive = false },
    new() { Id = 4, Name = "Alice", Email = "alice@test.com", Age = 28, IsActive = true },
};

// ===== Where (กรอง) =====
var activeUsers = users.Where(u => u.IsActive);
var adults = users.Where(u => u.Age >= 30);
var johnOrJane = users.Where(u => u.Name == "John" || u.Name == "Jane");

// ===== Select (เลือก/แปลง) =====
var names = users.Select(u => u.Name);                    // ["John", "Jane", "Bob", "Alice"]
var dtos = users.Select(u => new UserDto(u.Id, u.Name, u.Email));

// ===== OrderBy / OrderByDescending (เรียงลำดับ) =====
var byName = users.OrderBy(u => u.Name);
var byAgeDesc = users.OrderByDescending(u => u.Age);
var byNameThenAge = users.OrderBy(u => u.Name).ThenBy(u => u.Age);

// ===== First / Single / FirstOrDefault =====
var first = users.First();                                // ตัวแรก (throw ถ้าว่าง)
var firstOrNull = users.FirstOrDefault(u => u.Age > 100); // null ถ้าไม่มี
var single = users.Single(u => u.Id == 1);                // ต้องมีตัวเดียว (throw ถ้าไม่ใช่)

// ===== Any / All / Count =====
bool hasActive = users.Any(u => u.IsActive);              // true
bool allAdult = users.All(u => u.Age >= 18);              // true
int activeCount = users.Count(u => u.IsActive);           // 3

// ===== GroupBy =====
var byStatus = users.GroupBy(u => u.IsActive)
    .Select(g => new { Status = g.Key, Count = g.Count() });

// ===== Skip / Take (Pagination!) =====
int page = 2;
int pageSize = 10;
var paged = users
    .Skip((page - 1) * pageSize)
    .Take(pageSize)
    .ToList();

// ===== Aggregate =====
int totalAge = users.Sum(u => u.Age);
double avgAge = users.Average(u => u.Age);
int maxAge = users.Max(u => u.Age);
int minAge = users.Min(u => u.Age);

// ===== Chaining (ใช้ร่วมกัน — ใช้บ่อยมากใน API!) =====
var result = users
    .Where(u => u.IsActive)
    .OrderBy(u => u.Name)
    .Select(u => new UserDto(u.Id, u.Name, u.Email))
    .Skip(0)
    .Take(10)
    .ToList();

// ===== LINQ กับ Entity Framework (Database) =====
// เหมือนกันเลย! แต่แปลงเป็น SQL อัตโนมัติ
var activeUsers2 = await _context.Users
    .Where(u => u.IsActive)
    .OrderBy(u => u.CreatedAt)
    .Select(u => new UserDto(u.Id, u.Name, u.Email))
    .Skip(0)
    .Take(10)
    .ToListAsync();
// → SELECT Id, Name, Email FROM Users WHERE IsActive = 1 ORDER BY CreatedAt LIMIT 10
```

---

## 6. Exception Handling

```csharp
// ===== Custom Exceptions สำหรับ API =====
public class NotFoundException : Exception
{
    public NotFoundException(string entity, object id)
        : base($"{entity} with id '{id}' was not found.") { }
}

public class ConflictException : Exception
{
    public ConflictException(string message) : base(message) { }
}

public class ValidationException : Exception
{
    public Dictionary<string, string[]> Errors { get; }

    public ValidationException(Dictionary<string, string[]> errors)
        : base("Validation failed")
    {
        Errors = errors;
    }
}

public class UnauthorizedException : Exception
{
    public UnauthorizedException(string message = "Unauthorized") : base(message) { }
}

// ===== ใช้ =====
public async Task<User> GetUserAsync(int id)
{
    var user = await _context.Users.FindAsync(id);
    if (user is null)
        throw new NotFoundException("User", id);
    return user;
}

// ===== Try-Catch =====
try
{
    var user = await GetUserAsync(999);
}
catch (NotFoundException ex)
{
    // 404
    Console.WriteLine(ex.Message);
}
catch (Exception ex)
{
    // 500 — unexpected error
    _logger.LogError(ex, "Unexpected error");
}
finally
{
    // ทำเสมอ ไม่ว่าจะ error หรือไม่
}
```

---

## 7. Generics & Dependency Injection Preview

```csharp
// ===== Generics — ใช้ type เป็น parameter =====
// ใช้เยอะมากใน Enterprise API

// Generic Repository (ใช้กับ entity อะไรก็ได้)
public interface IRepository<T> where T : BaseEntity
{
    Task<T?> GetByIdAsync(int id);
    Task<List<T>> GetAllAsync();
    Task<T> AddAsync(T entity);
    Task UpdateAsync(T entity);
    Task DeleteAsync(int id);
}

public class Repository<T> : IRepository<T> where T : BaseEntity
{
    private readonly AppDbContext _context;
    private readonly DbSet<T> _dbSet;

    public Repository(AppDbContext context)
    {
        _context = context;
        _dbSet = context.Set<T>();
    }

    public async Task<T?> GetByIdAsync(int id)
        => await _dbSet.FindAsync(id);

    public async Task<List<T>> GetAllAsync()
        => await _dbSet.ToListAsync();

    public async Task<T> AddAsync(T entity)
    {
        await _dbSet.AddAsync(entity);
        await _context.SaveChangesAsync();
        return entity;
    }

    public async Task UpdateAsync(T entity)
    {
        _dbSet.Update(entity);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var entity = await GetByIdAsync(id);
        if (entity is not null)
        {
            _dbSet.Remove(entity);
            await _context.SaveChangesAsync();
        }
    }
}

// ใช้ได้กับทุก Entity:
// IRepository<User> userRepo
// IRepository<Product> productRepo
// IRepository<Order> orderRepo
// ไม่ต้องเขียน repository ใหม่สำหรับแต่ละ entity!

// ===== Generic API Response =====
public record ApiResponse<T>(
    bool Success,
    T? Data,
    string? Message = null,
    Dictionary<string, string[]>? Errors = null
)
{
    public static ApiResponse<T> Ok(T data, string? message = null)
        => new(true, data, message);

    public static ApiResponse<T> Fail(string message)
        => new(false, default, message);

    public static ApiResponse<T> ValidationFail(Dictionary<string, string[]> errors)
        => new(false, default, "Validation failed", errors);
}
```

---

## 8. สรุป — สิ่งที่ต้องรู้ก่อนไป ASP.NET Core

```
✅ ต้องเข้าใจ:
├── Variables, Data Types, Nullable
├── Collections (List, Dictionary)
├── OOP (Classes, Interfaces, Inheritance)
├── Records (สำหรับ DTOs)
├── Async/Await (ใช้ทุก method ใน API)
├── LINQ (query ข้อมูล)
├── Exception Handling
├── Generics
└── String Interpolation

📁 โครงสร้าง Enterprise API ที่จะได้เรียน:
MyApi/
├── Controllers/          ← รับ HTTP requests
├── Services/             ← Business logic
├── Repositories/         ← Database operations
├── Models/
│   ├── Entities/         ← Database entities
│   ├── DTOs/             ← Data Transfer Objects (Records)
│   └── Requests/         ← API request models
├── Middleware/            ← Cross-cutting concerns
├── Extensions/           ← Service registration
└── Program.cs            ← Entry point + configuration
```
