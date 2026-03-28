# Microservices — Message Brokers & API Gateway

> แตก Monolith เป็น Microservices สื่อสารผ่าน messages

---

## 1. Monolith vs Microservices

```
Monolith:
┌────────────────────────────────┐
│   All-in-One Application       │
│  ┌──────┐ ┌──────┐ ┌────────┐ │
│  │Users │ │Orders│ │Products│ │
│  └──────┘ └──────┘ └────────┘ │
│          1 Database            │
└────────────────────────────────┘
✅ ง่าย, deploy ง่าย, debug ง่าย
❌ scale ยาก, deploy ทุก feature พร้อมกัน, codebase ใหญ่

Microservices:
┌──────────┐  ┌──────────┐  ┌──────────┐
│ User API │  │Order API │  │Product   │
│          │  │          │  │API       │
│  Own DB  │  │  Own DB  │  │  Own DB  │
└────┬─────┘  └────┬─────┘  └────┬─────┘
     │             │              │
     └─────── Message Broker ─────┘ (RabbitMQ / Kafka)
✅ scale แยกได้, deploy แยกได้, เลือก tech ต่างกันได้
❌ ซับซ้อน, network latency, distributed transactions

เริ่มต้น: Monolith ก่อน → แตกเป็น Microservices เมื่อจำเป็น
```

---

## 2. RabbitMQ (แนะนำเริ่มต้น)

```bash
# Docker
docker run -d --name rabbitmq -p 5672:5672 -p 15672:15672 rabbitmq:management
# Management UI: http://localhost:15672 (guest/guest)

dotnet add package MassTransit
dotnet add package MassTransit.RabbitMQ
```

```csharp
// ===== Message Contracts =====
// สร้าง shared library สำหรับ contracts
public record OrderCreated(int OrderId, int UserId, decimal Total, DateTime CreatedAt);
public record OrderCompleted(int OrderId, DateTime CompletedAt);
public record SendEmailCommand(string To, string Subject, string Body);

// ===== Publisher (Order Service) =====
public class OrderService : IOrderService
{
    private readonly AppDbContext _context;
    private readonly IPublishEndpoint _publishEndpoint;

    public OrderService(AppDbContext context, IPublishEndpoint publishEndpoint)
    {
        _context = context;
        _publishEndpoint = publishEndpoint;
    }

    public async Task<OrderDto> CreateOrderAsync(CreateOrderRequest request)
    {
        var order = new Order { UserId = request.UserId, Total = request.Total };
        _context.Orders.Add(order);
        await _context.SaveChangesAsync();

        // Publish event → ใครก็ได้ที่สนใจจะได้รับ
        await _publishEndpoint.Publish(new OrderCreated(
            order.Id, order.UserId, order.Total, DateTime.UtcNow));

        return MapToDto(order);
    }
}

// ===== Consumer (Email Service) =====
public class OrderCreatedConsumer : IConsumer<OrderCreated>
{
    private readonly IEmailService _emailService;
    private readonly ILogger<OrderCreatedConsumer> _logger;

    public OrderCreatedConsumer(IEmailService emailService, ILogger<OrderCreatedConsumer> logger)
    {
        _emailService = emailService;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<OrderCreated> context)
    {
        var message = context.Message;
        _logger.LogInformation("Processing OrderCreated: {OrderId}", message.OrderId);

        await _emailService.SendAsync(
            "user@test.com",
            $"Order #{message.OrderId} Confirmed",
            $"Your order for {message.Total:C} has been received.");
    }
}

// ===== Registration (Program.cs) =====
builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<OrderCreatedConsumer>();

    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host("localhost", "/", h =>
        {
            h.Username("guest");
            h.Password("guest");
        });

        cfg.ConfigureEndpoints(context);
    });
});
```

---

## 3. Apache Kafka (High-Throughput)

```csharp
// ใช้ MassTransit เหมือนกัน เปลี่ยนแค่ transport
// dotnet add package MassTransit.Kafka

builder.Services.AddMassTransit(x =>
{
    x.UsingInMemory();  // internal bus

    x.AddRider(rider =>
    {
        rider.AddConsumer<OrderCreatedConsumer>();

        rider.UsingKafka((context, k) =>
        {
            k.Host("localhost:9092");

            k.TopicEndpoint<OrderCreated>("order-created", "my-group", e =>
            {
                e.ConfigureConsumer<OrderCreatedConsumer>(context);
            });
        });
    });
});

// Kafka vs RabbitMQ:
// RabbitMQ → message queue, ดีสำหรับ task distribution
// Kafka    → event streaming, ดีสำหรับ high-throughput, event log
```

---

## 4. API Gateway (Ocelot / YARP)

```csharp
// YARP (Yet Another Reverse Proxy) — Microsoft official
// dotnet add package Yarp.ReverseProxy

// appsettings.json
{
  "ReverseProxy": {
    "Routes": {
      "users-route": {
        "ClusterId": "users-cluster",
        "Match": { "Path": "/api/users/{**catch-all}" }
      },
      "orders-route": {
        "ClusterId": "orders-cluster",
        "Match": { "Path": "/api/orders/{**catch-all}" }
      }
    },
    "Clusters": {
      "users-cluster": {
        "Destinations": {
          "destination1": { "Address": "http://localhost:5001/" }
        }
      },
      "orders-cluster": {
        "Destinations": {
          "destination1": { "Address": "http://localhost:5002/" }
        }
      }
    }
  }
}

// Program.cs
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

app.MapReverseProxy();

// Client เรียก Gateway → Gateway route ไป service ที่ถูกต้อง
// Client → Gateway:8080/api/users → User Service:5001/api/users
// Client → Gateway:8080/api/orders → Order Service:5002/api/orders
```

---

## 5. Microservices Patterns

```
1. API Gateway          → Single entry point สำหรับ clients
2. Service Discovery    → Services หากันเจอ (Consul, Eureka)
3. Circuit Breaker      → หยุดเรียก service ที่พัง (Polly)
4. Saga Pattern         → Distributed transactions
5. Event Sourcing       → เก็บทุก event แทนเก็บ state
6. CQRS                 → แยก Read/Write models

เริ่มต้น:
1. Monolith ก่อน → ออกแบบ modules ให้ดี
2. แตก service ที่ scale แยก หรือ deploy แยกจริงๆ
3. ใช้ message broker สื่อสาร (async)
4. ใช้ API Gateway เป็น entry point
```
