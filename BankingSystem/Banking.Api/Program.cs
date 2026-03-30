using Banking.Api.Hubs;
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

var builder = WebApplication.CreateBuilder(args);

// ===== Database =====
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        npgsqlOptions =>
        {
            npgsqlOptions.MigrationsAssembly("Banking.Infrastructure");
            npgsqlOptions.EnableRetryOnFailure(3);
            npgsqlOptions.CommandTimeout(30);
        }));

// ===== Redis =====
// Singleton: 1 connection ใช้ทั้ง app (thread-safe, multiplexed)
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

        // ===== SignalR: ส่ง JWT ผ่าน query string (WebSocket ไม่มี header) =====
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;

                // ถ้า request เป็น SignalR hub → ดึง token จาก query string
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
builder.Services.AddScoped<INotificationService, NotificationService>();

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

// ===== CORS (สำหรับ Next.js frontend) =====
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(
                builder.Configuration["Frontend:Url"] ?? "http://localhost:3000")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();  // จำเป็นสำหรับ SignalR
    });
});

var app = builder.Build();

// ===== Auto Migration & Seed (Debug/Dev only) =====
var env = app.Environment;
if (env.EnvironmentName == "Debug" || env.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await context.Database.MigrateAsync();
    await Banking.Infrastructure.Seeds.DataSeeder.SeedAsync(context);
}

// ===== Swagger =====
var swaggerEnabled = app.Configuration.GetValue<bool>("Swagger:Enabled");
if (swaggerEnabled)
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// ===== Middleware Pipeline (ลำดับสำคัญมาก!) =====
app.UseMiddleware<ExceptionMiddleware>();       // 1. จับ error ทั้งหมด
app.UseHttpsRedirection();                      // 2. HTTP → HTTPS
app.UseCors("AllowFrontend");                   // 3. CORS (ก่อน auth)
app.UseAuthentication();                        // 4. ตรวจ JWT → "คุณเป็นใคร?"
app.UseMiddleware<TokenBlacklistMiddleware>();   // 5. เช็ค token blacklist
app.UseMiddleware<RateLimitMiddleware>();        // 6. จำกัด request rate
app.UseAuthorization();                         // 7. ตรวจสิทธิ์ [Authorize]
app.MapControllers();                           // 8. Route → Controller

// ===== SignalR Hub =====
app.MapHub<NotificationHub>("/hubs/notifications");

app.Run();