namespace Banking.Application.Services;

/// <summary>
/// Notification Service Interface
/// ส่งข้อความ real-time ไปยัง client
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// แจ้ง user ว่า balance เปลี่ยน — ส่งผ่าน SignalR
    /// </summary>
    Task NotifyBalanceUpdatedAsync(
        Guid userId,
        Guid accountId,
        decimal newBalance,
        decimal newAvailableBalance);

    /// <summary>
    /// แจ้ง user ว่ามี transaction ใหม่
    /// </summary>
    Task NotifyTransactionAsync(
        Guid userId,
        string type,
        decimal amount,
        string referenceNumber);
}