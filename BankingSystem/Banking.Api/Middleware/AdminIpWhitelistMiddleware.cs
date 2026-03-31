using System.Text.Json;

namespace Banking.Api.Middleware;

/// <summary>
/// Admin IP Whitelist — อนุญาตเฉพาะ IP ที่กำหนดสำหรับ /api/admin/*
/// Production: ตั้งค่า Security:AdminAllowedIps ใน appsettings
/// </summary>
public class AdminIpWhitelistMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IConfiguration _config;

    public AdminIpWhitelistMiddleware(RequestDelegate next, IConfiguration config)
    {
        _next = next;
        _config = config;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value?.ToLower() ?? "";

        if (!path.StartsWith("/api/admin"))
        {
            await _next(context);
            return;
        }

        var allowedIps = _config.GetSection("Security:AdminAllowedIps")
            .Get<string[]>() ?? ["127.0.0.1", "::1"];

        var remoteIp = context.Connection.RemoteIpAddress?.ToString();

        if (remoteIp is null || !allowedIps.Contains(remoteIp))
        {
            context.Response.StatusCode = 403;
            context.Response.ContentType = "application/json";

            var response = new
            {
                success = false,
                message = "Access denied. IP not authorized for admin endpoints.",
                statusCode = 403
            };

            await context.Response.WriteAsync(
                JsonSerializer.Serialize(response, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                }));
            return;
        }

        await _next(context);
    }
}
