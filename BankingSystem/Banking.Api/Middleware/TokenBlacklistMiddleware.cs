using Banking.Application.Services;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Text.Json;

namespace Banking.Api.Middleware;

/// <summary>
/// Token Blacklist Middleware — เช็คว่า JWT ถูก revoke ไหม
///
/// Flow:
///   1. ดึง JWT จาก Authorization header
///   2. อ่าน JTI (JWT ID) จาก token
///   3. เช็ค Redis: JTI อยู่ใน blacklist ไหม?
///   4. ถ้าอยู่ → 401 Unauthorized (token ถูก revoke)
///   5. ถ้าไม่อยู่ → ส่ง request ต่อ
///
/// Performance:
///   Redis GET = < 1ms → แทบไม่กระทบ performance
///   เช็คทุก request ไม่หนัก เพราะ Redis เร็วมาก
/// </summary>
public class TokenBlacklistMiddleware
{
    private readonly RequestDelegate _next;

    public TokenBlacklistMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IRedisCacheService cache)
    {
        var authHeader = context.Request.Headers.Authorization.FirstOrDefault();

        if (authHeader is not null && authHeader.StartsWith("Bearer "))
        {
            var token = authHeader["Bearer ".Length..];

            try
            {
                var handler = new JwtSecurityTokenHandler();
                var jwtToken = handler.ReadJwtToken(token);
                var jti = jwtToken.Id;  // JWT ID claim

                if (!string.IsNullOrEmpty(jti) && await cache.IsTokenBlacklistedAsync(jti))
                {
                    context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                    context.Response.ContentType = "application/json";

                    var response = new
                    {
                        success = false,
                        message = "Token has been revoked. Please login again.",
                        statusCode = 401
                    };

                    await context.Response.WriteAsync(
                        JsonSerializer.Serialize(response, new JsonSerializerOptions
                        {
                            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                        }));
                    return;
                }
            }
            catch
            {
                // token อ่านไม่ได้ → ปล่อยผ่าน ให้ JWT middleware จัดการ
            }
        }

        await _next(context);
    }
}