# API Clients & Communication — REST, GraphQL, gRPC, OData

> วิธีสร้างและเรียก API หลายรูปแบบใน .NET

---

## 1. REST API Client (HttpClient)

```csharp
// ===== Typed HttpClient (แนะนำ!) =====
// Program.cs
builder.Services.AddHttpClient<IPaymentClient, PaymentClient>(client =>
{
    client.BaseAddress = new Uri("https://api.stripe.com/v1/");
    client.DefaultRequestHeaders.Add("Authorization", $"Bearer {config["Stripe:Key"]}");
    client.Timeout = TimeSpan.FromSeconds(30);
});

// PaymentClient.cs
public class PaymentClient : IPaymentClient
{
    private readonly HttpClient _http;

    public PaymentClient(HttpClient http) => _http = http;

    public async Task<PaymentResult> ChargeAsync(ChargeRequest request)
    {
        var response = await _http.PostAsJsonAsync("charges", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<PaymentResult>()
            ?? throw new Exception("Failed to parse response");
    }

    public async Task<PaymentResult?> GetChargeAsync(string chargeId)
    {
        return await _http.GetFromJsonAsync<PaymentResult>($"charges/{chargeId}");
    }
}

// ===== Resilience ด้วย Polly =====
// dotnet add package Microsoft.Extensions.Http.Polly
builder.Services.AddHttpClient<IPaymentClient, PaymentClient>()
    .AddTransientHttpErrorPolicy(p => p.WaitAndRetryAsync(3,          // retry 3 ครั้ง
        attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt))))        // exponential backoff
    .AddTransientHttpErrorPolicy(p => p.CircuitBreakerAsync(5,        // หลัง fail 5 ครั้ง
        TimeSpan.FromSeconds(30)));                                     // หยุดเรียก 30 วินาที
```

---

## 2. gRPC — High-Performance RPC

```
gRPC คืออะไร:
- Google Remote Procedure Call
- ใช้ Protocol Buffers (binary format — เร็วกว่า JSON 5-10x)
- HTTP/2 — multiplexing, streaming
- เหมาะกับ: microservice ↔ microservice communication
```

```protobuf
// Protos/user.proto — กำหนด service contract
syntax = "proto3";

option csharp_namespace = "MyApi.Grpc";

service UserService {
  rpc GetUser (GetUserRequest) returns (UserResponse);
  rpc GetUsers (GetUsersRequest) returns (stream UserResponse);  // server streaming
  rpc CreateUser (CreateUserRequest) returns (UserResponse);
}

message GetUserRequest {
  int32 id = 1;
}

message GetUsersRequest {
  int32 page = 1;
  int32 page_size = 2;
}

message CreateUserRequest {
  string first_name = 1;
  string last_name = 2;
  string email = 3;
}

message UserResponse {
  int32 id = 1;
  string first_name = 2;
  string last_name = 3;
  string email = 4;
  string created_at = 5;
}
```

```csharp
// ===== gRPC Server =====
// dotnet add package Grpc.AspNetCore

public class UserGrpcService : UserService.UserServiceBase
{
    private readonly IUserService _userService;

    public UserGrpcService(IUserService userService) => _userService = userService;

    public override async Task<UserResponse> GetUser(GetUserRequest request, ServerCallContext context)
    {
        var user = await _userService.GetByIdAsync(request.Id);
        return new UserResponse
        {
            Id = user.Id,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Email = user.Email,
            CreatedAt = user.CreatedAt.ToString("o")
        };
    }
}

// Program.cs
builder.Services.AddGrpc();
app.MapGrpcService<UserGrpcService>();

// ===== gRPC Client =====
// dotnet add package Grpc.Net.Client
var channel = GrpcChannel.ForAddress("https://localhost:5001");
var client = new UserService.UserServiceClient(channel);
var reply = await client.GetUserAsync(new GetUserRequest { Id = 1 });
```

---

## 3. GraphQL — Flexible Queries

```csharp
// dotnet add package HotChocolate.AspNetCore

// ===== Query Type =====
public class Query
{
    [UseProjection]
    [UseFiltering]
    [UseSorting]
    public IQueryable<User> GetUsers([Service] AppDbContext context)
        => context.Users;

    public async Task<User?> GetUserById(int id, [Service] AppDbContext context)
        => await context.Users.FindAsync(id);
}

public class Mutation
{
    public async Task<User> CreateUser(CreateUserInput input, [Service] AppDbContext context)
    {
        var user = new User { FirstName = input.FirstName, Email = input.Email };
        context.Users.Add(user);
        await context.SaveChangesAsync();
        return user;
    }
}

// Program.cs
builder.Services
    .AddGraphQLServer()
    .AddQueryType<Query>()
    .AddMutationType<Mutation>()
    .AddProjections()
    .AddFiltering()
    .AddSorting();

app.MapGraphQL();  // → /graphql

// Client query:
// query { users(where: { isActive: { eq: true }}) { id firstName email orders { total } } }
// → ดึงเฉพาะ fields ที่ต้องการ! (ไม่ over-fetch เหมือน REST)
```

---

## 4. OData — Queryable REST API

```csharp
// dotnet add package Microsoft.AspNetCore.OData

// Program.cs
var modelBuilder = new ODataConventionModelBuilder();
modelBuilder.EntitySet<Product>("Products");

builder.Services.AddControllers().AddOData(options =>
    options.Select().Filter().OrderBy().Count().Expand().SetMaxTop(100)
        .AddRouteComponents("odata", modelBuilder.GetEdmModel()));

// Controller
[EnableQuery]
[HttpGet]
public IQueryable<Product> Get() => _context.Products;

// Client สามารถ query ผ่าน URL:
// GET /odata/Products?$filter=Price gt 100&$orderby=Name&$top=10&$select=Id,Name,Price
// GET /odata/Products?$expand=Category&$count=true
```

---

## 5. เมื่อไหร่ใช้อะไร

```
| Protocol  | ใช้เมื่อ                              | ตัวอย่าง                    |
|-----------|--------------------------------------|-----------------------------|
| REST      | Public API, CRUD ทั่วไป               | Web app ↔ API              |
| gRPC      | Service ↔ Service (internal)         | Microservice communication  |
| GraphQL   | Client ต้องการ flexible queries       | Mobile app, Dashboard       |
| OData     | ต้องการ queryable REST                | Admin panels, reporting     |

Enterprise API ทั่วไป:
- REST เป็นหลัก (90%)
- gRPC สำหรับ internal services (ถ้ามี microservices)
- GraphQL/OData สำหรับ dashboard/reporting (ถ้าต้องการ)
```
