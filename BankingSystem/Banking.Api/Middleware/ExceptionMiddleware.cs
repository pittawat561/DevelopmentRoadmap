using Banking.Domain.Exceptions;
using System.Net;
using System.Text.Json;

namespace Banking.Api.Middleware;

public class ExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionMiddleware> _logger;

    public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var (statusCode, message) = exception switch
        {
            NotFoundException ex => (HttpStatusCode.NotFound, ex.Message),
            InsufficientFundsException ex => (HttpStatusCode.BadRequest, ex.Message),
            DailyLimitExceededException ex => (HttpStatusCode.BadRequest, ex.Message),
            AccountFrozenException ex => (HttpStatusCode.Forbidden, ex.Message),
            AccountLockedException ex => (HttpStatusCode.Forbidden, ex.Message),
            DuplicateException ex => (HttpStatusCode.Conflict, ex.Message),
            ArgumentException ex => (HttpStatusCode.BadRequest, ex.Message),
            _ => (HttpStatusCode.InternalServerError, "An unexpected error occurred.")
        };

        if (statusCode == HttpStatusCode.InternalServerError)
            _logger.LogError(exception, "Unhandled exception: {Message}", exception.Message);
        else
            _logger.LogWarning("Handled exception: {Type} — {Message}",
                exception.GetType().Name, exception.Message);

        context.Response.ContentType = "application/problem+json";
        context.Response.StatusCode = (int)statusCode;

        var response = new
        {
            success = false,
            message = message,
            statusCode = (int)statusCode
        };

        await context.Response.WriteAsync(
            JsonSerializer.Serialize(response, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            }));
    }
}
