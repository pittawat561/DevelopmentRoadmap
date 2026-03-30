using Banking.Application.Services;
using System.Net;
using System.Security.Claims;
using System.Text.Json;

namespace Banking.Api.Middleware;

/// <summary>
/// Rate Limit Middleware — จำกัด request ต่อ user ต่อ endpoint
///
/// Flow:
///   1. ดึง userId จาก JWT (ถ้าไม่มี → ใช้ IP address)
///   2. สร้าง key: "ratelimit:{userId}:{endpoint}"
///   3. เช็ค Redis: ยังไม่เกิน limit ไหม?
///   4. ถ้าเกิน → 429 Too Many Requests
///   5. ถ้าไม่เกิน → ส่ง request ต่อ
///
/// ตำแหน่งใน Pipeline:
///   ExceptionMiddleware → RateLimitMiddleware → Authentication → Authorization → Controller
///   ต้องอยู่หลัง Authentication เพื่อให้ดึง userId จาก JWT ได้
/// </summary>
public class RateLimitMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IConfiguration _config;

    public RateLimitMiddleware(RequestDelegate next, IConfiguration config)
    {
        _next = next;
        _config = config;
    }

    public async Task InvokeAsync(HttpContext context, IRedisCacheService cache)
    {
        // ข้าม rate limit สำหรับ Swagger, health check
        var path = context.Request.Path.Value?.ToLower() ?? "";
        if (path.Contains("swagger") || path.Contains("health"))
        {
            await _next(context);
            return;
        }

        // ดึง identifier: userId (ถ้า login แล้ว) หรือ IP address
        var userId = context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var identifier = userId ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        // สร้าง key จาก identifier + endpoint
        var endpoint = context.Request.Path.Value?.Replace("/", "-") ?? "unknown";
        var rateLimitKey = $"{identifier}:{endpoint}";

        var maxRequests = _config.GetValue<int>("Redis:RateLimitMaxRequests", 10);
        var windowSeconds = _config.GetValue<int>("Redis:RateLimitWindowSeconds", 60);

        var allowed = await cache.CheckRateLimitAsync(
            rateLimitKey, maxRequests, TimeSpan.FromSeconds(windowSeconds));

        if (!allowed)
        {
            context.Response.StatusCode = (int)HttpStatusCode.TooManyRequests;
            context.Response.ContentType = "application/json";

            var response = new
            {
                success = false,
                message = $"Rate limit exceeded. Maximum {maxRequests} requests per {windowSeconds} seconds.",
                statusCode = 429
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