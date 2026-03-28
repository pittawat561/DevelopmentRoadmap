# Good-to-Know Libraries สำหรับ .NET Enterprise API

> NuGet packages ที่ใช้จริงในโปรเจกต์ Enterprise

---

## 1. FluentValidation — Input Validation

```csharp
// dotnet add package FluentValidation.AspNetCore

public class CreateOrderRequestValidator : AbstractValidator<CreateOrderRequest>
{
    public CreateOrderRequestValidator()
    {
        RuleFor(x => x.UserId).GreaterThan(0);
        RuleFor(x => x.Items).NotEmpty().WithMessage("ต้องมีสินค้าอย่างน้อย 1 รายการ");
        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.ProductId).GreaterThan(0);
            item.RuleFor(i => i.Quantity).InclusiveBetween(1, 100);
        });
    }
}
```

---

## 2. Polly — Resilience & Fault Handling

```csharp
// dotnet add package Microsoft.Extensions.Http.Resilience

// Retry + Circuit Breaker + Timeout ในบรรทัดเดียว
builder.Services.AddHttpClient<IExternalApi, ExternalApi>()
    .AddStandardResilienceHandler();

// Custom policy:
builder.Services.AddHttpClient<IPaymentApi, PaymentApi>()
    .AddResilienceHandler("payment", builder =>
    {
        builder.AddRetry(new HttpRetryStrategyOptions
        {
            MaxRetryAttempts = 3,
            Delay = TimeSpan.FromSeconds(1),
            BackoffType = DelayBackoffType.Exponential
        });
        builder.AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
        {
            FailureRatio = 0.5,
            SamplingDuration = TimeSpan.FromSeconds(10),
            BreakDuration = TimeSpan.FromSeconds(30)
        });
        builder.AddTimeout(TimeSpan.FromSeconds(5));
    });
```

---

## 3. MediatR — CQRS / Mediator Pattern

```csharp
// dotnet add package MediatR
// ดูรายละเอียดเต็มใน 18-Software-Design-Architecture

// ข้อดี:
// - Controller บาง (แค่ send command/query)
// - Pipeline behaviors (validation, logging, caching)
// - Decoupled architecture
```

---

## 4. AutoMapper / Mapperly — Object Mapping

```csharp
// ===== Mapperly (แนะนำ! — source generator, เร็วกว่า AutoMapper) =====
// dotnet add package Riok.Mapperly

[Mapper]
public partial class UserMapper
{
    public partial UserDto ToDto(User user);
    public partial List<UserDto> ToDtos(List<User> users);
    public partial User ToEntity(CreateUserRequest request);

    // Custom mapping
    [MapProperty(nameof(User.FirstName), nameof(UserDto.Name))]
    public partial UserDto ToCustomDto(User user);
}

// ใช้:
var mapper = new UserMapper();
var dto = mapper.ToDto(user);

// ===== AutoMapper (ยังนิยม) =====
// dotnet add package AutoMapper.Extensions.Microsoft.DependencyInjection

public class UserProfile : Profile
{
    public UserProfile()
    {
        CreateMap<User, UserDto>();
        CreateMap<CreateUserRequest, User>();
    }
}

// ใช้:
var dto = _mapper.Map<UserDto>(user);
```

---

## 5. Scalar / Swagger — API Documentation

```csharp
// ===== Scalar (แนะนำ .NET 9+) =====
// dotnet add package Scalar.AspNetCore
builder.Services.AddOpenApi();
app.MapOpenApi();
app.MapScalarApiReference();  // → /scalar/v1

// ===== Swashbuckle (Swagger — ยังใช้ได้) =====
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "My API", Version = "v1" });

    // JWT support ใน Swagger UI
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header. Example: 'Bearer {token}'",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});
```

---

## 6. Benchmark.NET — Performance Testing

```csharp
// dotnet add package BenchmarkDotNet

[MemoryDiagnoser]
public class SerializationBenchmarks
{
    private readonly User _user = new() { Id = 1, FirstName = "John", Email = "john@test.com" };

    [Benchmark(Baseline = true)]
    public string SystemTextJson() => JsonSerializer.Serialize(_user);

    [Benchmark]
    public string Newtonsoft() => JsonConvert.SerializeObject(_user);
}

// รัน: dotnet run -c Release
// ผลลัพธ์:
// |         Method |     Mean |  Allocated |
// |--------------- |---------:|-----------:|
// | SystemTextJson | 150.2 ns |      256 B |
// |     Newtonsoft | 450.7 ns |    1,024 B |
```

---

## 7. สรุป Libraries ที่ต้องมี

```
| Library              | ใช้ทำอะไร              | ความสำคัญ  |
|----------------------|----------------------|-----------|
| FluentValidation     | Input validation     | ⭐⭐⭐⭐⭐ |
| Serilog              | Structured logging   | ⭐⭐⭐⭐⭐ |
| MediatR              | CQRS, Decoupling     | ⭐⭐⭐⭐   |
| Polly                | Retry, Circuit break | ⭐⭐⭐⭐   |
| Mapperly/AutoMapper  | Object mapping       | ⭐⭐⭐⭐   |
| Scalar/Swagger       | API documentation    | ⭐⭐⭐⭐   |
| BCrypt.Net           | Password hashing     | ⭐⭐⭐⭐⭐ |
| Bogus                | Fake data for tests  | ⭐⭐⭐     |
| BenchmarkDotNet      | Performance testing  | ⭐⭐⭐     |
| Scrutor              | Auto DI registration | ⭐⭐⭐     |
```
