# Dependency Injection (DI) — หัวใจของ ASP.NET Core

> อ่านจบแล้วเข้าใจ DI และออกแบบ service ที่ testable/maintainable ได้

---

## 1. DI คืออะไร ทำไมต้องใช้

### ปัญหาที่ไม่มี DI

```csharp
// ❌ ไม่มี DI — สร้าง dependency เอง (tight coupling)
public class UserService
{
    private readonly AppDbContext _context;
    private readonly EmailService _emailService;
    private readonly ILogger _logger;

    public UserService()
    {
        // สร้างเอง → ปัญหา!
        _context = new AppDbContext(new DbContextOptions<AppDbContext>());  // hard-coded connection
        _emailService = new EmailService("smtp.gmail.com", 587);           // hard-coded config
        _logger = new ConsoleLogger();                                      // hard-coded logger
    }
}

// ปัญหา:
// 1. เปลี่ยน EmailService เป็น SmsService ต้องแก้ UserService
// 2. Test ยาก — ใช้ real database, ส่ง email จริง
// 3. config hard-coded
// 4. แก้ 1 ที่ → แก้ทุกที่ที่สร้าง UserService
```

### แก้ด้วย DI

```csharp
// ✅ มี DI — รับ dependency จากภายนอก (loose coupling)
public class UserService : IUserService
{
    private readonly AppDbContext _context;
    private readonly IEmailService _emailService;          // Interface!
    private readonly ILogger<UserService> _logger;

    // Constructor Injection — ASP.NET Core ส่ง dependencies ให้อัตโนมัติ
    public UserService(
        AppDbContext context,
        IEmailService emailService,
        ILogger<UserService> logger)
    {
        _context = context;
        _emailService = emailService;
        _logger = logger;
    }
}

// ข้อดี:
// 1. เปลี่ยน implementation ได้ง่าย (EmailService → SmsService)
// 2. Test ง่าย — ใช้ Mock แทนของจริง
// 3. Config อยู่ที่เดียว (Program.cs)
// 4. แก้ 1 ที่ (registration) → ได้ผลทุกที่
```

---

## 2. Service Lifetimes — 3 แบบที่ต้องเข้าใจ

```csharp
// ===== 1. Transient — สร้างใหม่ทุกครั้งที่ request =====
builder.Services.AddTransient<IEmailService, EmailService>();

// ทุกครั้งที่มีคน inject IEmailService → สร้าง EmailService ใหม่
// ใช้กับ: Lightweight services, stateless operations

// ===== 2. Scoped — สร้าง 1 ครั้งต่อ HTTP request =====
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddDbContext<AppDbContext>(...);  // DbContext = Scoped โดย default!

// 1 HTTP request = 1 instance
// ถ้า Controller inject IUserService 2 ครั้ง → ได้ตัวเดียวกัน
// request ใหม่ → instance ใหม่
// ใช้กับ: Services ที่ทำงานกับ database, มี state per-request

// ===== 3. Singleton — สร้าง 1 ครั้งตลอด app lifetime =====
builder.Services.AddSingleton<ICacheService, MemoryCacheService>();
builder.Services.AddSingleton<IConfiguration>(builder.Configuration);

// สร้างครั้งเดียว ใช้ร่วมกันทุก request
// ⚠️ ต้อง thread-safe!
// ใช้กับ: Cache, Configuration, HttpClient factories
```

### เปรียบเทียบ 3 แบบ

```
Request 1:                    Request 2:
┌─────────────────────┐      ┌─────────────────────┐
│ Transient A (new)   │      │ Transient A (new)   │  ← ใหม่ทุก request
│ Transient B (new)   │      │ Transient B (new)   │     ใหม่ทุก injection
│                     │      │                     │
│ Scoped C (shared)   │      │ Scoped C (new)      │  ← ใหม่ทุก request
│ Scoped C (same!)    │      │ Scoped C (same!)    │     shared ใน request เดียว
│                     │      │                     │
│ Singleton D (same)  │      │ Singleton D (same!) │  ← ตัวเดียวกันตลอด!
└─────────────────────┘      └─────────────────────┘

กฎสำคัญ:
❌ Singleton ห้าม inject Scoped service (Captive Dependency!)
   Singleton อยู่ตลอด แต่ Scoped ควรถูกทำลายหลัง request
   → Scoped service จะไม่ถูก dispose → memory leak!

✅ Scoped สามารถ inject Singleton ได้
✅ Transient สามารถ inject ทุกอย่างได้

ง่ายๆ:
Transient → inject ได้ทุกอย่าง
Scoped    → inject Scoped + Singleton ได้
Singleton → inject Singleton เท่านั้น!
```

### เลือก Lifetime ยังไง

```
| Service Type              | Lifetime   | เหตุผล                          |
|---------------------------|------------|--------------------------------|
| DbContext                 | Scoped ✅  | 1 request = 1 unit of work     |
| IUserService              | Scoped ✅  | ทำงานกับ DbContext              |
| IOrderService             | Scoped ✅  | ทำงานกับ DbContext              |
| IRepository<T>            | Scoped ✅  | ทำงานกับ DbContext              |
| IEmailService             | Transient  | stateless, ไม่มี shared state   |
| IValidator<T>             | Transient  | stateless                      |
| ICacheService             | Singleton  | share cache ข้าม requests      |
| IConfiguration            | Singleton  | config ไม่เปลี่ยน              |
| HttpClient (via factory)  | Singleton  | reuse connections               |
| ILogger<T>                | Singleton  | built-in, thread-safe          |
```

---

## 3. ลงทะเบียน Services — ตัวอย่างจริง

```csharp
// ===== Program.cs =====
var builder = WebApplication.CreateBuilder(args);

// --- Database ---
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// --- Repositories ---
builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));  // Generic!
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IOrderRepository, OrderRepository>();

// --- Services ---
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<IAuthService, AuthService>();

// --- External Services ---
builder.Services.AddTransient<IEmailService, SmtpEmailService>();
builder.Services.AddTransient<IStorageService, AzureBlobStorageService>();

// --- Singletons ---
builder.Services.AddSingleton<ICacheService, MemoryCacheService>();
builder.Services.AddMemoryCache();

// --- HttpClient ---
builder.Services.AddHttpClient<IPaymentService, StripePaymentService>(client =>
{
    client.BaseAddress = new Uri("https://api.stripe.com/");
    client.DefaultRequestHeaders.Add("Authorization",
        $"Bearer {builder.Configuration["Stripe:SecretKey"]}");
});

// --- Validation ---
builder.Services.AddValidatorsFromAssemblyContaining<CreateUserRequestValidator>();

// --- Authentication ---
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options => { ... });

builder.Services.AddAuthorization();
builder.Services.AddControllers();
```

### จัดระเบียบด้วย Extension Methods

```csharp
// Extensions/ServiceExtensions.cs — แยก registration ออกจาก Program.cs
public static class ServiceExtensions
{
    public static IServiceCollection AddDatabase(this IServiceCollection services, IConfiguration config)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(config.GetConnectionString("DefaultConnection")));
        return services;
    }

    public static IServiceCollection AddRepositories(this IServiceCollection services)
    {
        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IOrderRepository, OrderRepository>();
        return services;
    }

    public static IServiceCollection AddBusinessServices(this IServiceCollection services)
    {
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IOrderService, OrderService>();
        services.AddScoped<IAuthService, AuthService>();
        return services;
    }

    public static IServiceCollection AddExternalServices(this IServiceCollection services, IConfiguration config)
    {
        services.AddTransient<IEmailService, SmtpEmailService>();
        services.AddSingleton<ICacheService, MemoryCacheService>();

        services.AddHttpClient<IPaymentService, StripePaymentService>(client =>
        {
            client.BaseAddress = new Uri(config["Stripe:BaseUrl"]!);
        });

        return services;
    }
}

// Program.cs — สะอาดมาก!
var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddDatabase(builder.Configuration)
    .AddRepositories()
    .AddBusinessServices()
    .AddExternalServices(builder.Configuration);

builder.Services.AddControllers();
```

---

## 4. Injection Patterns

```csharp
// ===== 1. Constructor Injection (ใช้บ่อยที่สุด — 95%) =====
public class UserService : IUserService
{
    private readonly IUserRepository _userRepo;
    private readonly IEmailService _emailService;

    public UserService(IUserRepository userRepo, IEmailService emailService)
    {
        _userRepo = userRepo;
        _emailService = emailService;
    }
}

// ===== 2. Method Injection (ใช้บ้าง — [FromServices]) =====
[HttpGet]
public async Task<IActionResult> GetAll([FromServices] IUserService userService)
{
    return Ok(await userService.GetAllAsync());
}

// ===== 3. Primary Constructor (.NET 8+ — สั้นกว่า!) =====
public class UserService(
    IUserRepository userRepo,
    IEmailService emailService,
    ILogger<UserService> logger) : IUserService
{
    public async Task<UserDto> GetByIdAsync(int id)
    {
        logger.LogInformation("Getting user {Id}", id);
        var user = await userRepo.GetByIdAsync(id)
            ?? throw new NotFoundException("User", id);
        return MapToDto(user);
    }
}
```

---

## 5. Scrutor — Auto-Registration

```csharp
// dotnet add package Scrutor
// สแกนและลงทะเบียน services อัตโนมัติ!

builder.Services.Scan(scan => scan
    .FromAssemblyOf<UserService>()

    // ลงทะเบียนทุก class ที่ชื่อลงท้ายด้วย Service
    .AddClasses(classes => classes.Where(t => t.Name.EndsWith("Service")))
    .AsImplementedInterfaces()
    .WithScopedLifetime()

    // ลงทะเบียนทุก class ที่ชื่อลงท้ายด้วย Repository
    .AddClasses(classes => classes.Where(t => t.Name.EndsWith("Repository")))
    .AsImplementedInterfaces()
    .WithScopedLifetime()
);

// แทนที่ต้องเขียน:
// builder.Services.AddScoped<IUserService, UserService>();
// builder.Services.AddScoped<IOrderService, OrderService>();
// builder.Services.AddScoped<IProductService, ProductService>();
// ... อีก 20 บรรทัด

// Scrutor ลงทะเบียนให้ทั้งหมดในบรรทัดเดียว!
```

---

## 6. AutoFac (DI Container ทางเลือก)

```csharp
// dotnet add package Autofac.Extensions.DependencyInjection
// ใช้แทน built-in DI เมื่อต้องการ features เพิ่ม

// Program.cs
builder.Host.UseServiceProviderFactory(new AutofacServiceProviderFactory());
builder.Host.ConfigureContainer<ContainerBuilder>(containerBuilder =>
{
    // Module-based registration
    containerBuilder.RegisterModule<DataModule>();
    containerBuilder.RegisterModule<ServiceModule>();
});

// Modules/ServiceModule.cs
public class ServiceModule : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterAssemblyTypes(typeof(UserService).Assembly)
            .Where(t => t.Name.EndsWith("Service"))
            .AsImplementedInterfaces()
            .InstancePerLifetimeScope();   // = Scoped

        builder.RegisterAssemblyTypes(typeof(UserRepository).Assembly)
            .Where(t => t.Name.EndsWith("Repository"))
            .AsImplementedInterfaces()
            .InstancePerLifetimeScope();
    }
}

// AutoFac features เพิ่มเติม:
// - Property injection
// - Decorator pattern
// - Keyed services
// - Module system
// - Lazy<T> resolution
```

---

## 7. Testing กับ DI — Mock Dependencies

```csharp
// DI ทำให้ test ง่ายมาก — แทนที่ dependency จริงด้วย Mock

// dotnet add package Moq
// dotnet add package xunit

public class UserServiceTests
{
    private readonly Mock<IUserRepository> _mockRepo;
    private readonly Mock<IEmailService> _mockEmail;
    private readonly Mock<ILogger<UserService>> _mockLogger;
    private readonly UserService _service;

    public UserServiceTests()
    {
        _mockRepo = new Mock<IUserRepository>();
        _mockEmail = new Mock<IEmailService>();
        _mockLogger = new Mock<ILogger<UserService>>();

        // สร้าง UserService โดยใช้ Mock แทนของจริง
        _service = new UserService(
            _mockRepo.Object,
            _mockEmail.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task GetByIdAsync_UserExists_ReturnsUser()
    {
        // Arrange — เตรียมข้อมูล
        var fakeUser = new User { Id = 1, FirstName = "John", Email = "john@test.com" };
        _mockRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(fakeUser);

        // Act — เรียก method ที่ต้องการ test
        var result = await _service.GetByIdAsync(1);

        // Assert — ตรวจสอบผลลัพธ์
        Assert.NotNull(result);
        Assert.Equal("John", result.FirstName);
        Assert.Equal("john@test.com", result.Email);
    }

    [Fact]
    public async Task GetByIdAsync_UserNotFound_ThrowsNotFoundException()
    {
        // Arrange
        _mockRepo.Setup(r => r.GetByIdAsync(999)).ReturnsAsync((User?)null);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(
            () => _service.GetByIdAsync(999));
    }

    [Fact]
    public async Task CreateAsync_ValidRequest_SendsWelcomeEmail()
    {
        // Arrange
        var request = new CreateUserRequest("John", "Doe", "john@test.com", "Pass123!");
        _mockRepo.Setup(r => r.CreateAsync(It.IsAny<User>()))
            .ReturnsAsync(new User { Id = 1, FirstName = "John", Email = "john@test.com" });

        // Act
        await _service.CreateAsync(request);

        // Assert — ตรวจว่าส่ง email จริง
        _mockEmail.Verify(
            e => e.SendAsync(
                "john@test.com",
                It.Is<string>(s => s.Contains("Welcome"))),
            Times.Once);
    }
}
```

---

## 8. สรุป

```
DI ใน ASP.NET Core:

1. Interface + Implementation
   IUserService → UserService

2. Register ใน Program.cs
   builder.Services.AddScoped<IUserService, UserService>();

3. Inject ผ่าน Constructor
   public UserController(IUserService userService)

4. Lifetimes:
   - Transient:  สร้างใหม่ทุกครั้ง (stateless)
   - Scoped:     1 ต่อ request (database, services)  ← ใช้บ่อยสุด
   - Singleton:  1 ตลอด app (cache, config)

5. กฎ: Singleton ห้าม inject Scoped!

6. Test: ใช้ Mock แทน implementation จริง

Best Practice:
✅ ทุก service ต้องมี Interface
✅ ใช้ Constructor Injection
✅ จัด registration ด้วย Extension Methods
✅ ใช้ Scrutor สำหรับ auto-registration
✅ เลือก Lifetime ให้ถูกต้อง
```
