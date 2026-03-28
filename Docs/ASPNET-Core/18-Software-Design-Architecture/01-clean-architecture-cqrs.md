# Software Design & Architecture — Clean Architecture, CQRS

> ออกแบบ Enterprise API ที่ maintainable, testable, scalable

---

## 1. Clean Architecture

```
┌──────────────────────────────────────────────┐
│           Presentation Layer                  │  ← Controllers, Minimal APIs
│              (MyApi.Api)                      │
├──────────────────────────────────────────────┤
│           Application Layer                   │  ← Use Cases, DTOs, Interfaces
│          (MyApi.Application)                  │
├──────────────────────────────────────────────┤
│            Domain Layer                       │  ← Entities, Value Objects, Domain Events
│            (MyApi.Domain)                     │
├──────────────────────────────────────────────┤
│         Infrastructure Layer                  │  ← EF Core, Redis, Email, External APIs
│        (MyApi.Infrastructure)                 │
└──────────────────────────────────────────────┘

กฎ: Dependencies ชี้เข้าใน → Domain ไม่รู้จักอะไรข้างนอก
Api → Application → Domain ← Infrastructure
```

### โครงสร้าง Solution

```
MyApi.sln
├── src/
│   ├── MyApi.Domain/                    ← Entities, Interfaces, Exceptions
│   │   ├── Entities/
│   │   │   ├── User.cs
│   │   │   ├── Order.cs
│   │   │   └── BaseEntity.cs
│   │   ├── Interfaces/
│   │   │   ├── IUserRepository.cs
│   │   │   └── IUnitOfWork.cs
│   │   ├── Exceptions/
│   │   │   ├── NotFoundException.cs
│   │   │   └── ConflictException.cs
│   │   └── Events/
│   │       └── OrderCreatedEvent.cs
│   │
│   ├── MyApi.Application/              ← Use Cases, DTOs, Validators
│   │   ├── Users/
│   │   │   ├── Commands/
│   │   │   │   ├── CreateUser/
│   │   │   │   │   ├── CreateUserCommand.cs
│   │   │   │   │   ├── CreateUserCommandHandler.cs
│   │   │   │   │   └── CreateUserCommandValidator.cs
│   │   │   │   └── UpdateUser/
│   │   │   │       └── ...
│   │   │   └── Queries/
│   │   │       ├── GetUserById/
│   │   │       │   ├── GetUserByIdQuery.cs
│   │   │       │   └── GetUserByIdQueryHandler.cs
│   │   │       └── GetUsers/
│   │   │           └── ...
│   │   ├── Common/
│   │   │   ├── DTOs/
│   │   │   ├── Interfaces/
│   │   │   │   └── IApplicationDbContext.cs
│   │   │   └── Behaviors/
│   │   │       ├── ValidationBehavior.cs
│   │   │       └── LoggingBehavior.cs
│   │   └── DependencyInjection.cs
│   │
│   ├── MyApi.Infrastructure/           ← Implementations
│   │   ├── Data/
│   │   │   ├── AppDbContext.cs
│   │   │   ├── Configurations/
│   │   │   └── Repositories/
│   │   ├── Services/
│   │   │   ├── EmailService.cs
│   │   │   └── CacheService.cs
│   │   └── DependencyInjection.cs
│   │
│   └── MyApi.Api/                      ← Controllers, Middleware
│       ├── Controllers/
│       ├── Middleware/
│       └── Program.cs
│
└── tests/
    ├── MyApi.Tests.Unit/
    └── MyApi.Tests.Integration/
```

---

## 2. CQRS + MediatR

```
CQRS = Command Query Responsibility Segregation
แยก Read (Query) กับ Write (Command) ออกจากกัน

Command: สร้าง/แก้/ลบ → return void หรือ Id
Query:   อ่าน → return data

MediatR = library ที่ช่วย implement CQRS
Request → MediatR → Handler → Response
```

```bash
dotnet add package MediatR
```

```csharp
// ===== Command =====
// Application/Users/Commands/CreateUser/CreateUserCommand.cs
public record CreateUserCommand(
    string FirstName,
    string LastName,
    string Email,
    string Password
) : IRequest<int>;  // return User Id

// Application/Users/Commands/CreateUser/CreateUserCommandHandler.cs
public class CreateUserCommandHandler : IRequestHandler<CreateUserCommand, int>
{
    private readonly IApplicationDbContext _context;

    public CreateUserCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<int> Handle(CreateUserCommand request, CancellationToken cancellationToken)
    {
        if (await _context.Users.AnyAsync(u => u.Email == request.Email, cancellationToken))
            throw new ConflictException($"Email '{request.Email}' already exists");

        var user = new User
        {
            FirstName = request.FirstName,
            LastName = request.LastName,
            Email = request.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password)
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync(cancellationToken);

        return user.Id;
    }
}

// ===== Query =====
// Application/Users/Queries/GetUserById/GetUserByIdQuery.cs
public record GetUserByIdQuery(int Id) : IRequest<UserDto>;

public class GetUserByIdQueryHandler : IRequestHandler<GetUserByIdQuery, UserDto>
{
    private readonly IApplicationDbContext _context;

    public GetUserByIdQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<UserDto> Handle(GetUserByIdQuery request, CancellationToken cancellationToken)
    {
        var user = await _context.Users
            .AsNoTracking()
            .Where(u => u.Id == request.Id)
            .Select(u => new UserDto(u.Id, u.FirstName, u.LastName, u.Email,
                u.Role, u.IsActive, u.CreatedAt))
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("User", request.Id);

        return user;
    }
}

// ===== Controller (บางมาก!) =====
[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly ISender _mediator;

    public UsersController(ISender mediator) => _mediator = mediator;

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
        => Ok(await _mediator.Send(new GetUserByIdQuery(id)));

    [HttpPost]
    public async Task<IActionResult> Create(CreateUserCommand command)
    {
        var id = await _mediator.Send(command);
        return CreatedAtAction(nameof(GetById), new { id }, null);
    }
}

// ===== MediatR Pipeline Behaviors =====
// Validation อัตโนมัติก่อนถึง Handler
public class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
        => _validators = validators;

    public async Task<TResponse> Handle(TRequest request,
        RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (!_validators.Any()) return await next();

        var context = new ValidationContext<TRequest>(request);
        var failures = (await Task.WhenAll(
            _validators.Select(v => v.ValidateAsync(context, cancellationToken))))
            .SelectMany(r => r.Errors)
            .Where(f => f is not null)
            .ToList();

        if (failures.Count > 0)
            throw new ValidationException(failures);

        return await next();
    }
}

// ===== Registration =====
// Application/DependencyInjection.cs
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly);
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        });

        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);

        return services;
    }
}

// Program.cs
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
```

---

## 3. สรุป Architecture

```
เลือก Architecture ตาม Project Size:

Small (1-3 developers, MVP):
→ Simple layered: Controllers → Services → DbContext
→ ไม่ต้อง CQRS, ไม่ต้อง Clean Architecture

Medium (3-10 developers):
→ Clean Architecture + basic folder structure
→ Services แยก interface
→ อาจใช้ MediatR

Large (10+ developers, Enterprise):
→ Clean Architecture + CQRS + MediatR
→ Domain-Driven Design
→ Microservices (ถ้าจำเป็น)

กฎทอง: อย่า over-engineer!
เริ่มง่าย → เพิ่ม complexity เมื่อจำเป็นจริงๆ
```
