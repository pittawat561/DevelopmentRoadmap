# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build & Run Commands

```bash
# Build entire solution
dotnet build

# Run the API (default: https://localhost:7001)
dotnet run --project Banking.Api

# Run with specific environment
ASPNETCORE_ENVIRONMENT=Development dotnet run --project Banking.Api

# Run all tests
dotnet test

# Run unit tests only
dotnet test Banking.Tests.Unit

# Run integration tests only
dotnet test Banking.Tests.Integration

# Run a single test by name
dotnet test Banking.Tests.Unit --filter "FullyQualifiedName~MethodName"

# Add EF Core migration
dotnet ef migrations add MigrationName --project Banking.Infrastructure --startup-project Banking.Api

# Apply migrations
dotnet ef database update --project Banking.Infrastructure --startup-project Banking.Api
```

## Prerequisites

- .NET 10.0 SDK
- PostgreSQL (database: `banking_db` / `banking_dev` / `banking_debug` depending on environment)
- Redis on localhost:6379 (for cache, distributed locks, rate limiting, SignalR backplane)
- Auto-migration runs on startup in Debug/Development environments

## Architecture

Clean Architecture with 4 layers. Dependency flows inward: Api → Application → Domain ← Infrastructure.

```
Banking.Api              → Controllers, Middleware, SignalR Hubs, Program.cs
Banking.Application      → Services, DTOs, Validators, Interfaces (IRedisCacheService, INotificationService)
Banking.Domain           → Entities, Enums, Exceptions, Repository Interfaces (zero dependencies)
Banking.Infrastructure   → EF Core DbContext, Repository implementations, JwtService, RedisCacheService
```

**Key patterns:**
- **Repository + Unit of Work** — `IUnitOfWork` coordinates `IUserRepository`, `IAccountRepository`, `ITransactionRepository` with explicit DB transaction control (Begin/Commit/Rollback)
- **Soft Delete** — All entities inherit `BaseEntity` with `IsDeleted` flag; global QueryFilter applied in `AppDbContext`
- **Row-Level Locking** — `GetByIdForUpdateAsync()` uses raw SQL `SELECT ... FOR UPDATE` to prevent race conditions on balance updates
- **Distributed Locking** — Redis `SET NX` locks on account IDs before transactions; Lua script for safe unlock
- **ACID Transactions** — Every deposit/withdraw/transfer wraps DB operations in a transaction with rollback on failure

## Domain Model

Entities: `User` → `Account` (1:N) → `Transaction` (1:N), `Transfer` links two accounts with debit/credit transaction pair, `AuditLog` (separate, uses `long` auto-increment ID).

Enums: `AccountType` (Savings/Checking/FixedDeposit), `AccountStatus` (Active/Frozen/Closed), `TransactionType` (Deposit/Withdrawal/TransferIn/TransferOut/Fee/Interest), `TransactionStatus` (Pending/Processing/Completed/Failed/Reversed), `KycStatus` (Pending/Verified/Rejected).

Custom exceptions in `Banking.Domain.Exceptions`: `NotFoundException`, `InsufficientFundsException`, `AccountFrozenException`, `DailyLimitExceededException`, `DuplicateException`, `AccountLockedException`. These are caught by `ExceptionMiddleware` and mapped to HTTP status codes.

## API Endpoints

- `POST /api/auth/register|login`, `GET /api/auth/profile` [Authorize], `POST /api/auth/logout` [Authorize]
- `GET /api/accounts?userId=`, `GET /api/accounts/{id}`, `GET /api/accounts/{id}/balance` (Redis-cached), `POST /api/accounts`
- `POST /api/transactions/deposit|withdraw|transfer`, `GET /api/transactions?accountId=&page=&pageSize=`
- `GET /api/admin/dashboard`, `POST /api/admin/accounts/{id}/freeze|unfreeze`, `POST /api/admin/users/{id}/unlock`
- SignalR Hub: `/hubs/notifications` (events: `BalanceUpdated`, `TransactionCompleted`)

## Middleware Pipeline Order

ExceptionMiddleware → HttpsRedirection → CORS → Authentication → TokenBlacklistMiddleware → RateLimitMiddleware → Authorization → MapControllers + MapHub

## Key Conventions

- All monetary values use `decimal(18,2)`
- Timestamps are always UTC (`DateTime.UtcNow`)
- Account numbers format: `XXXX-XXXX-XXXX`
- Transaction reference format: `TXN-YYYYMMDD-XXXXXX`
- API responses wrapped in `ApiResponse<T>` record: `{ success, message, data }`
- Paginated responses use `PagedResponse<T>`: `{ items, totalCount, page, pageSize, totalPages }`
- DTOs are C# records (immutable)
- Validators use FluentValidation, registered via `AddValidatorsFromAssemblyContaining<>`
- JWT access tokens expire in 15 minutes; refresh tokens in 7 days

## Testing Stack

- xUnit + FluentAssertions + Moq + Bogus (fake data) + Respawn (DB cleanup)
- Unit tests reference Application + Domain layers
- Integration tests reference Api project (WebApplicationFactory)

## Configuration

Environment-specific settings in `appsettings.{Environment}.json`. Swagger enabled via `Swagger:Enabled` config key. Redis config under `Redis:` section (InstanceName prefix, cache TTLs, rate limit thresholds). JWT config under `Jwt:` section.
