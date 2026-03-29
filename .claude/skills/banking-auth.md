---
name: Banking Authentication
description: Setup และ extend ระบบ JWT Authentication สำหรับ Banking System รวม refresh token, password hashing, account lockout
command: bank-auth
argument-hint: "[feature: jwt|refresh|lockout|register|login|full-setup]"
allowed-tools:
  - Read
  - Write
  - Edit
  - Glob
  - Bash
  - Grep
---

# Banking Authentication Skill

คุณคือผู้เชี่ยวชาญ ASP.NET Core Authentication/Authorization สำหรับระบบ Banking ที่ต้องการความปลอดภัยสูง

## Input
- **argument:** feature ที่ต้องการ (optional, default: `full-setup`)
  - `full-setup` — setup ทั้งระบบตั้งแต่ต้น
  - `jwt` — JWT token generation + validation
  - `refresh` — Refresh token mechanism
  - `lockout` — Account lockout หลัง failed attempts
  - `register` — User registration + KYC
  - `login` — Login flow

## Project Context
- **Password hashing:** ใช้ BCrypt.Net-Next (มีอยู่แล้วใน Infrastructure)
- **JWT package:** Microsoft.AspNetCore.Authentication.JwtBearer (มีอยู่แล้วใน Api)
- **User entity:** มี PasswordHash, IsLocked, FailedLoginAttempts, LastLoginAt
- **KYC:** มี KycStatus enum (Pending/Verified/Rejected)

## ขั้นตอนการทำงาน

### Step 1: อ่าน Context
1. อ่าน `Banking.Domain/Entities/User.cs` — ดูโครงสร้าง user
2. อ่าน `Banking.Domain/Interfaces/IRepositories.cs` — ดู IUserRepository
3. อ่าน `Banking.Api/Program.cs` — ดู current service registration
4. อ่าน `Banking.Api/appsettings.json` — ดู JWT config
5. อ่าน `Banking.Infrastructure/Seeds/DataSeeder.cs` — ดู BCrypt usage

### Step 2: สร้าง JWT Configuration
**ไฟล์:** `Banking.Application/Auth/Services/IJwtService.cs` + `Banking.Infrastructure/Services/JwtService.cs`
- GenerateAccessToken(User user) → string (short-lived: 15 min)
- GenerateRefreshToken() → string (long-lived: 7 days)
- ValidateToken(string token) → ClaimsPrincipal?
- GetUserIdFromToken(string token) → Guid

**JWT Claims ที่ต้องมี:**
- `sub` = UserId
- `email` = Email
- `name` = FullName
- `role` = User role (Admin/Customer)
- `jti` = unique token ID (สำหรับ blacklist)

### Step 3: สร้าง Auth Commands/Handlers

**Register Flow:**
1. Validate input (email unique, phone unique, password strength)
2. Hash password ด้วย BCrypt
3. Hash national ID ถ้ามี
4. สร้าง User (KycStatus = Pending)
5. สร้าง default Savings account
6. Generate JWT + Refresh Token
7. Return tokens + user info

**Login Flow:**
1. Find user by email
2. Check IsLocked → throw AccountLockedException
3. Verify password ด้วย BCrypt
4. ถ้า fail → increment FailedLoginAttempts
5. ถ้า FailedLoginAttempts >= 5 → set IsLocked = true
6. ถ้า success → reset FailedLoginAttempts, update LastLoginAt
7. Generate JWT + Refresh Token
8. Return tokens + user info

**Refresh Token Flow:**
1. Validate refresh token (not expired, not blacklisted)
2. Extract user ID
3. Generate new JWT + new Refresh Token
4. Blacklist old refresh token (Redis)
5. Return new tokens

**Logout Flow:**
1. Get JTI from current token
2. Add to Redis blacklist: `blacklist:{jti}` with TTL = token remaining lifetime
3. Remove refresh token

### Step 4: Register JWT ใน Program.cs
```csharp
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!))
        };
    });
```

### Step 5: appsettings.json
```json
{
  "Jwt": {
    "Key": "your-super-secret-key-at-least-32-characters-long",
    "Issuer": "banking-api",
    "Audience": "banking-frontend",
    "AccessTokenExpirationMinutes": 15,
    "RefreshTokenExpirationDays": 7
  }
}
```

### Step 6: Security Checklist
- [ ] Password hashing ด้วย BCrypt (cost factor >= 11)
- [ ] JWT key >= 256 bits
- [ ] Access token short-lived (15 min)
- [ ] Refresh token rotation (ออกใหม่ทุกครั้งที่ refresh)
- [ ] Token blacklist ใน Redis
- [ ] Account lockout หลัง 5 failed attempts
- [ ] Rate limiting บน auth endpoints
- [ ] ไม่ return error message ที่ระบุว่า email มีอยู่หรือไม่ (ป้องกัน enumeration)

## ตัวอย่างการใช้งาน
```
/bank-auth full-setup    → setup ระบบ auth ทั้งหมด
/bank-auth jwt           → สร้าง JWT service เท่านั้น
/bank-auth login         → สร้าง login flow เท่านั้น
```
