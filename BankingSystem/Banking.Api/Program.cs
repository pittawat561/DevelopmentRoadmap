using Banking.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

// ===== สร้าง WebApplication Builder =====
// WebApplication.CreateBuilder(args) — สร้าง builder สำหรับตั้งค่า Web Application
// args — command line arguments ที่ส่งมาตอนรัน (เช่น --urls, --environment)
// builder มีหน้าที่:
//   1. ลงทะเบียน Services (Dependency Injection)
//   2. อ่าน Configuration (appsettings.json + appsettings.{Environment}.json)
//   3. ตั้งค่า Logging
//
// ASP.NET Core จะโหลด config ตามลำดับ:
//   appsettings.json → appsettings.{ASPNETCORE_ENVIRONMENT}.json → Environment Variables → Command Line
//   config ที่โหลดทีหลังจะ override ค่าก่อนหน้า
var builder = WebApplication.CreateBuilder(args);

// ===== Database: ลงทะเบียน AppDbContext ใน DI Container =====
// builder.Services.AddDbContext<AppDbContext>() — บอก DI Container ว่า
// เมื่อมีคนขอ AppDbContext ให้สร้างและ inject ให้อัตโนมัติ
//
// options.UseNpgsql() — กำหนดให้ใช้ PostgreSQL เป็น database provider
//   parameter แรก: connection string จาก appsettings.{Environment}.json
//   builder.Configuration.GetConnectionString("DefaultConnection")
//     — อ่านค่าจาก section "ConnectionStrings" > "DefaultConnection"
//     แต่ละ environment จะใช้ connection string ต่างกัน:
//       Debug       → banking_debug (localhost)
//       Development → banking_dev (localhost)
//       UAT         → banking_uat (uat-db-server)
//       Production  → banking_prod (prod-db-server + SSL)
//
// npgsqlOptions.MigrationsAssembly("Banking.Infrastructure")
//   — บอกว่าไฟล์ Migration อยู่ใน project Banking.Infrastructure
//
// npgsqlOptions.EnableRetryOnFailure(3)
//   — ถ้าเชื่อมต่อ database ล้มเหลว ให้ลองใหม่สูงสุด 3 ครั้ง
//
// npgsqlOptions.CommandTimeout(30)
//   — ตั้ง timeout 30 วินาทีสำหรับทุก SQL command
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        npgsqlOptions =>
        {
            npgsqlOptions.MigrationsAssembly("Banking.Infrastructure");
            npgsqlOptions.EnableRetryOnFailure(3);
            npgsqlOptions.CommandTimeout(30);
        }));

// ===== ลงทะเบียน Services สำหรับ API =====

// builder.Services.AddControllers()
//   — ลงทะเบียน MVC Controller services (เช่น WeatherForecastController)
//   ให้ framework รู้จัก Controller ทั้งหมดและจัดการ routing
builder.Services.AddControllers();

// builder.Services.AddEndpointsApiExplorer()
//   — เปิดใช้ API endpoint discovery สำหรับ Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();

// builder.Services.AddSwaggerGen()
//   — สร้าง Swagger UI documentation อัตโนมัติจาก API endpoints
builder.Services.AddSwaggerGen();

// ===== สร้าง Application จาก builder =====
// builder.Build() — สร้าง WebApplication instance จาก configuration ทั้งหมดที่ตั้งไว้
// หลังจากนี้จะเพิ่ม Service ไม่ได้อีก — ต้องตั้งค่า Middleware แทน
var app = builder.Build();

// ===== Helper: ตรวจสอบ Environment =====
// app.Environment.EnvironmentName — ชื่อ environment ปัจจุบัน (จาก ASPNETCORE_ENVIRONMENT)
var env = app.Environment;
var isDebug = env.EnvironmentName == "Debug";
var isDev = env.IsDevelopment();       // ตรวจสอบว่าเป็น "Development"
var isUat = env.EnvironmentName == "UAT";
var isProd = env.IsProduction();        // ตรวจสอบว่าเป็น "Production"

// ===== Auto Migration & Seed Data (เฉพาะ Debug และ Development) =====
// ใน Debug/Dev → สร้าง database + ตาราง + เติมข้อมูลตัวอย่างอัตโนมัติ
// ใน UAT/Production → ต้องรัน Migration แยกผ่าน CI/CD pipeline
if (isDebug || isDev)
{
    // app.Services.CreateScope() — สร้าง DI Scope เพื่อขอ Scoped Service (AppDbContext)
    //   ใช้ "using var" เพื่อให้ scope ถูก dispose อัตโนมัติเมื่อจบ block
    using var scope = app.Services.CreateScope();

    // GetRequiredService<AppDbContext>() — ขอ AppDbContext จาก DI Container
    //   throw exception ถ้าไม่พบ (ต่างจาก GetService ที่คืน null)
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    // MigrateAsync() — รัน pending migration ทั้งหมดเพื่อสร้าง/อัปเดตตาราง
    //   ถ้า database ยังไม่มี → สร้างใหม่ทั้ง database
    await context.Database.MigrateAsync();

    // SeedAsync() — เติมข้อมูลเริ่มต้น (Admin, Demo user, Accounts, Transactions)
    //   จะข้ามถ้ามีข้อมูลอยู่แล้ว (context.Users.Any())
    await Banking.Infrastructure.Seeds.DataSeeder.SeedAsync(context);
}

// ===== Swagger UI =====
// อ่านค่า Swagger:Enabled จาก appsettings.{Environment}.json
//   Debug       → true (เปิด Swagger)
//   Development → true (เปิด Swagger)
//   UAT         → true (เปิดให้ทีม QA ทดสอบ)
//   Production  → false (ปิด Swagger เพื่อความปลอดภัย)
//
// app.UseSwagger() — เปิด endpoint /swagger/v1/swagger.json (API spec)
// app.UseSwaggerUI() — เปิดหน้า Swagger UI ที่ /swagger
var swaggerEnabled = app.Configuration.GetValue<bool>("Swagger:Enabled");
if (swaggerEnabled)
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// ===== Middleware Pipeline =====

// app.UseHttpsRedirection()
//   — redirect HTTP → HTTPS อัตโนมัติ เพื่อความปลอดภัย
app.UseHttpsRedirection();

// app.UseAuthorization()
//   — ตรวจสอบสิทธิ์การเข้าถึง (ทำงานร่วมกับ [Authorize] attribute)
app.UseAuthorization();

// app.MapControllers()
//   — สแกน Controller ทั้งหมดและสร้าง route mapping อัตโนมัติ
app.MapControllers();

// app.Run()
//   — เริ่มรัน Web Server รอรับ HTTP request จนกว่าจะปิด app
app.Run();
