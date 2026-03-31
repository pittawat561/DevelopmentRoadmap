using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

namespace Banking.Infrastructure.Hubs;

/// <summary>
/// SignalR Hub สำหรับ real-time notifications
///
/// Flow:
///   1. Client connect → เข้า group "user:{userId}"
///   2. มีคนฝากเงินเข้าบัญชี → server ส่ง "BalanceUpdated" ไปยัง group
///   3. Client ได้รับ event → อัปเดต UI ทันที
///
/// Hub vs Controller:
///   Controller: Client ส่ง request → Server ตอบ (request/response)
///   Hub: Server ส่งข้อมูลไปหา Client ได้เลย (push)
/// </summary>
[Authorize]
public class NotificationHub : Hub
{
    private readonly ILogger<NotificationHub> _logger;

    public NotificationHub(ILogger<NotificationHub> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// เมื่อ client connect — เพิ่มเข้า group ของ user
    ///
    /// Group = ห้องส่งข้อความ
    ///   user:123 → ทุก device ของ user 123 อยู่ใน group เดียวกัน
    ///   เปิด browser 3 tab → ทั้ง 3 ได้รับ notification
    ///
    /// Context.User ดึงข้อมูลจาก JWT token (ผ่าน [Authorize])
    /// Context.ConnectionId เป็น ID unique ของแต่ละ connection
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId is not null)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user:{userId}");
            _logger.LogInformation(
                "User {UserId} connected to NotificationHub (ConnectionId: {ConnectionId})",
                userId, Context.ConnectionId);
        }

        await base.OnConnectedAsync();
    }

    /// <summary>
    /// เมื่อ client disconnect — ลบออกจาก group
    /// SignalR จัดการ cleanup อัตโนมัติ แต่ log ไว้เพื่อ debug
    /// </summary>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId is not null)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"user:{userId}");
            _logger.LogInformation(
                "User {UserId} disconnected from NotificationHub", userId);
        }

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Client สมัครรับ notification สำหรับบัญชีเฉพาะ
    ///
    /// ใช้เมื่อ user มีหลายบัญชี — สมัครเฉพาะบัญชีที่กำลังดูอยู่
    ///   JoinAccountGroup("account:abc-123") → ได้รับ balance update ของบัญชีนั้น
    /// </summary>
    public async Task JoinAccountGroup(string accountId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"account:{accountId}");
    }

    public async Task LeaveAccountGroup(string accountId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"account:{accountId}");
    }
}
