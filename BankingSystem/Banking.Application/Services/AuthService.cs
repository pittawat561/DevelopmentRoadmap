using Banking.Application.DTOs;
using Banking.Domain.Entities;
using Banking.Domain.Enums;
using Banking.Domain.Exceptions;
using Banking.Domain.Interfaces;
using System.Net;
using System.Security.Principal;

namespace Banking.Application.Services;

public class AuthService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IJwtService _jwtService;

    public AuthService(IUnitOfWork unitOfWork, IJwtService jwtService)
    {
        _unitOfWork = unitOfWork;
        _jwtService = jwtService;
    }

    /// <summary>
    /// สมัครสมาชิก + สร้างบัญชีออมทรัพย์เริ่มต้น
    ///
    /// Flow:
    /// 1. Validate: email/phone ไม่ซ้ำ, password match
    /// 2. Hash password ด้วย BCrypt
    /// 3. สร้าง User (KycStatus = Pending)
    /// 4. สร้าง default Savings Account
    /// 5. Generate JWT + Refresh Token
    /// </summary>
    public async Task<AuthResponse> RegisterAsync(
        RegisterRequest request, CancellationToken ct = default)
    {
        if (request.Password != request.ConfirmPassword)
            throw new ArgumentException("Passwords do not match.");

        if (await _unitOfWork.Users.EmailExistsAsync(request.Email, ct))
            throw new DuplicateException("Email already registered.");

        if (await _unitOfWork.Users.PhoneExistsAsync(request.Phone, ct))
            throw new DuplicateException("Phone number already registered.");

        var passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);

        var user = new User
        {
            FirstName = request.FirstName,
            LastName = request.LastName,
            Email = request.Email.ToLower().Trim(),
            Phone = request.Phone.Trim(),
            PasswordHash = passwordHash,
            KycStatus = KycStatus.Pending,
            IsActive = true
        };

        await _unitOfWork.Users.AddAsync(user, ct);

        // สร้าง default Savings Account
        string accountNumber;
        do
        {
            accountNumber = AccountNumberGenerator.Generate();
        } while (await _unitOfWork.Accounts.AccountNumberExistsAsync(accountNumber, ct));

        var account = new Account
        {
            UserId = user.Id,
            AccountNumber = accountNumber,
            Type = AccountType.Savings,
            Currency = "THB",
            Status = AccountStatus.Active
        };

        await _unitOfWork.Accounts.AddAsync(account, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        var accessToken = _jwtService.GenerateAccessToken(user);
        var refreshToken = _jwtService.GenerateRefreshToken();

        // TODO: เก็บ refreshToken ใน Redis (Phase 3)

        return new AuthResponse(
            UserId: user.Id,
            FullName: user.FullName,
            Email: user.Email,
            AccessToken: accessToken,
            RefreshToken: refreshToken,
            AccessTokenExpiry: DateTime.UtcNow.AddMinutes(15)
        );
    }

    /// <summary>
    /// Login ด้วย email + password
    ///
    /// Flow:
    /// 1. Find user by email
    /// 2. Check: ถูก lock ไหม?
    /// 3. Verify password (BCrypt)
    /// 4. ถ้าผิด → เพิ่ม FailedLoginAttempts (ถ้าครบ 5 → lock)
    /// 5. ถ้าถูก → reset counter + generate tokens
    ///
    /// ⚠️ Security: ข้อความ error ต้องกว้างๆ
    ///   ✅ "Invalid email or password." — ไม่บอกว่าอีเมลมีอยู่ไหม
    ///   ❌ "Email not found." → attacker รู้ว่าอีเมลไม่มี
    /// </summary>
    public async Task<AuthResponse> LoginAsync(
        LoginRequest request, CancellationToken ct = default)
    {
        var user = await _unitOfWork.Users.GetByEmailAsync(
            request.Email.ToLower().Trim(), ct);

        if (user is null)
            throw new ArgumentException("Invalid email or password.");

        if (user.IsLocked)
            throw new AccountLockedException();

        if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            user.FailedLoginAttempts++;
            if (user.FailedLoginAttempts >= 5)
                user.IsLocked = true;

            _unitOfWork.Users.Update(user);
            await _unitOfWork.SaveChangesAsync(ct);

            throw new ArgumentException("Invalid email or password.");
        }

        // Login สำเร็จ → reset counter
        user.FailedLoginAttempts = 0;
        user.LastLoginAt = DateTime.UtcNow;
        _unitOfWork.Users.Update(user);
        await _unitOfWork.SaveChangesAsync(ct);

        var accessToken = _jwtService.GenerateAccessToken(user);
        var refreshToken = _jwtService.GenerateRefreshToken();

        return new AuthResponse(
            UserId: user.Id,
            FullName: user.FullName,
            Email: user.Email,
            AccessToken: accessToken,
            RefreshToken: refreshToken,
            AccessTokenExpiry: DateTime.UtcNow.AddMinutes(15)
        );
    }

    public async Task<UserProfileResponse> GetProfileAsync(
        Guid userId, CancellationToken ct = default)
    {
        var user = await _unitOfWork.Users.GetByIdAsync(userId, ct)
            ?? throw new NotFoundException("User", userId);

        return new UserProfileResponse(
            Id: user.Id,
            FirstName: user.FirstName,
            LastName: user.LastName,
            Email: user.Email,
            Phone: user.Phone,
            KycStatus: user.KycStatus.ToString(),
            CreatedAt: user.CreatedAt
        );
    }
}