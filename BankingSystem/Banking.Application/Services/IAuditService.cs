namespace Banking.Application.Services;

/// <summary>
/// Audit Service Interface — บันทึกทุก action
/// </summary>
public interface IAuditService
{
    /// <summary>
    /// บันทึก audit log
    /// </summary>
    Task LogAsync(
        Guid? userId,
        string action,
        string entityType,
        string? entityId = null,
        object? oldValues = null,
        object? newValues = null,
        string? ipAddress = null,
        string? userAgent = null,
        CancellationToken ct = default);
}