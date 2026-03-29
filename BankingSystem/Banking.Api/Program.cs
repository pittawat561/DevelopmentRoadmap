using Banking.Application.Services;
using Banking.Application.Services;
using Banking.Domain.Interfaces;
using Banking.Infrastructure.Data;
using Banking.Infrastructure.Repositories;
using Banking.Infrastructure.Services;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
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
    });

// ===== Repositories + UnitOfWork =====
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

// ===== Application Services =====
builder.Services.AddScoped<TransactionService>();
builder.Services.AddScoped<IJwtService, JwtService>();
builder.Services.AddScoped<AuthService>();

// ===== FluentValidation =====
builder.Services.AddValidatorsFromAssemblyContaining<Banking.Application.Validators.DepositRequestValidator>();

// ===== ASP.NET Core =====
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

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

// ===== Middleware Pipeline (ลำดับสำคัญ!) =====
app.UseMiddleware<Banking.Api.Middleware.ExceptionMiddleware>();
app.UseHttpsRedirection();
app.UseAuthentication();  // ตรวจ JWT token
app.UseAuthorization();   // ตรวจสิทธิ์ [Authorize]
app.MapControllers();

app.Run();