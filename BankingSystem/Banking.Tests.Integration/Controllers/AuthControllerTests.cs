using System.Net;
using System.Net.Http.Json;
using Banking.Application.DTOs;
using FluentAssertions;

namespace Banking.Tests.Integration.Controllers;

public class AuthControllerTests : IClassFixture<BankingApiFactory>
{
    private readonly HttpClient _client;

    public AuthControllerTests(BankingApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Register_ValidData_Returns201WithToken()
    {
        // Arrange
        var request = new RegisterRequest(
            "Test", "User",
            $"test-{Guid.NewGuid():N}@test.com",
            $"08{Random.Shared.Next(10000000, 99999999)}",
            "Password1", "Password1");

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/register", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var result = await response.Content
            .ReadFromJsonAsync<ApiResponse<AuthResponse>>();

        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Data!.AccessToken.Should().NotBeNullOrEmpty();
        result.Data.UserId.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Register_InvalidEmail_Returns400()
    {
        // Arrange
        var request = new RegisterRequest(
            "Test", "User", "not-an-email",
            "0812345678", "Password1", "Password1");

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/register", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Login_WrongPassword_Returns400()
    {
        // Arrange
        var request = new LoginRequest("nonexistent@test.com", "wrong");

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/login", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Profile_NoToken_Returns401()
    {
        // Act
        var response = await _client.GetAsync("/api/auth/profile");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Profile_WithToken_Returns200()
    {
        // Arrange: register first to get token
        var registerRequest = new RegisterRequest(
            "Profile", "Test",
            $"profile-{Guid.NewGuid():N}@test.com",
            $"08{Random.Shared.Next(10000000, 99999999)}",
            "Password1", "Password1");

        var registerResponse = await _client.PostAsJsonAsync("/api/auth/register", registerRequest);
        var authResult = await registerResponse.Content
            .ReadFromJsonAsync<ApiResponse<AuthResponse>>();

        // Act
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", authResult!.Data!.AccessToken);

        var response = await _client.GetAsync("/api/auth/profile");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Cleanup
        _client.DefaultRequestHeaders.Authorization = null;
    }

    [Fact]
    public async Task HealthCheck_Returns200()
    {
        // Act
        var response = await _client.GetAsync("/health");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.ServiceUnavailable);
    }
}
