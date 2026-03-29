---
name: Banking Security Hardening
description: Security audit และ hardening สำหรับ Banking System — rate limiting, fraud detection, audit logging, input validation
command: bank-security
argument-hint: "<feature: audit|rate-limit|fraud|validation|middleware|checklist>"
allowed-tools:
  - Read
  - Write
  - Edit
  - Glob
  - Bash
  - Grep
---

# Banking Security Hardening Skill

คุณคือผู้เชี่ยวชาญ Application Security สำหรับระบบ Banking ที่ต้องผ่านมาตรฐานความปลอดภัยระดับ financial-grade

## Input
- **argument:** feature ที่ต้องการ (required)
  - `audit` — Audit logging middleware (ทุก action บันทึก)
  - `rate-limit` — Rate limiting middleware + per-endpoint config
  - `fraud` — Fraud detection (unusual amounts, frequency, patterns)
  - `validation` — Input validation + security headers
  - `middleware` — Exception handling + security middleware stack
  - `checklist` — Security audit checklist (scan existing code)

## ขั้นตอนการทำงาน

### Step 1: อ่าน Context
1. อ่าน `Banking.Domain/Entities/AuditLog.cs` — audit log entity
2. อ่าน `Banking.Api/Program.cs` — current middleware pipeline
3. อ่าน `Banking.Api/Middleware/` — existing middleware
4. อ่าน `Banking.Infrastructure/Data/AppDbContext.cs` — SaveChanges override
5. Scan ทุก Controller หา security gaps

### Step 2: Audit Logging

**Automatic Audit via DbContext:**
```csharp
// Override SaveChangesAsync in AppDbContext
// Track: EntityType, EntityId, Action (Create/Update/Delete)
// Capture: OldValues, NewValues as JSONB
// Include: UserId, IpAddress, UserAgent from ICurrentUser
```

**Audit Middleware:**
```csharp
public class AuditMiddleware
{
    // Log every request: endpoint, method, userId, IP, timestamp
    // Log response status code
    // ห้าม log sensitive data (passwords, tokens, account numbers in full)
    // Mask: account number → "****-****-9012"
}
```

### Step 3: Rate Limiting

**Per-Endpoint Configuration:**
```
/api/auth/login        → 5 requests / minute (ป้องกัน brute force)
/api/auth/register     → 3 requests / hour (ป้องกัน spam)
/api/transactions/*    → 10 requests / minute (ป้องกัน abuse)
/api/accounts/*        → 30 requests / minute (general)
/api/admin/*           → 60 requests / minute (admin needs more)
```

**Implementation:** ASP.NET Core Rate Limiting middleware (.NET 7+)
```csharp
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("transaction", context =>
        RateLimitPartition.GetSlidingWindowLimiter(
            context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "anonymous",
            _ => new SlidingWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                SegmentsPerWindow = 6
            }));
});
```

### Step 4: Fraud Detection

**Rules-based Detection:**
```
1. Large Transaction Alert:
   - Single transaction > 100,000 THB → flag
   - Daily total > 500,000 THB → flag

2. Frequency Alert:
   - > 5 transactions in 5 minutes → flag
   - Transactions at unusual hours (midnight-5am) → flag

3. Pattern Alert:
   - Multiple failed transactions → flag
   - Rapid fire small transactions (structuring) → flag
   - New account + large transfer within 24hrs → flag

4. Velocity Alert:
   - Balance drops > 80% in single day → flag
   - Multiple transfers to same destination → flag
```

**Implementation:**
- Background service ที่ check transaction patterns
- Flag suspicious transactions ใน Transaction.Metadata (JSON)
- Notify admin via SignalR / email

### Step 5: Input Validation & Security Headers

**Security Headers Middleware:**
```csharp
app.Use(async (context, next) =>
{
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Append("X-Frame-Options", "DENY");
    context.Response.Headers.Append("X-XSS-Protection", "0");
    context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
    context.Response.Headers.Append("Content-Security-Policy", "default-src 'self'");
    context.Response.Headers.Append("Strict-Transport-Security", "max-age=31536000; includeSubDomains");
    await next();
});
```

**Input Validation Rules:**
- ทุก string input: trim, max length, no HTML/script injection
- Amount: decimal precision(18,2), range(0.01, 1,000,000)
- Account number format: regex validation
- Email: proper format + lowercase
- Phone: E.164 format validation

### Step 6: Exception Handling Middleware
```csharp
public class ExceptionMiddleware
{
    // Catch domain exceptions → appropriate HTTP status
    // InsufficientFundsException → 400 Bad Request
    // AccountFrozenException → 403 Forbidden
    // NotFoundException → 404 Not Found
    // DailyLimitExceededException → 400 Bad Request
    // Unhandled → 500 Internal Server Error (ห้าม leak stack trace ใน production)
    // Return ProblemDetails format (RFC 7807)
}
```

### Step 7: Security Checklist (สำหรับ `checklist` option)
Scan codebase และ report:
```
Authentication & Authorization:
☐ JWT + Refresh Token implemented
☐ PIN verification สำหรับ financial transactions
☐ Account lockout หลัง 5 failed attempts
☐ Token blacklist ใน Redis

Data Protection:
☐ No plain text passwords/PINs in code or logs
☐ National ID hashed
☐ Account numbers masked in logs
☐ Sensitive data encrypted at rest

Transaction Security:
☐ Distributed lock ป้องกัน double-spending
☐ Idempotency key ป้องกัน duplicate transactions
☐ Rate limiting per user per endpoint
☐ Daily transaction limits enforced

Infrastructure:
☐ Security headers configured
☐ CORS properly configured
☐ HTTPS enforced
☐ No sensitive data in error responses
☐ Audit logging enabled
```

## ตัวอย่างการใช้งาน
```
/bank-security audit       → implement audit logging
/bank-security rate-limit  → implement rate limiting
/bank-security fraud       → implement fraud detection
/bank-security middleware  → implement security middleware stack
/bank-security checklist   → scan และ report security status
```
