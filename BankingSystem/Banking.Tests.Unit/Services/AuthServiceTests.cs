using Banking.Application.DTOs;
using Banking.Application.Services;
using Banking.Domain.Entities;
using Banking.Domain.Exceptions;
using Banking.Domain.Interfaces;
using FluentAssertions;
using Moq;

namespace Banking.Tests.Unit.Services;

public class AuthServiceTests
{
    private readonly Mock<IUnitOfWork> _unitOfWork;
    private readonly Mock<IJwtService> _jwtService;
    private readonly AuthService _sut;

    public AuthServiceTests()
    {
        _unitOfWork = new Mock<IUnitOfWork>();
        _jwtService = new Mock<IJwtService>();
        _sut = new AuthService(_unitOfWork.Object, _jwtService.Object);
    }

    // =====================================================
    // Register Tests
    // =====================================================

    [Fact]
    public async Task RegisterAsync_PasswordMismatch_ThrowsArgumentException()
    {
        // Arrange
        var request = new RegisterRequest(
            "John", "Doe", "test@test.com",
            "0812345678", "Password1", "DifferentPassword");

        // Act
        var act = () => _sut.RegisterAsync(request);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("Passwords do not match.");
    }

    [Fact]
    public async Task RegisterAsync_DuplicateEmail_ThrowsDuplicateException()
    {
        // Arrange
        _unitOfWork.Setup(x => x.Users.EmailExistsAsync(
            It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var request = new RegisterRequest(
            "John", "Doe", "existing@test.com",
            "0812345678", "Password1", "Password1");

        // Act
        var act = () => _sut.RegisterAsync(request);

        // Assert
        await act.Should().ThrowAsync<DuplicateException>()
            .WithMessage("Email already registered.");
    }

    [Fact]
    public async Task RegisterAsync_DuplicatePhone_ThrowsDuplicateException()
    {
        // Arrange
        _unitOfWork.Setup(x => x.Users.EmailExistsAsync(
            It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _unitOfWork.Setup(x => x.Users.PhoneExistsAsync(
            It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var request = new RegisterRequest(
            "John", "Doe", "new@test.com",
            "0812345678", "Password1", "Password1");

        // Act
        var act = () => _sut.RegisterAsync(request);

        // Assert
        await act.Should().ThrowAsync<DuplicateException>()
            .WithMessage("Phone number already registered.");
    }

    [Fact]
    public async Task RegisterAsync_ValidData_ReturnsAuthResponse()
    {
        // Arrange
        _unitOfWork.Setup(x => x.Users.EmailExistsAsync(
            It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _unitOfWork.Setup(x => x.Users.PhoneExistsAsync(
            It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _unitOfWork.Setup(x => x.Accounts.AccountNumberExistsAsync(
            It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _unitOfWork.Setup(x => x.Users.AddAsync(
            It.IsAny<User>(), It.IsAny<CancellationToken>()));
        _unitOfWork.Setup(x => x.Accounts.AddAsync(
            It.IsAny<Account>(), It.IsAny<CancellationToken>()));
        _unitOfWork.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        _jwtService.Setup(x => x.GenerateAccessToken(It.IsAny<User>()))
            .Returns("test-access-token");
        _jwtService.Setup(x => x.GenerateRefreshToken())
            .Returns("test-refresh-token");

        var request = new RegisterRequest(
            "John", "Doe", "john@test.com",
            "0812345678", "Password1", "Password1");

        // Act
        var result = await _sut.RegisterAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Email.Should().Be("john@test.com");
        result.FullName.Should().Be("John Doe");
        result.AccessToken.Should().Be("test-access-token");
        result.RefreshToken.Should().Be("test-refresh-token");
    }

    // =====================================================
    // Login Tests
    // =====================================================

    [Fact]
    public async Task LoginAsync_UserNotFound_ThrowsArgumentException()
    {
        // Arrange
        _unitOfWork.Setup(x => x.Users.GetByEmailAsync(
            It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var request = new LoginRequest("notfound@test.com", "password");

        // Act
        var act = () => _sut.LoginAsync(request);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("Invalid email or password.");
    }

    [Fact]
    public async Task LoginAsync_LockedAccount_ThrowsAccountLockedException()
    {
        // Arrange
        var user = CreateTestUser(isLocked: true);
        _unitOfWork.Setup(x => x.Users.GetByEmailAsync(
            It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var request = new LoginRequest("test@test.com", "password");

        // Act
        var act = () => _sut.LoginAsync(request);

        // Assert
        await act.Should().ThrowAsync<AccountLockedException>();
    }

    [Fact]
    public async Task LoginAsync_WrongPassword_IncrementsFailedAttempts()
    {
        // Arrange
        var user = CreateTestUser();
        _unitOfWork.Setup(x => x.Users.GetByEmailAsync(
            It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _unitOfWork.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var request = new LoginRequest("test@test.com", "WrongPassword");

        // Act
        var act = () => _sut.LoginAsync(request);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
        user.FailedLoginAttempts.Should().Be(1);
    }

    [Fact]
    public async Task LoginAsync_WrongPassword5Times_LocksAccount()
    {
        // Arrange
        var user = CreateTestUser();
        user.FailedLoginAttempts = 4; // จะเป็นครั้งที่ 5

        _unitOfWork.Setup(x => x.Users.GetByEmailAsync(
            It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _unitOfWork.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var request = new LoginRequest("test@test.com", "WrongPassword");

        // Act
        var act = () => _sut.LoginAsync(request);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
        user.IsLocked.Should().BeTrue();
        user.FailedLoginAttempts.Should().Be(5);
    }

    [Fact]
    public async Task LoginAsync_CorrectPassword_ResetsFailedAttempts()
    {
        // Arrange
        var user = CreateTestUser();
        user.FailedLoginAttempts = 3; // เคยผิด 3 ครั้ง

        _unitOfWork.Setup(x => x.Users.GetByEmailAsync(
            It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _unitOfWork.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        _jwtService.Setup(x => x.GenerateAccessToken(It.IsAny<User>()))
            .Returns("token");
        _jwtService.Setup(x => x.GenerateRefreshToken())
            .Returns("refresh");

        var request = new LoginRequest("test@test.com", "Password1");

        // Act
        var result = await _sut.LoginAsync(request);

        // Assert
        result.Should().NotBeNull();
        user.FailedLoginAttempts.Should().Be(0);
    }

    // =====================================================
    // Helpers
    // =====================================================

    private static User CreateTestUser(bool isLocked = false)
    {
        return new User
        {
            Id = Guid.NewGuid(),
            FirstName = "Test",
            LastName = "User",
            Email = "test@test.com",
            Phone = "0812345678",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password1"),
            IsLocked = isLocked,
            FailedLoginAttempts = 0
        };
    }
}
