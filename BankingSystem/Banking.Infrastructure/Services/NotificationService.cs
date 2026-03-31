using Banking.Application.Services;
using Banking.Infrastructure.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace Banking.Infrastructure.Services;

/// <summary>
/// Notification Service — ใช้ SignalR IHubContext ส่ง real-time messages
///
/// IHubContext<NotificationHub>:
///   ใช้ส่งข้อความจาก service/controller ไปยัง Hub
///   ไม่ต้องอยู่ใน Hub class ก็ส่งได้
///
/// ทำไมใช้ IHubContext แทนการเรียก Hub ตรง:
///   Hub instance ถูกสร้าง/ทำลายทุก connection
///   IHubContext เป็น singleton — ใช้ได้ตลอด
/// </summary>
public class NotificationService : INotificationService
{
    private readonly IHubContext<NotificationHub> _hubContext;

    public NotificationService(IHubContext<NotificationHub> hubContext)
    { 
        _hubContext = hubContext;
    }

    public async Task NotifyBalanceUpdatedAsync(
        Guid userId, Guid accountId,
        decimal newBalance, decimal newAvailableBalance)
    {
        // ส่งไปยังทุก device ของ user
        await _hubContext.Clients
            .Group($"user:{userId}")
            .SendAsync("BalanceUpdated", new
            {
                AccountId = accountId,
                Balance = newBalance,
                AvailableBalance = newAvailableBalance,
                UpdatedAt = DateTime.UtcNow
            });

        // ส่งไปยัง client ที่ subscribe บัญชีนี้
        await _hubContext.Clients
            .Group($"account:{accountId}")
            .SendAsync("BalanceUpdated", new
            {
                AccountId = accountId,
                Balance = newBalance,
                AvailableBalance = newAvailableBalance,
                UpdatedAt = DateTime.UtcNow
            });
    }

    public async Task NotifyTransactionAsync(
        Guid userId, string type, decimal amount, string referenceNumber)
    {
        await _hubContext.Clients
            .Group($"user:{userId}")
            .SendAsync("TransactionCompleted", new
            {
                Type = type,
                Amount = amount,
                ReferenceNumber = referenceNumber,
                CreatedAt = DateTime.UtcNow
            });
    }
}