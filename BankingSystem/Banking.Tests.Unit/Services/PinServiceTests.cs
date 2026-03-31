using Banking.Application.DTOs;
using Banking.Application.Services;
using Banking.Domain.Entities;
using Banking.Domain.Exceptions;
using Banking.Domain.Interfaces;
using FluentAssertions;
using Moq;

namespace Banking.Tests.Unit.Services;

public class PinServiceTests
{
    private readonly Mock<IUnitOfWork> _unitOfWork;
    private readonly PinService _sut;

    public PinServiceTests()
    {
        _unitOfWork = new Mock<IUnitOfWork>();
        _sut = new PinService(_unitOfWork.Object);
    }

    [Fact]
    public async Task SetPinAsync_FirstTime_Success()
    {
        // Arrange
        var user = CreateTestUser(pinHash: null);
        SetupUserMock(user);

        var request = new SetPinRequest("123456", "123456");

        // Act
        await _sut.SetPinAsync(user.Id, request);

        // Assert
        user.PinHash.Should().NotBeNullOrEmpty();
        BCrypt.Net.BCrypt.Verify("123456", user.PinHash).Should().BeTrue();
    }

    [Fact]
    public async Task SetPinAsync_AlreadySet_ThrowsInvalidOperation()
    {
        // Arrange
        var user = CreateTestUser(pinHash: BCrypt.Net.BCrypt.HashPassword("111111"));
        SetupUserMock(user);

        var request = new SetPinRequest("123456", "123456");

        // Act
        var act = () => _sut.SetPinAsync(user.Id, request);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already set*");
    }

    [Fact]
    public async Task VerifyPinAsync_CorrectPin_NoException()
    {
        // Arrange
        var user = CreateTestUser(pinHash: BCrypt.Net.BCrypt.HashPassword("123456"));
        SetupUserMock(user);

        // Act
        var act = () => _sut.VerifyPinAsync(user.Id, "123456");

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task VerifyPinAsync_WrongPin_ThrowsAndIncrements()
    {
        // Arrange
        var user = CreateTestUser(pinHash: BCrypt.Net.BCrypt.HashPassword("123456"));
        SetupUserMock(user);

        // Act
        var act = () => _sut.VerifyPinAsync(user.Id, "999999");

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Incorrect PIN*");
        user.FailedPinAttempts.Should().Be(1);
    }

    [Fact]
    public async Task VerifyPinAsync_ThreeWrongPins_LocksTransactions()
    {
        // Arrange
        var user = CreateTestUser(pinHash: BCrypt.Net.BCrypt.HashPassword("123456"));
        user.FailedPinAttempts = 2; // จะเป็นครั้งที่ 3
        SetupUserMock(user);

        // Act
        var act = () => _sut.VerifyPinAsync(user.Id, "999999");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*locked*");
        user.IsTransactionLocked.Should().BeTrue();
    }

    [Fact]
    public async Task VerifyPinAsync_NoPinSet_ThrowsInvalidOperation()
    {
        // Arrange
        var user = CreateTestUser(pinHash: null);
        SetupUserMock(user);

        // Act
        var act = () => _sut.VerifyPinAsync(user.Id, "123456");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not set*");
    }

    [Fact]
    public async Task VerifyPinAsync_TransactionLocked_ThrowsInvalidOperation()
    {
        // Arrange
        var user = CreateTestUser(pinHash: BCrypt.Net.BCrypt.HashPassword("123456"));
        user.IsTransactionLocked = true;
        SetupUserMock(user);

        // Act
        var act = () => _sut.VerifyPinAsync(user.Id, "123456");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*locked*");
    }

    // =====================================================
    // Helpers
    // =====================================================

    private static User CreateTestUser(string? pinHash)
    {
        return new User
        {
            Id = Guid.NewGuid(),
            FirstName = "Test",
            LastName = "User",
            Email = "test@test.com",
            Phone = "0812345678",
            PasswordHash = "hash",
            PinHash = pinHash,
            FailedPinAttempts = 0,
            IsTransactionLocked = false
        };
    }

    private void SetupUserMock(User user)
    {
        _unitOfWork.Setup(x => x.Users.GetByIdAsync(
            user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _unitOfWork.Setup(x => x.Users.Update(It.IsAny<User>()));
        _unitOfWork.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
    }
}
