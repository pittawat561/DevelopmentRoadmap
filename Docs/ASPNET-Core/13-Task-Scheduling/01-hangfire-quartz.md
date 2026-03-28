# Task Scheduling — Hangfire & Quartz.NET

> Background jobs, recurring tasks, delayed processing ใน .NET

---

## 1. ทำไมต้อง Task Scheduling

```
งานที่ไม่ควรทำใน HTTP request (เพราะช้า/ไม่จำเป็นต้องรอ):
- ส่ง email / SMS
- สร้าง PDF report
- ประมวลผลรูปภาพ
- Sync ข้อมูลกับ external service
- Cleanup ข้อมูลเก่า
- สร้าง daily/weekly report

ทางเลือก:
1. Hangfire      ← ง่ายที่สุด, มี Dashboard, persistent
2. Quartz.NET    ← ยืดหยุ่นสูง, cron expressions
3. Native BackgroundService ← built-in, เบา, ไม่ persistent
```

---

## 2. Hangfire (แนะนำ!)

```bash
dotnet add package Hangfire.Core
dotnet add package Hangfire.SqlServer
dotnet add package Hangfire.AspNetCore
```

```csharp
// Program.cs
builder.Services.AddHangfire(config => config
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UseSqlServerStorage(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddHangfireServer();

var app = builder.Build();
app.UseHangfireDashboard("/hangfire");    // Dashboard UI ที่ /hangfire

// ===== 4 ประเภท Jobs =====

// 1. Fire-and-Forget (ทำทันที ใน background)
BackgroundJob.Enqueue(() => SendWelcomeEmail("john@test.com"));
BackgroundJob.Enqueue<IEmailService>(x => x.SendAsync("john@test.com", "Welcome!"));

// 2. Delayed (ทำหลังจากเวลาที่กำหนด)
BackgroundJob.Schedule(() => SendReminder("john@test.com"), TimeSpan.FromHours(24));
BackgroundJob.Schedule<IReportService>(
    x => x.GenerateMonthlyReport(DateTime.UtcNow.Month),
    TimeSpan.FromMinutes(30));

// 3. Recurring (ทำซ้ำตาม schedule)
RecurringJob.AddOrUpdate<ICleanupService>(
    "cleanup-old-logs",
    x => x.CleanupOldLogsAsync(),
    Cron.Daily(2, 0));                    // ทุกวัน ตี 2

RecurringJob.AddOrUpdate<IReportService>(
    "weekly-report",
    x => x.GenerateWeeklyReportAsync(),
    "0 9 * * 1");                         // ทุกวันจันทร์ 9:00

RecurringJob.AddOrUpdate<ISyncService>(
    "sync-inventory",
    x => x.SyncInventoryAsync(),
    Cron.Hourly);                         // ทุกชั่วโมง

// 4. Continuation (ทำหลัง job อื่นเสร็จ)
var parentJobId = BackgroundJob.Enqueue<IOrderService>(x => x.ProcessOrderAsync(orderId));
BackgroundJob.ContinueJobWith<IEmailService>(parentJobId,
    x => x.SendOrderConfirmationAsync(orderId));
```

### ใช้ใน Service / Controller

```csharp
public class OrderService : IOrderService
{
    private readonly IBackgroundJobClient _backgroundJobs;

    public OrderService(IBackgroundJobClient backgroundJobs)
    {
        _backgroundJobs = backgroundJobs;
    }

    public async Task<OrderDto> CreateOrderAsync(CreateOrderRequest request)
    {
        // สร้าง order ใน DB
        var order = new Order { /* ... */ };
        _context.Orders.Add(order);
        await _context.SaveChangesAsync();

        // Background jobs (ไม่ block response!)
        _backgroundJobs.Enqueue<IEmailService>(
            x => x.SendOrderConfirmationAsync(order.Id));

        _backgroundJobs.Enqueue<IInventoryService>(
            x => x.ReserveStockAsync(order.Id));

        _backgroundJobs.Schedule<INotificationService>(
            x => x.SendOrderReminderAsync(order.Id),
            TimeSpan.FromHours(24));   // เตือนใน 24 ชม. ถ้ายังไม่ชำระ

        return MapToDto(order);
        // Response กลับทันที! jobs ทำงาน background
    }
}
```

---

## 3. Quartz.NET (ยืดหยุ่นสูง)

```bash
dotnet add package Quartz
dotnet add package Quartz.Extensions.Hosting
```

```csharp
// ===== สร้าง Job =====
public class DailyReportJob : IJob
{
    private readonly IReportService _reportService;
    private readonly ILogger<DailyReportJob> _logger;

    public DailyReportJob(IReportService reportService, ILogger<DailyReportJob> logger)
    {
        _reportService = reportService;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        _logger.LogInformation("Starting daily report generation");
        try
        {
            await _reportService.GenerateDailyReportAsync();
            _logger.LogInformation("Daily report completed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Daily report failed");
            throw;  // Quartz จะ retry ตาม config
        }
    }
}

// ===== ลงทะเบียน =====
// Program.cs
builder.Services.AddQuartz(q =>
{
    // Daily Report — ทุกวัน ตี 2
    var dailyReportKey = new JobKey("daily-report");
    q.AddJob<DailyReportJob>(opts => opts.WithIdentity(dailyReportKey));
    q.AddTrigger(opts => opts
        .ForJob(dailyReportKey)
        .WithIdentity("daily-report-trigger")
        .WithCronSchedule("0 0 2 * * ?"));    // ตี 2 ทุกวัน

    // Cleanup — ทุก 6 ชั่วโมง
    var cleanupKey = new JobKey("cleanup");
    q.AddJob<CleanupJob>(opts => opts.WithIdentity(cleanupKey));
    q.AddTrigger(opts => opts
        .ForJob(cleanupKey)
        .WithIdentity("cleanup-trigger")
        .WithSimpleSchedule(s => s.WithIntervalInHours(6).RepeatForever()));
});

builder.Services.AddQuartzHostedService(options =>
{
    options.WaitForJobsToComplete = true;
});
```

---

## 4. Native BackgroundService (Built-in)

```csharp
// ง่ายที่สุด — ไม่ต้องติดตั้ง package เพิ่ม
// แต่ไม่ persistent (หายเมื่อ restart)

public class OrderProcessingService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OrderProcessingService> _logger;

    public OrderProcessingService(IServiceScopeFactory scopeFactory, ILogger<OrderProcessingService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var orderService = scope.ServiceProvider.GetRequiredService<IOrderService>();

                await orderService.ProcessPendingOrdersAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing orders");
            }

            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }
}

// Program.cs
builder.Services.AddHostedService<OrderProcessingService>();
```

---

## 5. เปรียบเทียบ

```
| Feature              | Hangfire         | Quartz.NET       | BackgroundService |
|----------------------|------------------|------------------|-------------------|
| Dashboard UI         | ✅ built-in      | ❌               | ❌                |
| Persistent jobs      | ✅ (SQL/Redis)   | ✅ (SQL)         | ❌                |
| Cron schedule        | ✅               | ✅ (ดีกว่า)      | ❌ (manual)       |
| Fire-and-forget      | ✅               | ❌ (ต้อง config) | ❌                |
| Delayed jobs         | ✅               | ✅               | ❌                |
| Retry on failure     | ✅ auto          | ✅ configurable  | Manual            |
| Setup complexity     | ง่าย             | ปานกลาง          | ง่ายมาก           |
| NuGet packages       | ต้องติดตั้ง       | ต้องติดตั้ง       | built-in          |

เลือก:
- งานง่ายๆ, ไม่ต้อง persistent → BackgroundService
- Enterprise, ต้อง dashboard, persistent → Hangfire ✅
- Complex scheduling, Quartz ecosystem → Quartz.NET
```
