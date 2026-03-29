namespace Banking.Application.DTOs;

public record RegisterRequest(
    string FirstName,
    string LastName,
    string Email,
    string Phone,
    string Password,
    string ConfirmPassword
);

public record LoginRequest(
    string Email,
    string Password
);

public record RefreshTokenRequest(
    string RefreshToken
);

public record AuthResponse(
    Guid UserId,
    string FullName,
    string Email,
    string AccessToken,
    string RefreshToken,
    DateTime AccessTokenExpiry
);

public record UserProfileResponse(
    Guid Id,
    string FirstName,
    string LastName,
    string Email,
    string Phone,
    string KycStatus,
    DateTime CreatedAt
);