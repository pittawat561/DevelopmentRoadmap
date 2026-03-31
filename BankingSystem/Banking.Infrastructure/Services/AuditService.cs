using Banking.Application.Services;
using Banking.Domain.Entities;
using Banking.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;

namespace Banking.Infrastructure.Services;

/// <summary>
/// Audit Service — เขียน AuditLog ลง database โดยตรง
///
/// ทำไมไม่ใช้ UnitOfWork:
///   Audit log ต้องเขียนแยกจาก business transaction
///   ถ้า transaction rollback → audit log ไม่ควร rollback ด้วย
///   ต้องบันทึกแม้ว่า action จะ fail (เพื่อ security tracking)
///
/// ใช้ DbContext ตัวใหม่ (ไม่ใช่ตัวเดียวกับ UnitOfWork)
/// → เขียน audit log ได้แม้ main transaction rollback
/// </summary>
public class AuditService : IAuditService
{
    private readonly IServiceScopeFactory _scopeFactory;

    public AuditService(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public async Task LogAsync(
        Guid? userId,
        string action,
        string entityType,
        string? entityId = null,
        object? oldValues = null,
        object? newValues = null,
        string? ipAddress = null,
        string? userAgent = null,
        CancellationToken ct = default)
    {
        // สร้าง scope ใหม่ → DbContext ใหม่ → แยกจาก main transaction
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var auditLog = new AuditLog
        {
            UserId = userId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            OldValues = oldValues is not null
                ? JsonSerializer.Serialize(oldValues) : null,
            NewValues = newValues is not null
                ? JsonSerializer.Serialize(newValues) : null,
            IpAddress = ipAddress,
            UserAgent = userAgent
        };

        context.Set<AuditLog>().Add(auditLog);
        await context.SaveChangesAsync(ct);
    }
}
