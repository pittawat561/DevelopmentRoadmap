using Banking.Application.Services;
using System.Text.Json;

namespace Banking.Api.Middleware;

/// <summary>
/// Idempotency Middleware — ป้องกัน duplicate requests
///
/// ทำงานเฉพาะ POST/PUT/PATCH (write operations)
/// ไม่ทำงานกับ GET/DELETE
///
/// Flow:
///   1. อ่าน header X-Idempotency-Key
///   2. ถ้าไม่มี header → ปล่อยผ่าน (backwards compatible)
///   3. ถ้ามี → เช็ค Redis ว่า key นี้มี response แล้วไหม
///   4. ถ้ามี → return response เดิม (ไม่ process ซ้ำ)
///   5. ถ้าไม่มี → process request → เก็บ response ใน Redis
///
/// TTL: 24 ชั่วโมง — หลังจากนั้น key หมดอายุ (ส่ง request ซ้ำได้)
/// </summary>
public class IdempotencyMiddleware
{
    private readonly RequestDelegate _next;
    private static readonly TimeSpan IdempotencyTtl = TimeSpan.FromHours(24);

    public IdempotencyMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IRedisCacheService cache)
    {
        // เฉพาะ write operations
        var method = context.Request.Method;
        if (method != "POST" && method != "PUT" && method != "PATCH")
        {
            await _next(context);
            return;
        }

        // อ่าน idempotency key จาก header
        var idempotencyKey = context.Request.Headers["X-Idempotency-Key"].FirstOrDefault();
        if (string.IsNullOrEmpty(idempotencyKey))
        {
            await _next(context);
            return;
        }

        var cacheKey = $"idempotency:{idempotencyKey}";

        // เช็ค Redis: เคย process request นี้แล้วไหม?
        var cachedResponse = await cache.GetAsync<IdempotencyResponse>(cacheKey);
        if (cachedResponse is not null)
        {
            // Return response เดิม — ไม่ process ซ้ำ
            context.Response.StatusCode = cachedResponse.StatusCode;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(cachedResponse.Body);
            return;
        }

        // ดักจับ response เพื่อเก็บ cache
        var originalBodyStream = context.Response.Body;
        using var responseBody = new MemoryStream();
        context.Response.Body = responseBody;

        await _next(context);

        // อ่าน response ที่ได้
        responseBody.Seek(0, SeekOrigin.Begin);
        var body = await new StreamReader(responseBody).ReadToEndAsync();

        // เก็บ response ใน Redis (เฉพาะ 2xx)
        if (context.Response.StatusCode >= 200 && context.Response.StatusCode < 300)
        {
            var idempotencyResponse = new IdempotencyResponse(
                context.Response.StatusCode, body);

            await cache.SetAsync(cacheKey, idempotencyResponse, IdempotencyTtl);
        }

        // เขียน response กลับให้ client
        responseBody.Seek(0, SeekOrigin.Begin);
        await responseBody.CopyToAsync(originalBodyStream);
    }

    private record IdempotencyResponse(int StatusCode, string Body);
}