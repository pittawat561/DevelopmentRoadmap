using Banking.Infrastructure.Hubs;
using Banking.Api.Middleware;
using Banking.Application.Services;
using Banking.Domain.Interfaces;
using Banking.Infrastructure.Data;
using Banking.Infrastructure.Repositories;
using Banking.Infrastructure.Services;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using StackExchange.Redis;
using System.Text;
using Prometheus;

var builder = WebApplication.CreateBuilder(args);

// ===== Database (Write → Primary) =====
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        npgsqlOptions =>
        {
            npgsqlOptions.MigrationsAssembly("Banking.Infrastructure");
            npgsqlOptions.EnableRetryOnFailure(3);
            npgsqlOptions.CommandTimeout(30);
        }));

// ===== Database (Read → Replica) =====
builder.Services.AddDbContext<ReadOnlyDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("ReadConnection")
            ?? builder.Configuration.GetConnectionString("DefaultConnection"))
    .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking));

// ===== Redis =====
builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    var connectionString = builder.Configuration.GetConnectionString("Redis")
        ?? "localhost:6379,abortConnect=false";
    return ConnectionMultiplexer.Connect(connectionString);
});
builder.Services.AddSingleton<IRedisCacheService, RedisCacheService>();

// ===== JWT Authentication =====
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
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!)),
            ClockSkew = TimeSpan.Zero
        };

        // SignalR: ส่ง JWT ผ่าน query string (WebSocket ไม่มี header)
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;

                if (!string.IsNullOrEmpty(accessToken)
                    && path.StartsWithSegments("/hubs"))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            }
        };
    });

// ===== Repositories + UnitOfWork =====
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

// ===== Application Services =====
builder.Services.AddScoped<TransactionService>();
builder.Services.AddScoped<IJwtService, JwtService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<PinService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<IFraudDetectionService, FraudDetectionService>();

// ===== FluentValidation =====
builder.Services.AddValidatorsFromAssemblyContaining<Banking.Application.Validators.DepositRequestValidator>();

// ===== SignalR =====
builder.Services.AddSignalR()
    .AddStackExchangeRedis(builder.Configuration.GetConnectionString("Redis")
        ?? "localhost:6379", options =>
        {
            options.Configuration.ChannelPrefix = RedisChannel.Literal("banking-signalr:");
        });

// ===== ASP.NET Core =====
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ===== Forwarded Headers (สำหรับ Nginx reverse proxy) =====
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor
        | Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

// ===== CORS (สำหรับ Next.js frontend) =====
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(
                builder.Configuration["Frontend:Url"] ?? "http://localhost:3000")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

var app = builder.Build();

// ===== Auto Migration & Seed =====
// Production: migrate แต่ไม่ seed demo data
// Development/Debug: migrate + seed demo data
{
    using var scope = app.Services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await context.Database.MigrateAsync();

    var env = app.Environment;
    if (env.EnvironmentName == "Debug" || env.IsDevelopment())
    {
        await Banking.Infrastructure.Seeds.DataSeeder.SeedAsync(context);
    }
}

// ===== Swagger =====
var swaggerEnabled = app.Configuration.GetValue<bool>("Swagger:Enabled");
if (swaggerEnabled)
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// ===== Middleware Pipeline (ลำดับสำคัญมาก!) =====
app.UseMiddleware<ExceptionMiddleware>();           // 1. จับ error ทั้งหมด
app.UseForwardedHeaders();                          // 2. อ่าน X-Forwarded-For จาก Nginx
app.UseHttpsRedirection();                          // 3. HTTP → HTTPS
app.UseCors("AllowFrontend");                       // 3. CORS (ก่อน auth)
app.UseAuthentication();                            // 4. ตรวจ JWT
app.UseMiddleware<TokenBlacklistMiddleware>();      // 5. เช็ค token blacklist
app.UseMiddleware<RateLimitMiddleware>();           // 6. จำกัด request rate
app.UseMiddleware<IdempotencyMiddleware>();         // 7. ป้องกัน duplicate request
app.UseMiddleware<AdminIpWhitelistMiddleware>();    // 8. IP whitelist สำหรับ admin
app.UseAuthorization();                             // 9. ตรวจสิทธิ์ [Authorize]
app.UseMiddleware<AuditMiddleware>();               // 10. บันทึก audit log (หลัง auth)
app.MapControllers();                               // 11. Route → Controller
app.UseHttpMetrics();                               // วัด HTTP request metrics อัตโนมัติ
app.MapMetrics();                                   // Expose /metrics endpoint สำหรับ Prometheus

// ===== SignalR Hub =====
app.MapHub<NotificationHub>("/hubs/notifications");

// ===== Health Check =====
app.MapGet("/health", async (AppDbContext db, IConnectionMultiplexer redis) =>
{
    var checks = new Dictionary<string, string>();

    try
    {
        await db.Database.CanConnectAsync();
        checks["database"] = "healthy";
    }
    catch { checks["database"] = "unhealthy"; }

    try
    {
        var pong = await redis.GetDatabase().PingAsync();
        checks["redis"] = pong.TotalMilliseconds < 100 ? "healthy" : "degraded";
    }
    catch { checks["redis"] = "unhealthy"; }

    var isHealthy = checks.Values.All(v => v == "healthy");
    return Results.Json(new
    {
        status = isHealthy ? "healthy" : "unhealthy",
        checks,
        timestamp = DateTime.UtcNow
    }, statusCode: isHealthy ? 200 : 503);
}).ExcludeFromDescription();

app.Run();

// ทำให้ Program class เข้าถึงได้จาก Integration Tests (WebApplicationFactory)
public partial class Program { }
