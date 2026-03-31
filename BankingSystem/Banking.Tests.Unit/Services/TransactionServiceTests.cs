using Banking.Application.DTOs;
using Banking.Application.Services;
using Banking.Domain.Entities;
using Banking.Domain.Enums;
using Banking.Domain.Exceptions;
using Banking.Domain.Interfaces;
using FluentAssertions;
using Moq;

namespace Banking.Tests.Unit.Services;

public class TransactionServiceTests
{
    private readonly Mock<IUnitOfWork> _unitOfWork;
    private readonly Mock<IRedisCacheService> _cache;
    private readonly Mock<INotificationService> _notification;
    private readonly Mock<IAuditService> _auditService;
    private readonly PinService _pinService;
    private readonly TransactionService _sut;

    // Test user with PIN set
    private readonly User _testUser;
    private const string TestPin = "123456";

    public TransactionServiceTests()
    {
        _unitOfWork = new Mock<IUnitOfWork>();
        _cache = new Mock<IRedisCacheService>();
        _notification = new Mock<INotificationService>();
        _auditService = new Mock<IAuditService>();

        _pinService = new PinService(_unitOfWork.Object);

        _sut = new TransactionService(
            _unitOfWork.Object,
            _cache.Object,
            _notification.Object,
            _pinService,
            _auditService.Object);

        // Setup test user with PIN
        _testUser = new User
        {
            Id = Guid.NewGuid(),
            FirstName = "Test",
            LastName = "User",
            Email = "test@test.com",
            Phone = "0812345678",
            PasswordHash = "hash",
            PinHash = BCrypt.Net.BCrypt.HashPassword(TestPin),
            IsTransactionLocked = false,
            FailedPinAttempts = 0
        };
    }

    // =====================================================
    // Deposit Tests
    // =====================================================

    [Fact]
    public async Task DepositAsync_ValidAmount_IncreasesBalance()
    {
        // Arrange
        var account = CreateTestAccount(balance: 10_000);
        SetupMocks(account);

        var request = new DepositRequest(account.Id, 5_000, "Test deposit", TestPin);

        // Act
        var result = await _sut.DepositAsync(request, _testUser.Id, TestPin);

        // Assert
        result.Should().NotBeNull();
        result.Type.Should().Be("Deposit");
        result.Amount.Should().Be(5_000);
        result.BalanceBefore.Should().Be(10_000);
        result.BalanceAfter.Should().Be(15_000);
        result.Status.Should().Be("Completed");
    }

    [Fact]
    public async Task DepositAsync_ZeroAmount_ThrowsArgumentException()
    {
        // Arrange
        var request = new DepositRequest(Guid.NewGuid(), 0, null, TestPin);

        // Act
        var act = () => _sut.DepositAsync(request, _testUser.Id, TestPin);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("Amount must be greater than 0.");
    }

    [Fact]
    public async Task DepositAsync_NegativeAmount_ThrowsArgumentException()
    {
        // Arrange
        var request = new DepositRequest(Guid.NewGuid(), -100, null, TestPin);

        // Act
        var act = () => _sut.DepositAsync(request, _testUser.Id, TestPin);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task DepositAsync_FrozenAccount_ThrowsAccountFrozenException()
    {
        // Arrange
        var account = CreateTestAccount(status: AccountStatus.Frozen);
        SetupMocks(account);

        var request = new DepositRequest(account.Id, 1_000, null, TestPin);

        // Act
        var act = () => _sut.DepositAsync(request, _testUser.Id, TestPin);

        // Assert
        await act.Should().ThrowAsync<AccountFrozenException>();
    }

    [Fact]
    public async Task DepositAsync_AccountNotFound_ThrowsNotFoundException()
    {
        // Arrange
        SetupLockMock();
        SetupPinMock();
        _unitOfWork.Setup(x => x.Accounts.GetByIdForUpdateAsync(
            It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Account?)null);

        var request = new DepositRequest(Guid.NewGuid(), 1_000, null, TestPin);

        // Act
        var act = () => _sut.DepositAsync(request, _testUser.Id, TestPin);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    // =====================================================
    // Withdraw Tests
    // =====================================================

    [Fact]
    public async Task WithdrawAsync_SufficientFunds_DecreasesBalance()
    {
        // Arrange
        var account = CreateTestAccount(balance: 10_000);
        SetupMocks(account);
        SetupDailyLimitMock(0);

        var request = new WithdrawRequest(account.Id, 3_000, null, TestPin);

        // Act
        var result = await _sut.WithdrawAsync(request, _testUser.Id, TestPin);

        // Assert
        result.BalanceBefore.Should().Be(10_000);
        result.BalanceAfter.Should().Be(7_000);
        result.Type.Should().Be("Withdrawal");
    }

    [Fact]
    public async Task WithdrawAsync_InsufficientFunds_ThrowsException()
    {
        // Arrange
        var account = CreateTestAccount(balance: 1_000);
        SetupMocks(account);
        SetupDailyLimitMock(0);

        var request = new WithdrawRequest(account.Id, 5_000, null, TestPin);

        // Act
        var act = () => _sut.WithdrawAsync(request, _testUser.Id, TestPin);

        // Assert
        await act.Should().ThrowAsync<InsufficientFundsException>();
    }

    [Fact]
    public async Task WithdrawAsync_ExceedsDailyLimit_ThrowsException()
    {
        // Arrange
        var account = CreateTestAccount(balance: 100_000);
        SetupMocks(account);
        SetupDailyLimitMock(45_000); // ถอนไปแล้ว 45,000 วันนี้

        var request = new WithdrawRequest(account.Id, 10_000, null, TestPin);
        // 45,000 + 10,000 = 55,000 > daily limit 50,000

        // Act
        var act = () => _sut.WithdrawAsync(request, _testUser.Id, TestPin);

        // Assert
        await act.Should().ThrowAsync<DailyLimitExceededException>();
    }

    [Fact]
    public async Task WithdrawAsync_ExactBalance_Succeeds()
    {
        // Arrange
        var account = CreateTestAccount(balance: 5_000);
        SetupMocks(account);
        SetupDailyLimitMock(0);

        var request = new WithdrawRequest(account.Id, 5_000, null, TestPin);

        // Act
        var result = await _sut.WithdrawAsync(request, _testUser.Id, TestPin);

        // Assert
        result.BalanceAfter.Should().Be(0);
    }

    // =====================================================
    // Transfer Tests
    // =====================================================

    [Fact]
    public async Task TransferAsync_SameAccount_ThrowsArgumentException()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var request = new TransferRequest(accountId, accountId, 1_000, null, TestPin);

        // Act
        var act = () => _sut.TransferAsync(request, _testUser.Id, TestPin);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("Cannot transfer to the same account.");
    }

    [Fact]
    public async Task TransferAsync_ZeroAmount_ThrowsArgumentException()
    {
        // Arrange
        var request = new TransferRequest(Guid.NewGuid(), Guid.NewGuid(), 0, null, TestPin);

        // Act
        var act = () => _sut.TransferAsync(request, _testUser.Id, TestPin);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("Amount must be greater than 0.");
    }

    // =====================================================
    // Helpers
    // =====================================================

    private static Account CreateTestAccount(
        decimal balance = 10_000,
        AccountStatus status = AccountStatus.Active)
    {
        return new Account
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            AccountNumber = "1234-5678-9012",
            Type = AccountType.Savings,
            Currency = "THB",
            Balance = balance,
            AvailableBalance = balance,
            DailyWithdrawalLimit = 50_000,
            Status = status
        };
    }

    private void SetupMocks(Account account)
    {
        SetupLockMock();
        SetupPinMock();

        _unitOfWork.Setup(x => x.Accounts.GetByIdForUpdateAsync(
            account.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        _unitOfWork.Setup(x => x.Accounts.Update(It.IsAny<Account>()));
        _unitOfWork.Setup(x => x.Transactions.AddAsync(
            It.IsAny<Transaction>(), It.IsAny<CancellationToken>()));
        _unitOfWork.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
    }

    private void SetupLockMock()
    {
        _cache.Setup(x => x.AcquireLockAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>()))
            .ReturnsAsync(true);

        _cache.Setup(x => x.ReleaseLockAsync(
            It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(true);
    }

    private void SetupPinMock()
    {
        _unitOfWork.Setup(x => x.Users.GetByIdAsync(
            _testUser.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_testUser);
    }

    private void SetupDailyLimitMock(decimal todayTotal)
    {
        _unitOfWork.Setup(x => x.Transactions.GetTodayWithdrawalTotalAsync(
            It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(todayTotal);
    }
}
