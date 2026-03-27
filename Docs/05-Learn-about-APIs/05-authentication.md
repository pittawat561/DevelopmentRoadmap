# Authentication - การยืนยันตัวตนทั้งหมด

> ครอบคลุมทุกวิธียืนยันตัวตน: JWT, OAuth, Basic, Token, Cookie, OpenID Connect, SAML

---

## 1. ภาพรวม Authentication

```
Authentication = พิสูจน์ว่า "คุณเป็นใคร"

วิธีต่างๆ:
┌─────────────────────────────────────────────────┐
│              Authentication Methods              │
├──────────────┬──────────────┬───────────────────┤
│  ง่าย        │  กลาง        │  ซับซ้อน           │
│  Basic Auth  │  Token Auth  │  OAuth 2.0        │
│  API Key     │  JWT         │  OpenID Connect   │
│              │  Session     │  SAML             │
│              │  Cookie Auth │                   │
└──────────────┴──────────────┴───────────────────┘
```

---

## 2. Basic Authentication

วิธีง่ายที่สุด — ส่ง username:password ทุก request

```
ขั้นตอน:
1. ต่อ username กับ password ด้วย :     → admin:password123
2. Encode ด้วย Base64                    → YWRtaW46cGFzc3dvcmQxMjM=
3. ใส่ใน Authorization header

GET /api/users
Authorization: Basic YWRtaW46cGFzc3dvcmQxMjM=
```

```
⚠️ ข้อควรระวัง:
- Base64 ไม่ใช่การเข้ารหัส ถอดกลับได้ทันที
- ต้องใช้ HTTPS เสมอ
- ส่ง password ทุก request = เสี่ยง

✅ เหมาะกับ:
- Internal API / เครื่องมือภายใน
- Development/Testing
- API ง่ายๆ ที่ไม่ sensitive

❌ ไม่เหมาะกับ:
- Production API สาธารณะ
- ระบบที่ต้อง security สูง
```

---

## 3. Token Based Authentication

ใช้ token สุ่ม (opaque token) แทน username/password

```
ขั้นตอน:
┌────────┐                          ┌────────┐
│ Client │                          │ Server │
└───┬────┘                          └───┬────┘
    │ 1. POST /login                    │
    │    {username, password}      ──→  │
    │                                   │ 2. ตรวจสอบ credentials
    │                                   │    สร้าง token สุ่ม
    │                                   │    เก็บ token ใน DB
    │  3. { token: "a1b2c3d4e5" }  ←── │
    │                                   │
    │ 4. GET /api/users                 │
    │    Authorization: Bearer a1b2c3.. │
    │                              ──→  │ 5. ค้นหา token ใน DB
    │                                   │    ดึงข้อมูลผู้ใช้
    │  6. { users: [...] }         ←── │
    │                                   │
    │ 7. POST /logout                   │
    │    Authorization: Bearer a1b2c3.. │
    │                              ──→  │ 8. ลบ token จาก DB
```

```
Token vs JWT:
- Token: string สุ่มไม่มีความหมาย → Server ต้องเก็บใน DB
- JWT: มีข้อมูลในตัวเอง → Server ไม่ต้องเก็บ (stateless)
```

---

## 4. JWT (JSON Web Token)

### โครงสร้าง JWT

```
eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.     ← Header
eyJzdWIiOiIxMjMiLCJuYW1lIjoiSm9obiIsInJ     ← Payload
vbGUiOiJhZG1pbiIsImV4cCI6MTcwMDAwMDAwMH0.
SflKxwRJSMeKKF2QT4fwpMeJf36POk6yJV_adQssw5c ← Signature

ทั้ง 3 ส่วนคั่นด้วยจุด (.)
```

#### Header
```json
{
  "alg": "HS256",    // Algorithm ที่ใช้ sign
  "typ": "JWT"       // ประเภท token
}
// Algorithms ที่ใช้บ่อย:
// HS256 — HMAC + SHA256 (symmetric key)
// RS256 — RSA + SHA256 (asymmetric key pair)
// ES256 — ECDSA + SHA256 (asymmetric, เล็กกว่า RSA)
```

#### Payload (Claims)
```json
{
  // Registered Claims (มาตรฐาน)
  "sub": "123",                    // Subject (user ID)
  "iss": "api.example.com",       // Issuer (ผู้ออก token)
  "aud": "app.example.com",       // Audience (ผู้รับ)
  "exp": 1700000000,              // Expiration (หมดอายุ)
  "iat": 1699996400,              // Issued At (เวลาออก)
  "nbf": 1699996400,              // Not Before (ใช้ได้หลัง)
  "jti": "unique-token-id",       // JWT ID (ป้องกันใช้ซ้ำ)

  // Custom Claims (กำหนดเอง)
  "name": "John Doe",
  "role": "admin",
  "permissions": ["read", "write", "delete"]
}
```

#### Signature
```
// สร้าง Signature
HMACSHA256(
  base64UrlEncode(header) + "." + base64UrlEncode(payload),
  secret_key
)

// Server ตรวจสอบโดย:
// 1. ถอด header + payload
// 2. สร้าง signature ใหม่ด้วย secret_key เดิม
// 3. เปรียบเทียบ signature → ตรง = ไม่ถูกแก้ไข
```

### Access Token + Refresh Token

```
ปัญหา: JWT หมดอายุแล้วต้อง login ใหม่ทุกครั้ง
แก้: ใช้ 2 tokens

Access Token:
- อายุสั้น (15 นาที - 1 ชั่วโมง)
- ใช้เรียก API

Refresh Token:
- อายุยาว (7 วัน - 30 วัน)
- ใช้ขอ Access Token ใหม่
- เก็บใน httpOnly cookie

ขั้นตอน:
1. Login → ได้ Access Token + Refresh Token
2. ใช้ Access Token เรียก API
3. Access Token หมดอายุ → ส่ง Refresh Token ขอ Access Token ใหม่
4. Refresh Token หมดอายุ → ต้อง Login ใหม่

POST /auth/login
→ { access_token: "...", refresh_token: "...", expires_in: 3600 }

POST /auth/refresh
{ refresh_token: "..." }
→ { access_token: "new-token...", expires_in: 3600 }

POST /auth/logout
→ เพิกถอน refresh token
```

### JWT Best Practices

```
✅ ทำ:
- ตั้ง exp สั้น (15-60 นาที)
- ใช้ HTTPS เสมอ
- ใช้ RS256 สำหรับ microservices (public key verify)
- Validate ทุก claim (exp, iss, aud)
- เก็บ refresh token ใน httpOnly cookie

❌ อย่าทำ:
- เก็บข้อมูล sensitive ใน payload (อ่านได้โดย decode)
- เก็บ JWT ใน localStorage (XSS attack)
- ใช้ secret key ง่ายเกินไป
- ไม่ตั้ง exp (token ไม่มีวันหมดอายุ)
```

---

## 5. OAuth 2.0

### OAuth 2.0 คืออะไร

Framework สำหรับให้สิทธิ์ (authorization) แอปอื่นเข้าถึงข้อมูลแทนผู้ใช้ **โดยไม่ต้องเปิดเผยรหัสผ่าน**

### ตัวละครใน OAuth 2.0

```
1. Resource Owner    = ผู้ใช้ (เจ้าของข้อมูล)
2. Client            = แอปของเรา (ที่ต้องการเข้าถึงข้อมูล)
3. Authorization Server = ระบบยืนยันตัวตน (เช่น Google, Facebook)
4. Resource Server   = ระบบเก็บข้อมูล (เช่น Google Drive, Facebook API)
```

### Grant Types

#### Authorization Code (แนะนำ — ปลอดภัยที่สุด)

```
สำหรับ: Web app ที่มี backend server

┌────────┐     ┌───────────┐     ┌─────────────┐     ┌──────────┐
│ ผู้ใช้  │     │  แอปเรา   │     │ Auth Server │     │ Resource │
│        │     │ (Client)  │     │ (Google)    │     │ Server   │
└───┬────┘     └─────┬─────┘     └──────┬──────┘     └────┬─────┘
    │                │                   │                  │
    │ 1. กด Login    │                   │                  │
    │───────────────→│                   │                  │
    │                │ 2. Redirect       │                  │
    │←───────────────│ ไป Google         │                  │
    │                                    │                  │
    │ 3. Login + อนุญาต                  │                  │
    │───────────────────────────────────→│                  │
    │                                    │                  │
    │ 4. Redirect กลับ + auth code       │                  │
    │←───────────────────────────────────│                  │
    │                │                   │                  │
    │                │ 5. แลก code       │                  │
    │                │ เป็น token        │                  │
    │                │──────────────────→│                  │
    │                │                   │                  │
    │                │ 6. Access Token   │                  │
    │                │←──────────────────│                  │
    │                │                   │                  │
    │                │ 7. ดึงข้อมูล + token                 │
    │                │─────────────────────────────────────→│
    │                │                                      │
    │                │ 8. ข้อมูลผู้ใช้                       │
    │                │←─────────────────────────────────────│
    │                │                   │                  │
    │ 9. แสดงข้อมูล  │                   │                  │
    │←───────────────│                   │                  │
```

```
// ขั้นตอนจริง:

// 2. Redirect ไป Google
https://accounts.google.com/o/oauth2/v2/auth?
  client_id=YOUR_CLIENT_ID
  &redirect_uri=https://yourapp.com/callback
  &response_type=code
  &scope=openid email profile
  &state=random_csrf_token

// 4. Google redirect กลับมาพร้อม code
https://yourapp.com/callback?code=AUTH_CODE_HERE&state=random_csrf_token

// 5. Backend แลก code เป็น token (server-to-server)
POST https://oauth2.googleapis.com/token
Content-Type: application/x-www-form-urlencoded

client_id=YOUR_CLIENT_ID
&client_secret=YOUR_CLIENT_SECRET
&code=AUTH_CODE_HERE
&grant_type=authorization_code
&redirect_uri=https://yourapp.com/callback

// 6. ได้ tokens
{
  "access_token": "ya29.a0AfH6SMB...",
  "refresh_token": "1//0gdB...",
  "expires_in": 3600,
  "token_type": "Bearer",
  "id_token": "eyJhbGci..."
}
```

#### Authorization Code + PKCE (สำหรับ Mobile/SPA)

```
PKCE (Proof Key for Code Exchange) — ป้องกัน code ถูกขโมย
สำหรับ: Mobile app, Single Page App (ไม่มี client_secret)

เพิ่มขั้นตอน:
1. Client สร้าง code_verifier (random string)
2. สร้าง code_challenge = SHA256(code_verifier)
3. ส่ง code_challenge ไปกับ auth request
4. แลก code + code_verifier → Server ตรวจสอบว่าตรงกัน

// Auth request เพิ่ม PKCE
?code_challenge=E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw
&code_challenge_method=S256

// Token request เพิ่ม code_verifier
&code_verifier=dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk
```

#### Client Credentials (Server-to-Server)

```
สำหรับ: Service คุยกับ service (ไม่มีผู้ใช้เกี่ยวข้อง)

POST /oauth/token
Content-Type: application/x-www-form-urlencoded

client_id=SERVICE_A_ID
&client_secret=SERVICE_A_SECRET
&grant_type=client_credentials
&scope=read:orders

// Response
{
  "access_token": "eyJhbGci...",
  "token_type": "Bearer",
  "expires_in": 3600
}
```

### Scopes (ขอบเขตสิทธิ์)

```
Scope กำหนดว่า token ทำอะไรได้บ้าง:

scope=openid          → ดึง user ID
scope=email           → ดึงอีเมล
scope=profile         → ดึงชื่อ, รูป
scope=read:users      → อ่านข้อมูลผู้ใช้
scope=write:users     → แก้ไขข้อมูลผู้ใช้

// หลักการ Least Privilege: ขอเฉพาะ scope ที่จำเป็น
```

---

## 6. Cookie Based Authentication (Session Auth)

### ขั้นตอนการทำงาน

```
┌────────┐                          ┌────────┐
│ Browser│                          │ Server │
└───┬────┘                          └───┬────┘
    │ 1. POST /login                    │
    │    {username, password}      ──→  │
    │                                   │ 2. สร้าง Session
    │                                   │    เก็บใน Memory/DB/Redis
    │                                   │    session_id → {user_id, role, ...}
    │ 3. Set-Cookie: sid=abc123    ←── │
    │    HttpOnly; Secure; SameSite     │
    │                                   │
    │ 4. GET /api/profile               │
    │    Cookie: sid=abc123 (อัตโนมัติ)  │
    │                              ──→  │ 5. ค้น session จาก sid
    │                                   │    ดึงข้อมูลผู้ใช้
    │ 6. { name: "John", ... }     ←── │
    │                                   │
    │ 7. POST /logout                   │
    │    Cookie: sid=abc123        ──→  │ 8. ลบ session
    │ 9. Set-Cookie: sid=; Max-Age=0 ←─│
```

### Session Storage

```
เก็บ Session ได้หลายที่:

1. In-Memory (เร็ว แต่หายเมื่อ restart)
   ใช้ตอน development

2. Database (PostgreSQL, MySQL)
   sessions table: id, user_id, data, expires_at

3. Redis (แนะนำ สำหรับ production)
   เร็ว, ตั้ง TTL หมดอายุอัตโนมัติ, share ข้าม server ได้

4. File System
   ไม่แนะนำ (ช้า, ไม่ share ข้าม server)
```

### Cookie Settings สำคัญ

```
Set-Cookie: session_id=abc123;
  HttpOnly;            ← JavaScript อ่านไม่ได้ (ป้องกัน XSS)
  Secure;              ← ส่งผ่าน HTTPS เท่านั้น
  SameSite=Lax;        ← ป้องกัน CSRF (Lax = ผ่อนปรน, Strict = เข้มงวด)
  Max-Age=86400;       ← หมดอายุใน 24 ชม.
  Path=/;              ← ส่งทุก path
  Domain=.example.com  ← ใช้ได้ทุก subdomain
```

### Cookie Auth vs JWT

```
| หัวข้อ           | Cookie/Session     | JWT              |
|------------------|--------------------|------------------|
| เก็บที่           | Server (session)   | Client (token)   |
| Stateful/less    | Stateful           | Stateless        |
| Scale            | ต้อง share session  | ง่าย              |
| เพิกถอน          | ลบ session ได้เลย   | ยาก (ต้องรอหมดอายุ)|
| CSRF             | เสี่ยง (ต้องป้องกัน) | ไม่เสี่ยง          |
| XSS              | ปลอดภัย (HttpOnly)  | เสี่ยงถ้าเก็บผิดที่  |
| Cross-domain     | ยาก                | ง่าย              |
| Mobile           | ไม่สะดวก           | สะดวก            |
| เหมาะกับ         | Web app แบบดั้งเดิม | API, SPA, Mobile |
```

---

## 7. OpenID Connect (OIDC)

### OIDC คืออะไร

**Identity layer** ที่สร้างทับ OAuth 2.0 — OAuth 2.0 ให้แค่ **authorization** (สิทธิ์เข้าถึง) แต่ OIDC เพิ่ม **authentication** (ยืนยันตัวตน) ให้ด้วย

```
OAuth 2.0 = "แอปนี้มีสิทธิ์เข้าถึงข้อมูลของคุณ"
OIDC      = "แอปนี้รู้ว่าคุณคือใคร" + สิทธิ์เข้าถึง

OAuth 2.0 → Access Token (เข้าถึงข้อมูล)
OIDC      → Access Token + ID Token (ข้อมูลตัวตน)
```

### ID Token

```json
// ID Token เป็น JWT ที่มีข้อมูลผู้ใช้
{
  "iss": "https://accounts.google.com",   // ผู้ออก
  "sub": "110169484474386276334",          // user ID (unique)
  "aud": "YOUR_CLIENT_ID",                // client ที่ขอ
  "exp": 1700000000,                      // หมดอายุ
  "iat": 1699996400,                      // เวลาออก
  "nonce": "random-nonce",                // ป้องกัน replay

  // ข้อมูลผู้ใช้ (claims)
  "name": "John Doe",
  "email": "john@gmail.com",
  "email_verified": true,
  "picture": "https://lh3.googleusercontent.com/a/photo.jpg",
  "locale": "th"
}
```

### OIDC Flow

```
เหมือน OAuth 2.0 Authorization Code แต่เพิ่ม:
1. scope ต้องมี "openid"
2. ได้ id_token กลับมาด้วย
3. มี UserInfo endpoint

// 1. Auth Request (เพิ่ม openid scope)
GET https://accounts.google.com/o/oauth2/v2/auth?
  response_type=code
  &client_id=YOUR_CLIENT_ID
  &scope=openid email profile
  &redirect_uri=https://yourapp.com/callback
  &nonce=random_nonce

// 2. Token Response (มี id_token)
{
  "access_token": "ya29...",
  "id_token": "eyJhbGci...",      ← ID Token (JWT)
  "token_type": "Bearer",
  "expires_in": 3600
}

// 3. UserInfo Endpoint (ดึงข้อมูลเพิ่มเติม)
GET https://openidconnect.googleapis.com/v1/userinfo
Authorization: Bearer ya29...

{
  "sub": "110169484474386276334",
  "name": "John Doe",
  "email": "john@gmail.com",
  "picture": "https://..."
}
```

### OIDC Discovery

```
// Provider เปิดเผย configuration ที่ URL มาตรฐาน
GET https://accounts.google.com/.well-known/openid-configuration

{
  "issuer": "https://accounts.google.com",
  "authorization_endpoint": "https://accounts.google.com/o/oauth2/v2/auth",
  "token_endpoint": "https://oauth2.googleapis.com/token",
  "userinfo_endpoint": "https://openidconnect.googleapis.com/v1/userinfo",
  "jwks_uri": "https://www.googleapis.com/oauth2/v3/certs",
  "scopes_supported": ["openid", "email", "profile"],
  "response_types_supported": ["code", "token", "id_token"],
  "subject_types_supported": ["public"],
  "id_token_signing_alg_values_supported": ["RS256"]
}
```

### OIDC Providers ที่ใช้บ่อย

```
- Google      → "Login with Google"
- Microsoft   → Azure AD, Microsoft Entra ID
- Apple       → "Sign in with Apple"
- Facebook    → Facebook Login
- Auth0       → Identity-as-a-Service
- Keycloak    → Self-hosted (open source)
- Okta        → Enterprise identity
```

---

## 8. SAML (Security Assertion Markup Language)

### SAML คืออะไร

มาตรฐาน **XML-based** สำหรับ **Single Sign-On (SSO)** ใช้มากในองค์กรขนาดใหญ่ (Enterprise) ให้พนักงาน login ครั้งเดียวแล้วใช้ได้ทุกระบบ

```
ตัวอย่าง: พนักงาน login เข้า Active Directory ครั้งเดียว
แล้วใช้ได้ทั้ง Salesforce, Slack, Jira, Office 365 โดยไม่ต้อง login ซ้ำ
```

### ตัวละครใน SAML

```
1. User (Principal)     = ผู้ใช้/พนักงาน
2. Identity Provider (IdP) = ระบบยืนยันตัวตนกลาง
                             (เช่น Okta, Azure AD, ADFS)
3. Service Provider (SP)   = แอปที่ต้องการใช้งาน
                             (เช่น Salesforce, Slack)
```

### SAML Flow (SP-Initiated SSO)

```
┌────────┐          ┌─────────────┐          ┌───────────────┐
│ ผู้ใช้  │          │ Service     │          │ Identity      │
│        │          │ Provider    │          │ Provider      │
│        │          │ (Slack)     │          │ (Okta)        │
└───┬────┘          └──────┬──────┘          └───────┬───────┘
    │                      │                         │
    │ 1. เข้า slack.com    │                         │
    │─────────────────────→│                         │
    │                      │                         │
    │ 2. ยังไม่ได้ login    │                         │
    │   redirect ไป IdP    │                         │
    │←─────────────────────│                         │
    │                      │                         │
    │ 3. redirect + SAML AuthnRequest                │
    │───────────────────────────────────────────────→│
    │                                                │
    │ 4. แสดงหน้า Login ของ IdP                      │
    │←───────────────────────────────────────────────│
    │                                                │
    │ 5. กรอก username/password                      │
    │───────────────────────────────────────────────→│
    │                                                │
    │ 6. สร้าง SAML Assertion (XML ยืนยันตัวตน)      │
    │   redirect กลับไป SP + SAML Response           │
    │←───────────────────────────────────────────────│
    │                      │                         │
    │ 7. ส่ง SAML Response │                         │
    │─────────────────────→│                         │
    │                      │ 8. ตรวจสอบ Assertion     │
    │                      │    สร้าง session         │
    │ 9. เข้าใช้ Slack ได้  │                         │
    │←─────────────────────│                         │
```

### SAML Assertion (ตัวอย่าง XML)

```xml
<saml:Assertion>
  <saml:Issuer>https://idp.example.com</saml:Issuer>

  <!-- ลายเซ็นดิจิทัล -->
  <ds:Signature>
    <ds:SignedInfo>...</ds:SignedInfo>
    <ds:SignatureValue>base64-signature</ds:SignatureValue>
  </ds:Signature>

  <!-- เงื่อนไข -->
  <saml:Conditions
    NotBefore="2024-01-15T10:00:00Z"
    NotOnOrAfter="2024-01-15T11:00:00Z">
    <saml:AudienceRestriction>
      <saml:Audience>https://slack.com</saml:Audience>
    </saml:AudienceRestriction>
  </saml:Conditions>

  <!-- ข้อมูลตัวตน -->
  <saml:Subject>
    <saml:NameID>john@company.com</saml:NameID>
  </saml:Subject>

  <!-- ข้อมูลเพิ่มเติม (Attributes) -->
  <saml:AttributeStatement>
    <saml:Attribute Name="email">
      <saml:AttributeValue>john@company.com</saml:AttributeValue>
    </saml:Attribute>
    <saml:Attribute Name="firstName">
      <saml:AttributeValue>John</saml:AttributeValue>
    </saml:Attribute>
    <saml:Attribute Name="role">
      <saml:AttributeValue>admin</saml:AttributeValue>
    </saml:Attribute>
  </saml:AttributeStatement>
</saml:Assertion>
```

### SAML vs OIDC

```
| หัวข้อ           | SAML              | OIDC              |
|------------------|-------------------|-------------------|
| รูปแบบข้อมูล      | XML               | JSON (JWT)        |
| Protocol         | SAML 2.0          | OAuth 2.0 + OIDC  |
| ขนาดข้อมูล       | ใหญ่ (XML)         | เล็ก (JSON)        |
| ความซับซ้อน       | สูง               | ปานกลาง            |
| Mobile support   | ไม่ดี             | ดีมาก              |
| SPA support      | ไม่ดี             | ดีมาก              |
| Enterprise SSO   | ดีมาก             | ดี                 |
| ใช้มากใน         | Enterprise        | Web, Mobile, API   |
| ตัวอย่าง Provider | ADFS, Shibboleth  | Google, Auth0      |
```

```
เลือกอะไร:
- Enterprise SSO + Legacy systems → SAML
- Web/Mobile app ทั่วไป → OIDC
- ต้องรองรับทั้งสอง → ใช้ Identity Provider ที่รองรับทั้ง SAML + OIDC
  (เช่น Auth0, Okta, Azure AD)
```

---

## 9. สรุปเปรียบเทียบทั้งหมด

```
| วิธี              | ความซับซ้อน | Stateless | ใช้กับ              |
|-------------------|-----------|-----------|---------------------|
| Basic Auth        | ต่ำ        | ใช่       | Internal, Dev       |
| Token Auth        | ต่ำ        | ไม่       | API ง่ายๆ           |
| JWT               | กลาง      | ใช่       | API, SPA, Mobile    |
| Cookie/Session    | กลาง      | ไม่       | Web app             |
| OAuth 2.0         | สูง       | ใช่       | Third-party access  |
| OIDC              | สูง       | ใช่       | Login with Google/FB|
| SAML              | สูงมาก    | ไม่       | Enterprise SSO      |
```

### แผนภาพเลือก

```
ต้องการระบบ Authentication:

├─ Internal API / Dev?
│  └─ Basic Auth หรือ API Key
│
├─ Web app แบบดั้งเดิม (SSR)?
│  └─ Cookie/Session Auth
│
├─ SPA / Mobile app?
│  ├─ API ของเราเอง → JWT
│  └─ Login with Google/FB → OIDC
│
├─ ให้แอปอื่นเข้าถึงข้อมูลผู้ใช้?
│  └─ OAuth 2.0
│
├─ Enterprise SSO (พนักงาน login ครั้งเดียว)?
│  ├─ ระบบเก่ามี → SAML
│  └─ ระบบใหม่ → OIDC
│
└─ Microservices คุยกัน?
   └─ OAuth 2.0 Client Credentials หรือ JWT
```
