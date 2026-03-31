using Banking.Application.Services;
using System.Security.Claims;

namespace Banking.Api.Middleware;

/// <summary>
/// Audit Middleware — บันทึก HTTP request/response สำหรับ write operations
///
/// บันทึกเฉพาะ:
///   - POST, PUT, PATCH, DELETE (write operations)
///   - ข้าม GET (read-only ไม่ต้องบันทึก)
///   - ข้าม Swagger, health check
///
/// ข้อมูลที่บันทึก:
///   - UserId (จาก JWT)
///   - Action (HTTP method + path)
///   - IP Address
///   - User Agent (browser/app)
///   - Status Code
/// </summary>
public class AuditMiddleware
{
    private readonly RequestDelegate _next;

    public AuditMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IAuditService auditService)
    {
        // เฉพาะ write operations
        var method = context.Request.Method;
        if (method == "GET" || method == "OPTIONS" || method == "HEAD")
        {
            await _next(context);
            return;
        }

        // ข้าม swagger, health
        var path = context.Request.Path.Value?.ToLower() ?? "";
        if (path.Contains("swagger") || path.Contains("health"))
        {
            await _next(context);
            return;
        }

        await _next(context);

        // บันทึกหลัง request เสร็จ (ได้ status code)
        var userId = context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var parsedUserId = userId is not null ? Guid.Parse(userId) : (Guid?)null;

        try
        {
            await auditService.LogAsync(
                userId: parsedUserId,
                action: $"{method} {context.Request.Path}",
                entityType: "HttpRequest",
                entityId: null,
                oldValues: null,
                newValues: new
                {
                    StatusCode = context.Response.StatusCode,
                    QueryString = context.Request.QueryString.Value
                },
                ipAddress: context.Connection.RemoteIpAddress?.ToString(),
                userAgent: context.Request.Headers.UserAgent.FirstOrDefault()
            );
        }
        catch
        {
            // Audit log failure ไม่ควรทำให้ request fail
            // Log warning แต่ไม่ throw
        }
    }
}