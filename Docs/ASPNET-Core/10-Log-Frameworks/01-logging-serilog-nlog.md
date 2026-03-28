# Logging ใน ASP.NET Core — Serilog & NLog

> ระบบ log ที่ดี = debug ปัญหาใน production ได้เร็ว

---

## 1. Built-in Logging

```csharp
// ASP.NET Core มี ILogger built-in

public class UserService : IUserService
{
    private readonly ILogger<UserService> _logger;

    public UserService(ILogger<UserService> logger)
    {
        _logger = logger;
    }

    public async Task<UserDto> GetByIdAsync(int id)
    {
        _logger.LogInformation("Getting user {UserId}", id);

        try
        {
            var user = await _context.Users.FindAsync(id);
            if (user is null)
            {
                _logger.LogWarning("User {UserId} not found", id);
                throw new NotFoundException("User", id);
            }
            return MapToDto(user);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user {UserId}", id);
            throw;
        }
    }
}

// Log Levels (เรียงจาก verbose → critical):
// Trace       → รายละเอียดมากสุด (ใช้ debug เท่านั้น)
// Debug       → ข้อมูลสำหรับ developer
// Information → flow ปกติ (user created, order placed)
// Warning     → มีปัญหาแต่ไม่ fail (not found, slow query)
// Error       → error ที่ต้องแก้ (exception, DB connection fail)
// Critical    → app กำลังจะพัง (out of memory, data corruption)
```

---

## 2. Serilog (แนะนำ! — ใช้มากที่สุด)

```bash
dotnet add package Serilog.AspNetCore
dotnet add package Serilog.Sinks.Console
dotnet add package Serilog.Sinks.File
dotnet add package Serilog.Sinks.Seq            # สำหรับ Seq log server
dotnet add package Serilog.Enrichers.Environment
```

```csharp
// Program.cs
using Serilog;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithEnvironmentName()
    .Enrich.WithMachineName()
    .WriteTo.Console(outputTemplate:
        "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties}{NewLine}{Exception}")
    .WriteTo.File(
        path: "logs/app-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj} {Properties}{NewLine}{Exception}")
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog();

var app = builder.Build();

// Request logging middleware (แทน built-in)
app.UseSerilogRequestLogging(options =>
{
    options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000}ms";
});
```

### Structured Logging (จุดเด่นของ Serilog)

```csharp
// ❌ ไม่ดี — string concatenation (ค้นหายาก)
_logger.LogInformation("User " + user.Name + " created order " + order.Id);

// ✅ ดี — Structured logging (ค้นหาง่าย, index ได้)
_logger.LogInformation("User {UserName} created order {OrderId} for {Total:C}",
    user.Name, order.Id, order.Total);

// → log output: User John created order 42 for $500.00
// → properties: { UserName: "John", OrderId: 42, Total: 500.00 }
// → ค้นหาได้: "OrderId = 42", "UserName = John"

// Log with context (เพิ่ม properties ตลอด scope)
using (_logger.BeginScope(new Dictionary<string, object>
{
    ["UserId"] = userId,
    ["CorrelationId"] = correlationId
}))
{
    _logger.LogInformation("Processing order");
    _logger.LogInformation("Sending email");
    // ทั้ง 2 log จะมี UserId, CorrelationId
}
```

### Log to Seq / Elasticsearch

```csharp
// Seq (log server สำหรับ .NET — แนะนำ!)
.WriteTo.Seq("http://localhost:5341")

// Elasticsearch
// dotnet add package Serilog.Sinks.Elasticsearch
.WriteTo.Elasticsearch(new ElasticsearchSinkOptions(new Uri("http://localhost:9200"))
{
    IndexFormat = "myapp-logs-{0:yyyy.MM.dd}",
    AutoRegisterTemplate = true
})
```

---

## 3. NLog (ทางเลือก)

```bash
dotnet add package NLog.Web.AspNetCore
```

```xml
<!-- nlog.config -->
<?xml version="1.0" encoding="utf-8" ?>
<nlog xmlns="http://www.nlog-project.org/schemas/NLog.xsd">
  <targets>
    <target name="console" xsi:type="Console"
            layout="${date:format=HH\:mm\:ss} [${level:uppercase=true}] ${message} ${exception:format=tostring}" />

    <target name="file" xsi:type="File"
            fileName="logs/app-${shortdate}.log"
            layout="${longdate} [${level:uppercase=true}] ${logger} ${message} ${exception:format=tostring}"
            archiveEvery="Day"
            maxArchiveFiles="30" />
  </targets>

  <rules>
    <logger name="Microsoft.*" maxlevel="Warn" writeTo="file" />
    <logger name="*" minlevel="Info" writeTo="console,file" />
  </rules>
</nlog>
```

---

## 4. Serilog vs NLog

```
| Feature              | Serilog            | NLog               |
|----------------------|--------------------|--------------------|
| Structured Logging   | ✅ native, ดีมาก   | ✅ ได้ แต่ซับซ้อนกว่า |
| Performance          | เร็ว               | เร็ว                |
| Config               | C# code            | XML หรือ C#         |
| Sinks/Targets        | 200+ sinks         | 100+ targets        |
| .NET Community       | ⭐ นิยมมากกว่า      | นิยมมาก             |
| Learning Curve       | ง่าย               | ง่าย                |

แนะนำ: Serilog ← ใช้มากที่สุดใน .NET community
```

---

## 5. Best Practices

```csharp
// 1. ใช้ Structured Logging เสมอ
_logger.LogInformation("Order {OrderId} placed by {UserId}", orderId, userId);

// 2. Log level ที่ถูกต้อง
_logger.LogDebug(...)        // dev only
_logger.LogInformation(...)  // flow ปกติ
_logger.LogWarning(...)      // ปัญหาที่ไม่ fail
_logger.LogError(ex, ...)    // exception + ส่ง exception object ด้วย!

// 3. อย่า log sensitive data
// ❌ _logger.LogInformation("Login with password {Password}", password);
// ✅ _logger.LogInformation("User {Email} logged in", email);

// 4. ใช้ Correlation ID ติดตาม request ข้าม services
app.Use(async (context, next) =>
{
    var correlationId = context.Request.Headers["X-Correlation-Id"].FirstOrDefault()
        ?? Guid.NewGuid().ToString();
    context.Response.Headers["X-Correlation-Id"] = correlationId;

    using (LogContext.PushProperty("CorrelationId", correlationId))
    {
        await next();
    }
});
```
