using Banking.Infrastructure.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Banking.Tests.Integration;

/// <summary>
/// Custom WebApplicationFactory — สร้าง test server ที่ใช้ test database
/// </summary>
public class BankingApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            // ลบ DbContext registration เดิม
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            if (descriptor is not null) services.Remove(descriptor);

            // ใช้ test database
            services.AddDbContext<AppDbContext>(options =>
                options.UseNpgsql(
                    "Host=localhost;Database=banking_test;Username=postgres;Password=root1234"));
        });
    }
}
