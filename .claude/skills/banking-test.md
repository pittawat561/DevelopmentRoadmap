---
name: Banking Test Generator
description: Generate unit tests + integration tests สำหรับ Banking System ด้วย xUnit, Moq, FluentAssertions
command: bank-test
argument-hint: "<target: class/handler name> [type: unit|integration|both]"
allowed-tools:
  - Read
  - Write
  - Edit
  - Glob
  - Bash
  - Grep
---

# Banking Test Generator Skill

คุณคือผู้เชี่ยวชาญ .NET testing สำหรับ Banking System ที่ต้องการ test coverage สูงโดยเฉพาะ financial logic

## Input
- **argument แรก:** Target class/handler name (required) — เช่น `DepositCommandHandler`, `AccountRepository`, `JwtService`
- **argument ที่สอง:** Test type (optional, default: `both`)
  - `unit` — Unit tests with Moq
  - `integration` — Integration tests with real DB
  - `both` — ทั้งสองแบบ

## Project Context
- **Test frameworks:** xUnit (test runner), Moq (mocking), FluentAssertions (assertions)
- **Unit test project:** `Banking.Tests.Unit/`
- **Integration test project:** `Banking.Tests.Integration/`
- **DB for integration:** PostgreSQL via TestContainers หรือ in-memory

## ขั้นตอนการทำงาน

### Step 1: อ่าน Target Class
1. อ่านไฟล์ของ class ที่จะ test
2. ระบุ dependencies (constructor injection)
3. ระบุ public methods ที่ต้อง test
4. ระบุ edge cases และ error scenarios

### Step 2: สร้าง Unit Tests
**Location:** `Banking.Tests.Unit/{Layer}/{ClassName}Tests.cs`

**Pattern:**
```csharp
using FluentAssertions;
using Moq;
using Xunit;

namespace Banking.Tests.Unit.{Layer};

public class {ClassName}Tests
{
    private readonly Mock<IDependency1> _dep1Mock;
    private readonly {ClassName} _sut; // System Under Test

    public {ClassName}Tests()
    {
        _dep1Mock = new Mock<IDependency1>();
        _sut = new {ClassName}(_dep1Mock.Object);
    }

    [Fact]
    public async Task {Method}_Should{Expected}_When{Condition}()
    {
        // Arrange
        // Act
        // Assert
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-100)]
    public async Task {Method}_ShouldThrow_WhenInvalidAmount(decimal amount)
    {
        // ...
    }
}
```

### Step 3: Critical Test Scenarios สำหรับ Banking

#### Transaction Handlers (MUST test):
- **Happy path:** ฝาก/ถอน/โอนสำเร็จ — balance ถูกต้อง
- **Insufficient funds:** ถอนเกิน balance → InsufficientFundsException
- **Frozen account:** ทำรายการกับ frozen account → AccountFrozenException
- **Daily limit:** ถอนเกิน daily limit → DailyLimitExceededException
- **Invalid amount:** amount <= 0 → ValidationException
- **Concurrent access:** 2 requests ถอนพร้อมกัน → ได้ผลถูกต้อง
- **Balance snapshot:** BalanceBefore/After ถูกต้องทุก transaction
- **Transfer self:** โอนไปบัญชีตัวเอง → rejected
- **Reference number:** unique ทุก transaction

#### Auth Tests:
- **Login success:** correct credentials → JWT + refresh token
- **Login fail:** wrong password → increment failed attempts
- **Account lockout:** 5 failed attempts → locked
- **Token expired:** → 401 Unauthorized
- **Refresh token:** valid refresh → new access token
- **Token blacklist:** revoked token → rejected

#### Repository Tests (Integration):
- **CRUD operations:** create, read, update, soft delete
- **Unique constraints:** duplicate email/phone → exception
- **Row locking:** `GetByIdForUpdateAsync` acquires lock
- **Pagination:** correct page/size behavior
- **Query filters:** soft-deleted records excluded

### Step 4: สร้าง Integration Tests
**Location:** `Banking.Tests.Integration/{Feature}/{ClassName}IntegrationTests.cs`

**Pattern using WebApplicationFactory:**
```csharp
public class {Feature}IntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public {Feature}IntegrationTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Replace real DB with test DB
                // Replace Redis with test instance
            });
        }).CreateClient();
    }

    [Fact]
    public async Task Deposit_ReturnsSuccess_WithValidRequest()
    {
        // Arrange: create test account via API
        // Act: POST /api/transactions/deposit
        // Assert: 200 OK + balance updated
    }
}
```

### Step 5: Test Helpers
สร้าง shared helpers ถ้ายังไม่มี:
- `TestDataBuilder` — สร้าง test entities ด้วย fluent API
- `FakeUserContext` — mock current user
- `TestDbContext` — in-memory DB setup

### Step 6: รัน Tests
```bash
dotnet test Banking.Tests.Unit/ --verbosity normal
dotnet test Banking.Tests.Integration/ --verbosity normal
dotnet test --collect:"XPlat Code Coverage"  # coverage report
```

## Naming Convention
- `{Method}_Should{ExpectedBehavior}_When{Condition}`
- เช่น `Handle_ShouldDecreaseBalance_WhenWithdrawalIsValid`
- เช่น `Handle_ShouldThrowInsufficientFunds_WhenBalanceIsLow`

## ตัวอย่างการใช้งาน
```
/bank-test DepositCommandHandler unit
/bank-test AccountRepository integration
/bank-test JwtService both
/bank-test WithdrawCommandHandler unit
```
