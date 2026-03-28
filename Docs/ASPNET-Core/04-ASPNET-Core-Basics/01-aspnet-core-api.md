# ASP.NET Core Web API — สร้าง Enterprise API ฉบับเต็ม

> อ่านจบแล้วสร้าง REST API ที่ใช้งานจริงในองค์กรได้

---

## 1. สร้าง Web API แรก

### สร้างโปรเจกต์

```bash
# สร้าง Solution
dotnet new sln -n EnterpriseApi

# สร้าง Web API project
dotnet new webapi -n EnterpriseApi.Api --use-controllers

# เพิ่มเข้า solution
dotnet sln add EnterpriseApi.Api

# รัน
cd EnterpriseApi.Api
dotnet watch run
# → https://localhost:5001/swagger  (Swagger UI)
```

### โครงสร้างโปรเจกต์

```
EnterpriseApi.Api/
├── Controllers/           ← รับ HTTP requests
│   └── UsersController.cs
├── Models/
│   ├── Entities/          ← Database models
│   │   └── User.cs
│   ├── DTOs/              ← Data Transfer Objects
│   │   └── UserDto.cs
│   └── Requests/          ← Request models
│       └── CreateUserRequest.cs
├── Services/              ← Business logic
│   ├── IUserService.cs
│   └── UserService.cs
├── Data/                  ← Database context
│   └── AppDbContext.cs
├── Middleware/             ← Cross-cutting concerns
│   └── ExceptionMiddleware.cs
├── Extensions/            ← Service registration
│   └── ServiceExtensions.cs
├── appsettings.json       ← Configuration
└── Program.cs             ← Entry point
```

### Program.cs — จุดเริ่มต้น

```csharp
// Program.cs — .NET 8+ (Minimal Hosting)
var builder = WebApplication.CreateBuilder(args);

// ===== 1. Register Services (Dependency Injection) =====
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Database
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Custom Services
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IOrderService, OrderService>();

var app = builder.Build();

// ===== 2. Configure Middleware Pipeline (ลำดับสำคัญ!) =====
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthentication();     // ใครเป็นใคร
app.UseAuthorization();      // มีสิทธิ์ไหม

app.MapControllers();        // Map routes จาก Controllers

app.Run();
```

### appsettings.json

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=EnterpriseDb;Trusted_Connection=true;TrustServerCertificate=true;"
  },
  "Jwt": {
    "Key": "YourSuperSecretKeyThatIsAtLeast32Characters!",
    "Issuer": "EnterpriseApi",
    "Audience": "EnterpriseApi",
    "ExpireMinutes": 60
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  }
}
```

---

## 2. Controllers — รับ HTTP Requests

```csharp
// Controllers/UsersController.cs
using Microsoft.AspNetCore.Mvc;

[ApiController]                          // เปิด automatic model validation
[Route("api/[controller]")]              // → /api/users
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly ILogger<UsersController> _logger;

    // Constructor Injection
    public UsersController(IUserService userService, ILogger<UsersController> logger)
    {
        _userService = userService;
        _logger = logger;
    }

    // GET /api/users
    [HttpGet]
    [ProducesResponseType(typeof(PaginatedResponse<UserDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? search = null,
        [FromQuery] string? sortBy = "CreatedAt",
        [FromQuery] bool sortDesc = true)
    {
        var result = await _userService.GetAllAsync(page, pageSize, search, sortBy, sortDesc);
        return Ok(result);
    }

    // GET /api/users/5
    [HttpGet("{id:int}")]                // route constraint: id ต้องเป็น int
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id)
    {
        var user = await _userService.GetByIdAsync(id);
        return Ok(user);                 // ถ้าไม่เจอ → Service throw NotFoundException
    }

    // POST /api/users
    [HttpPost]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateUserRequest request)
    {
        var user = await _userService.CreateAsync(request);
        return CreatedAtAction(
            nameof(GetById),
            new { id = user.Id },
            user
        );
        // → 201 Created + Location header: /api/users/5
    }

    // PUT /api/users/5
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateUserRequest request)
    {
        var user = await _userService.UpdateAsync(id, request);
        return Ok(user);
    }

    // DELETE /api/users/5
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id)
    {
        await _userService.DeleteAsync(id);
        return NoContent();              // 204 — ลบสำเร็จ ไม่มี content
    }

    // GET /api/users/5/orders
    [HttpGet("{id:int}/orders")]
    public async Task<IActionResult> GetUserOrders(int id)
    {
        var orders = await _userService.GetOrdersAsync(id);
        return Ok(orders);
    }
}
```

### HTTP Status Codes ที่ใช้บ่อย

```
| Status Code | Method    | ใช้เมื่อ                        |
|-------------|-----------|--------------------------------|
| 200 OK      | GET/PUT   | สำเร็จ + มี response body      |
| 201 Created | POST      | สร้างสำเร็จ                     |
| 204 No Content | DELETE | ลบสำเร็จ ไม่มี body             |
| 400 Bad Request | POST/PUT | input ไม่ถูกต้อง            |
| 401 Unauthorized | ทุกอัน | ไม่ได้ login                  |
| 403 Forbidden | ทุกอัน   | login แล้วแต่ไม่มีสิทธิ์        |
| 404 Not Found | GET/PUT/DELETE | ไม่เจอ resource          |
| 409 Conflict | POST     | ข้อมูลซ้ำ (duplicate email)     |
| 422 Unprocessable | POST/PUT | validation error          |
| 500 Internal Server Error | ทุกอัน | server error ที่ไม่คาดคิด |
```

---

## 3. Models — Entities, DTOs, Requests

```csharp
// ===== Entity (ตรงกับ Database) =====
// Models/Entities/User.cs
public class User
{
    public int Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Role { get; set; } = "User";
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // Navigation properties (relationships)
    public ICollection<Order> Orders { get; set; } = new List<Order>();

    // Computed property
    public string FullName => $"{FirstName} {LastName}";
}

public class Order
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public decimal Total { get; set; }
    public string Status { get; set; } = "Pending";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public User User { get; set; } = null!;
    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
}

// ===== DTOs (ส่งกลับ client — ห้ามส่ง Entity ตรงๆ!) =====
// Models/DTOs/UserDto.cs
public record UserDto(
    int Id,
    string FirstName,
    string LastName,
    string Email,
    string Role,
    bool IsActive,
    DateTime CreatedAt
);

public record UserDetailDto(
    int Id,
    string FirstName,
    string LastName,
    string Email,
    string Role,
    bool IsActive,
    DateTime CreatedAt,
    List<OrderSummaryDto> RecentOrders
);

public record OrderSummaryDto(int Id, string OrderNumber, decimal Total, string Status);

// ===== Requests (รับจาก client) =====
// Models/Requests/CreateUserRequest.cs
public record CreateUserRequest(
    string FirstName,
    string LastName,
    string Email,
    string Password,
    string? Role
);

public record UpdateUserRequest(
    string? FirstName,
    string? LastName,
    string? Email,
    string? Role,
    bool? IsActive
);

public record LoginRequest(string Email, string Password);

// ===== Responses =====
public record PaginatedResponse<T>(
    List<T> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages
)
{
    public bool HasPreviousPage => Page > 1;
    public bool HasNextPage => Page < TotalPages;
}

public record ApiError(string Message, Dictionary<string, string[]>? Errors = null);
```

---

## 4. Services — Business Logic

```csharp
// Services/IUserService.cs
public interface IUserService
{
    Task<PaginatedResponse<UserDto>> GetAllAsync(int page, int pageSize, string? search, string? sortBy, bool sortDesc);
    Task<UserDetailDto> GetByIdAsync(int id);
    Task<UserDto> CreateAsync(CreateUserRequest request);
    Task<UserDto> UpdateAsync(int id, UpdateUserRequest request);
    Task DeleteAsync(int id);
}

// Services/UserService.cs
public class UserService : IUserService
{
    private readonly AppDbContext _context;
    private readonly ILogger<UserService> _logger;

    public UserService(AppDbContext context, ILogger<UserService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<PaginatedResponse<UserDto>> GetAllAsync(
        int page, int pageSize, string? search, string? sortBy, bool sortDesc)
    {
        var query = _context.Users.AsQueryable();

        // Search
        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(u =>
                u.FirstName.Contains(search) ||
                u.LastName.Contains(search) ||
                u.Email.Contains(search));
        }

        // Count ก่อน sort/page
        var totalCount = await query.CountAsync();

        // Sort
        query = sortBy?.ToLower() switch
        {
            "name"  => sortDesc ? query.OrderByDescending(u => u.FirstName) : query.OrderBy(u => u.FirstName),
            "email" => sortDesc ? query.OrderByDescending(u => u.Email) : query.OrderBy(u => u.Email),
            _       => sortDesc ? query.OrderByDescending(u => u.CreatedAt) : query.OrderBy(u => u.CreatedAt),
        };

        // Pagination
        var users = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(u => new UserDto(u.Id, u.FirstName, u.LastName, u.Email, u.Role, u.IsActive, u.CreatedAt))
            .ToListAsync();

        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        return new PaginatedResponse<UserDto>(users, totalCount, page, pageSize, totalPages);
    }

    public async Task<UserDetailDto> GetByIdAsync(int id)
    {
        var user = await _context.Users
            .Include(u => u.Orders.OrderByDescending(o => o.CreatedAt).Take(5))
            .FirstOrDefaultAsync(u => u.Id == id);

        if (user is null)
            throw new NotFoundException("User", id);

        return new UserDetailDto(
            user.Id, user.FirstName, user.LastName, user.Email,
            user.Role, user.IsActive, user.CreatedAt,
            user.Orders.Select(o => new OrderSummaryDto(o.Id, o.OrderNumber, o.Total, o.Status)).ToList()
        );
    }

    public async Task<UserDto> CreateAsync(CreateUserRequest request)
    {
        // Check duplicate email
        if (await _context.Users.AnyAsync(u => u.Email == request.Email))
            throw new ConflictException($"Email '{request.Email}' already exists");

        var user = new User
        {
            FirstName = request.FirstName,
            LastName = request.LastName,
            Email = request.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Role = request.Role ?? "User"
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        _logger.LogInformation("User created: {Email}", user.Email);

        return new UserDto(user.Id, user.FirstName, user.LastName, user.Email, user.Role, user.IsActive, user.CreatedAt);
    }

    public async Task<UserDto> UpdateAsync(int id, UpdateUserRequest request)
    {
        var user = await _context.Users.FindAsync(id)
            ?? throw new NotFoundException("User", id);

        // อัปเดตเฉพาะ field ที่ส่งมา (partial update)
        if (request.FirstName is not null) user.FirstName = request.FirstName;
        if (request.LastName is not null) user.LastName = request.LastName;
        if (request.Email is not null) user.Email = request.Email;
        if (request.Role is not null) user.Role = request.Role;
        if (request.IsActive.HasValue) user.IsActive = request.IsActive.Value;

        user.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return new UserDto(user.Id, user.FirstName, user.LastName, user.Email, user.Role, user.IsActive, user.CreatedAt);
    }

    public async Task DeleteAsync(int id)
    {
        var user = await _context.Users.FindAsync(id)
            ?? throw new NotFoundException("User", id);

        // Soft delete
        user.IsActive = false;
        user.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
    }
}
```

---

## 5. Middleware — Cross-Cutting Concerns

```csharp
// ===== Global Exception Handler =====
// Middleware/ExceptionMiddleware.cs
public class ExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionMiddleware> _logger;

    public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);    // ส่งต่อไป middleware/controller ถัดไป
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var (statusCode, message) = exception switch
        {
            NotFoundException     => (StatusCodes.Status404NotFound, exception.Message),
            ConflictException     => (StatusCodes.Status409Conflict, exception.Message),
            ValidationException   => (StatusCodes.Status422UnprocessableEntity, exception.Message),
            UnauthorizedException => (StatusCodes.Status401Unauthorized, exception.Message),
            _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred")
        };

        // Log error
        if (statusCode == 500)
            _logger.LogError(exception, "Unhandled exception");
        else
            _logger.LogWarning("Handled exception: {Message}", exception.Message);

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";

        var error = new ApiError(message);
        await context.Response.WriteAsJsonAsync(error);
    }
}

// ลงทะเบียนใน Program.cs:
app.UseMiddleware<ExceptionMiddleware>();  // ก่อน UseAuthorization
```

---

## 6. Validation — ตรวจสอบ Input

```csharp
// ===== Data Annotations (พื้นฐาน) =====
public record CreateUserRequest(
    [Required(ErrorMessage = "First name is required")]
    [StringLength(100, MinimumLength = 2)]
    string FirstName,

    [Required]
    [StringLength(100, MinimumLength = 2)]
    string LastName,

    [Required]
    [EmailAddress(ErrorMessage = "Invalid email format")]
    string Email,

    [Required]
    [MinLength(8, ErrorMessage = "Password must be at least 8 characters")]
    string Password,

    string? Role
);

// ===== FluentValidation (แนะนำสำหรับ Enterprise!) =====
// dotnet add package FluentValidation.AspNetCore

public class CreateUserRequestValidator : AbstractValidator<CreateUserRequest>
{
    public CreateUserRequestValidator()
    {
        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("ชื่อห้ามว่าง")
            .MaximumLength(100)
            .Matches(@"^[a-zA-Z\s]+$").WithMessage("ชื่อต้องเป็นตัวอักษรเท่านั้น");

        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(255);

        RuleFor(x => x.Password)
            .NotEmpty()
            .MinimumLength(8)
            .Matches(@"[A-Z]").WithMessage("ต้องมีตัวอักษรพิมพ์ใหญ่อย่างน้อย 1 ตัว")
            .Matches(@"[a-z]").WithMessage("ต้องมีตัวอักษรพิมพ์เล็กอย่างน้อย 1 ตัว")
            .Matches(@"\d").WithMessage("ต้องมีตัวเลขอย่างน้อย 1 ตัว");

        RuleFor(x => x.Role)
            .Must(r => r is null or "Admin" or "User" or "Manager")
            .WithMessage("Role ต้องเป็น Admin, User, หรือ Manager");
    }
}

// ลงทะเบียน:
builder.Services.AddValidatorsFromAssemblyContaining<CreateUserRequestValidator>();
```

---

## 7. Authentication & Authorization (JWT)

```csharp
// ===== JWT Authentication =====
// dotnet add package Microsoft.AspNetCore.Authentication.JwtBearer

// ลงทะเบียนใน Program.cs:
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!))
        };
    });

// ===== Auth Service =====
public class AuthService : IAuthService
{
    private readonly AppDbContext _context;
    private readonly IConfiguration _config;

    public AuthService(AppDbContext context, IConfiguration config)
    {
        _context = context;
        _config = config;
    }

    public async Task<LoginResponse> LoginAsync(LoginRequest request)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email);

        if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            throw new UnauthorizedException("Invalid email or password");

        if (!user.IsActive)
            throw new UnauthorizedException("Account is deactivated");

        var token = GenerateJwtToken(user);

        return new LoginResponse(token, user.Email, user.Role);
    }

    private string GenerateJwtToken(User user)
    {
        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Name, user.FullName),
            new Claim(ClaimTypes.Role, user.Role),
        };

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(
                int.Parse(_config["Jwt:ExpireMinutes"]!)),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256)
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

// ===== Auth Controller =====
[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var result = await _authService.LoginAsync(request);
        return Ok(result);
    }
}

// ===== ใช้ [Authorize] ป้องกัน Endpoints =====
[ApiController]
[Route("api/[controller]")]
[Authorize]                              // ทุก endpoint ต้อง login
public class UsersController : ControllerBase
{
    // ทุกคนที่ login แล้วเข้าได้
    [HttpGet]
    public async Task<IActionResult> GetAll() { ... }

    // เฉพาะ Admin
    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id) { ... }

    // ดึง user ปัจจุบันจาก JWT token
    [HttpGet("me")]
    public async Task<IActionResult> GetCurrentUser()
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var user = await _userService.GetByIdAsync(userId);
        return Ok(user);
    }
}
```

---

## 8. Entity Framework Core (พื้นฐาน)

```csharp
// ===== DbContext =====
// Data/AppDbContext.cs
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<Product> Products => Set<Product>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // User configuration
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasIndex(e => e.Email).IsUnique();
            entity.Property(e => e.FirstName).HasMaxLength(100);
            entity.Property(e => e.LastName).HasMaxLength(100);
            entity.Property(e => e.Email).HasMaxLength(255);
        });

        // Order configuration
        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasIndex(e => e.OrderNumber).IsUnique();
            entity.Property(e => e.Total).HasPrecision(18, 2);

            entity.HasOne(e => e.User)
                  .WithMany(u => u.Orders)
                  .HasForeignKey(e => e.UserId);
        });

        // OrderItem
        modelBuilder.Entity<OrderItem>(entity =>
        {
            entity.Property(e => e.UnitPrice).HasPrecision(18, 2);

            entity.HasOne(e => e.Order)
                  .WithMany(o => o.Items)
                  .HasForeignKey(e => e.OrderId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Seed data
        modelBuilder.Entity<User>().HasData(
            new User { Id = 1, FirstName = "Admin", LastName = "User", Email = "admin@test.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin123!"), Role = "Admin" }
        );
    }
}

// ===== Migrations =====
// Terminal:
// dotnet ef migrations add InitialCreate
// dotnet ef database update
// dotnet ef migrations add AddProductTable
// dotnet ef database update
```

---

## 9. Minimal APIs (ทางเลือกแทน Controllers)

```csharp
// Program.cs — Minimal API style (ไม่ต้องมี Controllers/)
var app = builder.Build();

// GET /api/users
app.MapGet("/api/users", async (IUserService service, int page = 1, int pageSize = 10) =>
{
    var result = await service.GetAllAsync(page, pageSize, null, null, true);
    return Results.Ok(result);
});

// GET /api/users/{id}
app.MapGet("/api/users/{id:int}", async (int id, IUserService service) =>
{
    var user = await service.GetByIdAsync(id);
    return Results.Ok(user);
});

// POST /api/users
app.MapPost("/api/users", async (CreateUserRequest request, IUserService service) =>
{
    var user = await service.CreateAsync(request);
    return Results.Created($"/api/users/{user.Id}", user);
});

// PUT /api/users/{id}
app.MapPut("/api/users/{id:int}", async (int id, UpdateUserRequest request, IUserService service) =>
{
    var user = await service.UpdateAsync(id, request);
    return Results.Ok(user);
});

// DELETE /api/users/{id}
app.MapDelete("/api/users/{id:int}", async (int id, IUserService service) =>
{
    await service.DeleteAsync(id);
    return Results.NoContent();
}).RequireAuthorization("Admin");

// เมื่อไหร่ใช้อะไร:
// Controllers  → Enterprise apps, ซับซ้อน, หลาย endpoints
// Minimal APIs → Microservices, APIs เล็ก, prototype
```

---

## 10. สรุป — โครงสร้าง Enterprise API ที่ดี

```
Request Flow:
Client → Middleware (Exception, Auth, Logging)
       → Controller (รับ request, ส่ง response)
       → Service (business logic, validation)
       → Repository/DbContext (database)
       → Database

หลักการ:
✅ Controller บางที่สุด — แค่รับ-ส่ง
✅ Logic อยู่ใน Service
✅ ใช้ DTOs ส่งกลับ client (ห้ามส่ง Entity!)
✅ Async ทุก method ที่ติดต่อ DB/external service
✅ Global Exception Handler (ไม่ต้อง try-catch ทุก controller)
✅ FluentValidation สำหรับ input validation
✅ JWT สำหรับ authentication
✅ Dependency Injection ทุกที่

NuGet Packages สำคัญ:
- Microsoft.EntityFrameworkCore.SqlServer
- Microsoft.AspNetCore.Authentication.JwtBearer
- FluentValidation.AspNetCore
- BCrypt.Net-Next
- Serilog.AspNetCore
- AutoMapper (or Mapperly)
- Swashbuckle.AspNetCore (Swagger)
```
