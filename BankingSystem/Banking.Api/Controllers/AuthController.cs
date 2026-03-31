using Banking.Application.DTOs;
using Banking.Application.Services;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Banking.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AuthService _authService;
    private readonly IValidator<RegisterRequest> _registerValidator;
    private readonly IValidator<LoginRequest> _loginValidator;
    private readonly IRedisCacheService _cache;
    private readonly PinService _pinService;

    public AuthController(
        AuthService authService,
        IValidator<RegisterRequest> registerValidator,
        IValidator<LoginRequest> loginValidator,
        IRedisCacheService cache,
        PinService pinService)
    {
        _authService = authService;
        _registerValidator = registerValidator;
        _loginValidator = loginValidator;
        _cache = cache;
        _pinService = pinService;
    }

    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register(
        [FromBody] RegisterRequest request, CancellationToken ct)
    {
        var validation = await _registerValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return BadRequest(new ApiResponse<object>(false,
                string.Join("; ", validation.Errors.Select(e => e.ErrorMessage))));

        var result = await _authService.RegisterAsync(request, ct);
        return Created($"/api/auth/profile",
            new ApiResponse<AuthResponse>(true, "Registration successful.", result));
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login(
        [FromBody] LoginRequest request, CancellationToken ct)
    {
        var validation = await _loginValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return BadRequest(new ApiResponse<object>(false,
                string.Join("; ", validation.Errors.Select(e => e.ErrorMessage))));

        var result = await _authService.LoginAsync(request, ct);
        return Ok(new ApiResponse<AuthResponse>(true, "Login successful.", result));
    }

    [HttpGet("profile")]
    [Authorize]
    public async Task<IActionResult> Profile(CancellationToken ct)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userIdClaim is null || !Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized(new ApiResponse<object>(false, "Invalid token."));

        var result = await _authService.GetProfileAsync(userId, ct);
        return Ok(new ApiResponse<UserProfileResponse>(true, "Profile retrieved.", result));
    }

    /// <summary>
    /// Logout — Blacklist token ใน Redis
    ///
    /// Flow:
    /// 1. ดึง JWT จาก Authorization header
    /// 2. อ่าน JTI + expiration จาก token
    /// 3. เก็บ JTI ลง Redis พร้อม TTL = เวลาที่เหลือก่อน token หมดอายุ
    /// 4. ทุก request หลังจากนี้จะถูก reject โดย TokenBlacklistMiddleware
    /// </summary>
    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        var authHeader = Request.Headers.Authorization.FirstOrDefault();
        if (authHeader is not null && authHeader.StartsWith("Bearer "))
        {
            var token = authHeader["Bearer ".Length..];
            var handler = new JwtSecurityTokenHandler();
            var jwtToken = handler.ReadJwtToken(token);

            var jti = jwtToken.Id;
            var expiry = jwtToken.ValidTo;
            var ttl = expiry - DateTime.UtcNow;

            if (!string.IsNullOrEmpty(jti) && ttl > TimeSpan.Zero)
            {
                await _cache.BlacklistTokenAsync(jti, ttl);
            }
        }

        return Ok(new ApiResponse<object>(true, "Logged out successfully."));
    }
    /// <summary>
    /// ตั้ง PIN ครั้งแรก — POST /api/auth/pin/set
    /// </summary>
    [HttpPost("pin/set")]
    [Authorize]
    public async Task<IActionResult> SetPin(
        [FromBody] SetPinRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        await _pinService.SetPinAsync(userId.Value, request, ct);
        return Ok(new ApiResponse<object>(true, "PIN set successfully."));
    }

    /// <summary>
    /// เปลี่ยน PIN — POST /api/auth/pin/change
    /// </summary>
    [HttpPost("pin/change")]
    [Authorize]
    public async Task<IActionResult> ChangePin(
        [FromBody] ChangePinRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        await _pinService.ChangePinAsync(userId.Value, request, ct);
        return Ok(new ApiResponse<object>(true, "PIN changed successfully."));
    }

    // Helper: ดึง userId จาก JWT
    private Guid? GetUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return claim is not null ? Guid.Parse(claim) : null;
    }
}