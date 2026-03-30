using Banking.Application.DTOs;
using Banking.Application.Services;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Banking.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AuthService _authService;
    private readonly IValidator<RegisterRequest> _registerValidator;
    private readonly IValidator<LoginRequest> _loginValidator;

    public AuthController(
        AuthService authService,
        IValidator<RegisterRequest> registerValidator,
        IValidator<LoginRequest> loginValidator)
    {
        _authService = authService;
        _registerValidator = registerValidator;
        _loginValidator = loginValidator;
    }

    /// <summary>
    /// สมัครสมาชิก — POST /api/auth/register
    /// </summary>
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

    /// <summary>
    /// เข้าสู่ระบบ — POST /api/auth/login
    /// </summary>
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

    /// <summary>
    /// ดูโปรไฟล์ (ต้อง login) — GET /api/auth/profile
    /// User.FindFirst(ClaimTypes.NameIdentifier) ดึง userId จาก JWT token
    /// </summary>
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
    /// ออกจากระบบ — POST /api/auth/logout
    /// Placeholder สำหรับ Phase 3 (Redis token blacklist)
    /// </summary>
    [HttpPost("logout")]
    [Authorize]
    public IActionResult Logout()
    {
        // TODO Phase 3: เพิ่ม JTI ลง Redis blacklist
        return Ok(new ApiResponse<object>(true, "Logged out successfully."));
    }
}