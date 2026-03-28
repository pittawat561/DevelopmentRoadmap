# Testing ใน ASP.NET Core — Unit, Integration, E2E

> เขียน test ให้ครบ เพื่อ deploy อย่างมั่นใจ

---

## 1. Testing Pyramid

```
          /  E2E Tests  \         ← น้อย, ช้า, แพง (Playwright/Cypress)
         / Integration   \        ← ปานกลาง (WebApplicationFactory)
        /   Unit Tests    \       ← เยอะ, เร็ว, ถูก (xUnit + Moq)
       ‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾

Unit Test:       test 1 method/class แยกจากทุกอย่าง (mock dependencies)
Integration:     test หลาย layers ร่วมกัน (real DB, real middleware)
E2E:             test ทั้ง system เหมือน user จริง (browser automation)
```

---

## 2. Unit Testing — xUnit + Moq

```bash
dotnet new xunit -n MyApi.Tests.Unit
dotnet add MyApi.Tests.Unit package Moq
dotnet add MyApi.Tests.Unit package FluentAssertions
dotnet add MyApi.Tests.Unit reference MyApi.Api
```

```csharp
// ===== Test Service Layer =====
public class UserServiceTests
{
    private readonly Mock<AppDbContext> _mockContext;
    private readonly Mock<ILogger<UserService>> _mockLogger;
    private readonly UserService _sut;  // System Under Test

    public UserServiceTests()
    {
        _mockContext = new Mock<AppDbContext>(new DbContextOptions<AppDbContext>());
        _mockLogger = new Mock<ILogger<UserService>>();
        _sut = new UserService(_mockContext.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task CreateAsync_ValidRequest_ReturnsUserDto()
    {
        // Arrange
        var request = new CreateUserRequest("John", "Doe", "john@test.com", "Pass123!");

        var mockDbSet = new Mock<DbSet<User>>();
        _mockContext.Setup(c => c.Users).Returns(mockDbSet.Object);
        _mockContext.Setup(c => c.SaveChangesAsync(default)).ReturnsAsync(1);

        // Act
        var result = await _sut.CreateAsync(request);

        // Assert (FluentAssertions — อ่านง่ายกว่า!)
        result.Should().NotBeNull();
        result.FirstName.Should().Be("John");
        result.Email.Should().Be("john@test.com");

        mockDbSet.Verify(d => d.AddAsync(It.IsAny<User>(), default), Times.Once);
        _mockContext.Verify(c => c.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task GetByIdAsync_NotFound_ThrowsNotFoundException()
    {
        // Arrange
        _mockContext.Setup(c => c.Users.FindAsync(999))
            .ReturnsAsync((User?)null);

        // Act & Assert
        await _sut.Invoking(s => s.GetByIdAsync(999))
            .Should().ThrowAsync<NotFoundException>()
            .WithMessage("*999*");
    }

    [Theory]
    [InlineData("", "Doe", "john@test.com")]     // empty first name
    [InlineData("John", "", "john@test.com")]     // empty last name
    [InlineData("John", "Doe", "invalid-email")]  // invalid email
    public async Task CreateAsync_InvalidInput_ThrowsValidationException(
        string firstName, string lastName, string email)
    {
        var request = new CreateUserRequest(firstName, lastName, email, "Pass123!");

        await _sut.Invoking(s => s.CreateAsync(request))
            .Should().ThrowAsync<ValidationException>();
    }
}

// ===== Test Validators =====
public class CreateUserRequestValidatorTests
{
    private readonly CreateUserRequestValidator _validator = new();

    [Fact]
    public void Valid_Request_Passes()
    {
        var request = new CreateUserRequest("John", "Doe", "john@test.com", "Pass123!");
        var result = _validator.Validate(request);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Empty_Email_Fails()
    {
        var request = new CreateUserRequest("John", "Doe", "", "Pass123!");
        var result = _validator.Validate(request);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Email");
    }

    [Fact]
    public void Short_Password_Fails()
    {
        var request = new CreateUserRequest("John", "Doe", "john@test.com", "123");
        var result = _validator.Validate(request);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Password");
    }
}
```

---

## 3. Integration Testing — WebApplicationFactory

```bash
dotnet new xunit -n MyApi.Tests.Integration
dotnet add MyApi.Tests.Integration package Microsoft.AspNetCore.Mvc.Testing
dotnet add MyApi.Tests.Integration package Testcontainers.MsSql  # real DB in Docker!
dotnet add MyApi.Tests.Integration package Respawn                # reset DB between tests
```

```csharp
// ===== Custom WebApplicationFactory =====
public class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly MsSqlContainer _dbContainer = new MsSqlBuilder()
        .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
        .Build();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // แทนที่ DB จริงด้วย test container
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            if (descriptor is not null) services.Remove(descriptor);

            services.AddDbContext<AppDbContext>(options =>
                options.UseSqlServer(_dbContainer.GetConnectionString()));
        });
    }

    public async Task InitializeAsync()
    {
        await _dbContainer.StartAsync();

        // Apply migrations
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();
    }

    public new async Task DisposeAsync() => await _dbContainer.DisposeAsync();
}

// ===== Integration Tests =====
public class UsersApiTests : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client;
    private readonly ApiFactory _factory;

    public UsersApiTests(ApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CreateUser_ValidData_Returns201()
    {
        // Arrange
        var request = new CreateUserRequest("John", "Doe", "john@test.com", "Pass123!");

        // Act
        var response = await _client.PostAsJsonAsync("/api/users", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var user = await response.Content.ReadFromJsonAsync<UserDto>();
        user.Should().NotBeNull();
        user!.FirstName.Should().Be("John");
        user.Email.Should().Be("john@test.com");

        response.Headers.Location.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateUser_DuplicateEmail_Returns409()
    {
        var request = new CreateUserRequest("Jane", "Doe", "duplicate@test.com", "Pass123!");

        await _client.PostAsJsonAsync("/api/users", request);  // first → 201
        var response = await _client.PostAsJsonAsync("/api/users", request);  // second → 409

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task GetUser_NotFound_Returns404()
    {
        var response = await _client.GetAsync("/api/users/99999");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetUsers_WithPagination_ReturnsPaginatedList()
    {
        // Seed data
        for (int i = 0; i < 25; i++)
            await _client.PostAsJsonAsync("/api/users",
                new CreateUserRequest($"User{i}", "Test", $"user{i}@test.com", "Pass123!"));

        // Act
        var response = await _client.GetFromJsonAsync<PaginatedResponse<UserDto>>(
            "/api/users?page=2&pageSize=10");

        // Assert
        response.Should().NotBeNull();
        response!.Items.Should().HaveCount(10);
        response.Page.Should().Be(2);
        response.TotalCount.Should().BeGreaterOrEqualTo(25);
    }

    // ===== Test with Authentication =====
    [Fact]
    public async Task DeleteUser_AsAdmin_Returns204()
    {
        // Login as admin
        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest("admin@test.com", "Admin123!"));
        var token = (await loginResponse.Content.ReadFromJsonAsync<LoginResponse>())!.Token;

        // Create user to delete
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var createResponse = await _client.PostAsJsonAsync("/api/users",
            new CreateUserRequest("ToDelete", "User", "delete@test.com", "Pass123!"));
        var userId = (await createResponse.Content.ReadFromJsonAsync<UserDto>())!.Id;

        // Delete
        var deleteResponse = await _client.DeleteAsync($"/api/users/{userId}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task DeleteUser_Unauthorized_Returns401()
    {
        _client.DefaultRequestHeaders.Authorization = null;  // ไม่มี token
        var response = await _client.DeleteAsync("/api/users/1");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
```

---

## 4. Fake Data Generation

```csharp
// dotnet add package Bogus
using Bogus;

public static class FakeData
{
    public static Faker<User> UserFaker => new Faker<User>()
        .RuleFor(u => u.FirstName, f => f.Name.FirstName())
        .RuleFor(u => u.LastName, f => f.Name.LastName())
        .RuleFor(u => u.Email, (f, u) => f.Internet.Email(u.FirstName, u.LastName))
        .RuleFor(u => u.IsActive, true)
        .RuleFor(u => u.CreatedAt, f => f.Date.Past(1));

    public static Faker<Order> OrderFaker => new Faker<Order>()
        .RuleFor(o => o.OrderNumber, f => $"ORD-{f.Random.Number(10000, 99999)}")
        .RuleFor(o => o.Total, f => f.Finance.Amount(10, 1000))
        .RuleFor(o => o.Status, f => f.PickRandom<OrderStatus>());
}

// ใช้ใน tests:
var users = FakeData.UserFaker.Generate(50);
var orders = FakeData.OrderFaker.Generate(100);
```

---

## 5. รัน Tests

```bash
dotnet test                                    # รันทั้งหมด
dotnet test --filter "FullyQualifiedName~UserService"  # เฉพาะ class
dotnet test --filter "Category=Integration"     # เฉพาะ category
dotnet test --collect:"XPlat Code Coverage"     # code coverage
dotnet test --logger "console;verbosity=detailed"
```
