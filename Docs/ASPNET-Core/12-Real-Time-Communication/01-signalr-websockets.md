# Real-Time Communication — SignalR & WebSockets

> แจ้งเตือน, chat, live dashboard แบบ real-time ใน .NET

---

## 1. SignalR คืออะไร

```
SignalR = library สำหรับ real-time communication ใน ASP.NET Core
- Server push ข้อมูลไป Client ได้ทันที (ไม่ต้องรอ client poll)
- จัดการ WebSocket, Server-Sent Events, Long Polling อัตโนมัติ
- Groups, Users targeting built-in

ใช้ทำอะไร:
- Chat application
- Live notifications (ออเดอร์สถานะเปลี่ยน, มี comment ใหม่)
- Real-time dashboard (กราฟ sales, จำนวน users online)
- Collaborative editing
- Live sports scores
```

### ติดตั้ง

```bash
dotnet add package Microsoft.AspNetCore.SignalR
# Client-side: npm install @microsoft/signalr
```

---

## 2. สร้าง SignalR Hub

```csharp
// ===== Hub = endpoint ที่ clients เชื่อมต่อ =====
// Hubs/NotificationHub.cs
public class NotificationHub : Hub
{
    private readonly ILogger<NotificationHub> _logger;

    public NotificationHub(ILogger<NotificationHub> logger) => _logger = logger;

    // Client เชื่อมต่อ
    public override async Task OnConnectedAsync()
    {
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId is not null)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user:{userId}");
            _logger.LogInformation("User {UserId} connected (ConnectionId: {ConnectionId})",
                userId, Context.ConnectionId);
        }
        await base.OnConnectedAsync();
    }

    // Client ตัดการเชื่อมต่อ
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("Client disconnected: {ConnectionId}", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    // ===== Methods ที่ Client เรียกได้ =====

    // ส่งข้อความไป client ทุกคน
    public async Task SendMessage(string message)
    {
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "anonymous";
        await Clients.All.SendAsync("ReceiveMessage", new
        {
            UserId = userId,
            Message = message,
            Timestamp = DateTime.UtcNow
        });
    }

    // เข้าร่วม group (เช่น chat room)
    public async Task JoinGroup(string groupName)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        await Clients.Group(groupName).SendAsync("UserJoined",
            Context.User?.Identity?.Name ?? "Anonymous");
    }

    // ออกจาก group
    public async Task LeaveGroup(string groupName)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
    }

    // ส่งข้อความเฉพาะ group
    public async Task SendToGroup(string groupName, string message)
    {
        await Clients.Group(groupName).SendAsync("ReceiveMessage", new
        {
            UserId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value,
            Message = message,
            Timestamp = DateTime.UtcNow
        });
    }
}

// ===== Chat Hub (ตัวอย่างเต็ม) =====
public class ChatHub : Hub
{
    public async Task SendToUser(string targetUserId, string message)
    {
        // ส่งถึง user คนเดียว (ทุก connections ของ user นั้น)
        await Clients.Group($"user:{targetUserId}").SendAsync("ReceiveDirectMessage", new
        {
            From = Context.User?.Identity?.Name,
            Message = message,
            Timestamp = DateTime.UtcNow
        });
    }

    // Typing indicator
    public async Task StartTyping(string groupName)
    {
        await Clients.OthersInGroup(groupName).SendAsync("UserTyping",
            Context.User?.Identity?.Name);
    }
}

// ===== Program.cs =====
builder.Services.AddSignalR()
    .AddJsonProtocol(options =>
    {
        options.PayloadSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    });

// ถ้าหลาย instances → ใช้ Redis backplane
// builder.Services.AddSignalR().AddStackExchangeRedis(redisConnectionString);

app.MapHub<NotificationHub>("/hubs/notifications");
app.MapHub<ChatHub>("/hubs/chat");
```

---

## 3. ส่ง Notification จาก Service (นอก Hub)

```csharp
// ใช้ IHubContext<T> inject เข้า service

public class OrderService : IOrderService
{
    private readonly AppDbContext _context;
    private readonly IHubContext<NotificationHub> _hubContext;

    public OrderService(AppDbContext context, IHubContext<NotificationHub> hubContext)
    {
        _context = context;
        _hubContext = hubContext;
    }

    public async Task<OrderDto> UpdateStatusAsync(int orderId, OrderStatus newStatus)
    {
        var order = await _context.Orders
            .Include(o => o.User)
            .FirstOrDefaultAsync(o => o.Id == orderId)
            ?? throw new NotFoundException("Order", orderId);

        order.Status = newStatus;
        await _context.SaveChangesAsync();

        // ===== แจ้ง user เจ้าของ order แบบ real-time! =====
        await _hubContext.Clients
            .Group($"user:{order.UserId}")
            .SendAsync("OrderStatusChanged", new
            {
                OrderId = order.Id,
                OrderNumber = order.OrderNumber,
                NewStatus = newStatus.ToString(),
                Timestamp = DateTime.UtcNow
            });

        // แจ้ง admin ทุกคน
        await _hubContext.Clients
            .Group("admins")
            .SendAsync("OrderUpdated", new { OrderId = order.Id, Status = newStatus.ToString() });

        return MapToDto(order);
    }
}
```

---

## 4. JavaScript Client

```javascript
// npm install @microsoft/signalr

import { HubConnectionBuilder, LogLevel } from '@microsoft/signalr';

const connection = new HubConnectionBuilder()
    .withUrl('/hubs/notifications', {
        accessTokenFactory: () => localStorage.getItem('jwt_token')
    })
    .withAutomaticReconnect([0, 2000, 5000, 10000, 30000]) // retry intervals
    .configureLogging(LogLevel.Information)
    .build();

// รับ events
connection.on('ReceiveMessage', (data) => {
    console.log(`${data.userId}: ${data.message}`);
});

connection.on('OrderStatusChanged', (data) => {
    showNotification(`Order ${data.orderNumber} is now ${data.newStatus}`);
});

// เชื่อมต่อ
await connection.start();
console.log('Connected to SignalR');

// ส่ง events
await connection.invoke('SendMessage', 'Hello everyone!');
await connection.invoke('JoinGroup', 'order-updates');

// Reconnect handling
connection.onreconnecting(() => showStatus('Reconnecting...'));
connection.onreconnected(() => showStatus('Connected'));
connection.onclose(() => showStatus('Disconnected'));
```

---

## 5. เมื่อไหร่ใช้ SignalR vs Polling

```
| สถานการณ์                  | ใช้              |
|---------------------------|------------------|
| Chat / Messaging          | SignalR ✅        |
| Live notifications        | SignalR ✅        |
| Real-time dashboard       | SignalR ✅        |
| ข้อมูลอัปเดตทุก 5+ นาที    | Polling ก็พอ     |
| ดึงข้อมูลครั้งเดียว         | REST API ปกติ    |
| File upload progress      | SignalR ✅        |
```
