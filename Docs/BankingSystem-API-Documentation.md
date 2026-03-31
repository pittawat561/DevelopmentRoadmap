# Banking System API Documentation

> เอกสารอ้างอิงสำหรับ API ทั้งหมดของระบบธนาคาร (Banking System)
> Base URL: `https://localhost:7001`

---

## สารบัญ

1. [ภาพรวมระบบ](#1-ภาพรวมระบบ)
2. [Authentication (การยืนยันตัวตน)](#2-authentication-การยืนยันตัวตน)
3. [Accounts (บัญชี)](#3-accounts-บัญชี)
4. [Transactions (ธุรกรรม)](#4-transactions-ธุรกรรม)
5. [Admin (ผู้ดูแลระบบ)](#5-admin-ผู้ดูแลระบบ)
6. [SignalR Real-time Notifications](#6-signalr-real-time-notifications)
7. [Error Handling](#7-error-handling)
8. [Rate Limiting](#8-rate-limiting)
9. [Data Models](#9-data-models)

---

## 1. ภาพรวมระบบ

### รูปแบบ Response มาตรฐาน

ทุก API จะตอบกลับในรูปแบบเดียวกัน:

```json
{
  "success": true,
  "message": "ข้อความอธิบายผลลัพธ์",
  "data": { }
}
```

### การยืนยันตัวตน (Authentication)

ระบบใช้ **JWT Bearer Token** สำหรับ API ที่ต้องการยืนยันตัวตน ให้ส่ง Header:

```
Authorization: Bearer <access_token>
```

### CORS

อนุญาตเฉพาะ `http://localhost:3000` (frontend)

---

## 2. Authentication (การยืนยันตัวตน)

### 2.1 สมัครสมาชิก (Register)

```
POST /api/auth/register
```

**ไม่ต้องใช้ Token** (AllowAnonymous)

**Request Body:**

| ฟิลด์ | ชนิด | จำเป็น | เงื่อนไข |
|-------|------|--------|---------|
| `firstName` | string | ใช่ | สูงสุด 100 ตัวอักษร |
| `lastName` | string | ใช่ | สูงสุด 100 ตัวอักษร |
| `email` | string | ใช่ | รูปแบบ email ถูกต้อง, สูงสุด 255 ตัวอักษร |
| `phone` | string | ใช่ | รูปแบบเบอร์โทรไทย (0 ตามด้วย 8-9 หลัก) |
| `password` | string | ใช่ | อย่างน้อย 8 ตัว, มีตัวพิมพ์ใหญ่ 1, ตัวพิมพ์เล็ก 1, ตัวเลข 1 |
| `confirmPassword` | string | ใช่ | ต้องตรงกับ password |

**ตัวอย่าง Request:**

```json
{
  "firstName": "สมชาย",
  "lastName": "ใจดี",
  "email": "somchai@example.com",
  "phone": "0812345678",
  "password": "MyPass123",
  "confirmPassword": "MyPass123"
}
```

**ตัวอย่าง Response (200 OK):**

```json
{
  "success": true,
  "message": "Registration successful.",
  "data": {
    "userId": "a1b2c3d4-...",
    "fullName": "สมชาย ใจดี",
    "email": "somchai@example.com",
    "accessToken": "eyJhbGciOiJIUzI1NiIs...",
    "refreshToken": "abc123def456...",
    "accessTokenExpiry": "2026-03-31T12:15:00Z"
  }
}
```

---

### 2.2 เข้าสู่ระบบ (Login)

```
POST /api/auth/login
```

**ไม่ต้องใช้ Token** (AllowAnonymous)

**Request Body:**

| ฟิลด์ | ชนิด | จำเป็น | เงื่อนไข |
|-------|------|--------|---------|
| `email` | string | ใช่ | รูปแบบ email ถูกต้อง |
| `password` | string | ใช่ | - |

**ตัวอย่าง Request:**

```json
{
  "email": "somchai@example.com",
  "password": "MyPass123"
}
```

**ตัวอย่าง Response (200 OK):**

```json
{
  "success": true,
  "message": "Login successful.",
  "data": {
    "userId": "a1b2c3d4-...",
    "fullName": "สมชาย ใจดี",
    "email": "somchai@example.com",
    "accessToken": "eyJhbGciOiJIUzI1NiIs...",
    "refreshToken": "abc123def456...",
    "accessTokenExpiry": "2026-03-31T12:15:00Z"
  }
}
```

> **หมายเหตุ:** Access Token มีอายุ 15 นาที, Refresh Token มีอายุ 7 วัน

---

### 2.3 ดูโปรไฟล์ (Get Profile)

```
GET /api/auth/profile
```

**ต้องใช้ Token** (Authorize)

**ตัวอย่าง Response (200 OK):**

```json
{
  "success": true,
  "message": "Profile retrieved.",
  "data": {
    "id": "a1b2c3d4-...",
    "firstName": "สมชาย",
    "lastName": "ใจดี",
    "email": "somchai@example.com",
    "phone": "0812345678",
    "kycStatus": "Pending",
    "createdAt": "2026-03-30T10:00:00Z"
  }
}
```

**KYC Status ที่เป็นไปได้:** `Pending` | `Verified` | `Rejected`

---

### 2.4 ออกจากระบบ (Logout)

```
POST /api/auth/logout
```

**ต้องใช้ Token** (Authorize)

ระบบจะนำ JWT token ไป blacklist ใน Redis ทำให้ token นั้นใช้งานไม่ได้อีก

**ตัวอย่าง Response (200 OK):**

```json
{
  "success": true,
  "message": "Logged out successfully."
}
```

---

## 3. Accounts (บัญชี)

> ทุก endpoint ในส่วนนี้ **ต้องใช้ Token**

### 3.1 ดูบัญชีทั้งหมดของผู้ใช้

```
GET /api/accounts?userId={userId}
```

**Query Parameters:**

| พารามิเตอร์ | ชนิด | จำเป็น | คำอธิบาย |
|-------------|------|--------|---------|
| `userId` | Guid | ใช่ | ID ของผู้ใช้ |

**ตัวอย่าง Response (200 OK):**

```json
{
  "success": true,
  "message": "Accounts retrieved.",
  "data": [
    {
      "id": "e5f6g7h8-...",
      "accountNumber": "1234-5678-9012",
      "type": "Savings",
      "currency": "THB",
      "balance": 50000.00,
      "availableBalance": 50000.00,
      "status": "Active",
      "createdAt": "2026-03-30T10:00:00Z"
    }
  ]
}
```

---

### 3.2 ดูบัญชีตาม ID

```
GET /api/accounts/{id}
```

**Path Parameters:**

| พารามิเตอร์ | ชนิด | คำอธิบาย |
|-------------|------|---------|
| `id` | Guid | ID ของบัญชี |

**Response:** เหมือน 3.1 แต่ return object เดียว (ไม่ใช่ array)

---

### 3.3 ดูยอดเงินคงเหลือ (มี Cache)

```
GET /api/accounts/{id}/balance
```

**Path Parameters:**

| พารามิเตอร์ | ชนิด | คำอธิบาย |
|-------------|------|---------|
| `id` | Guid | ID ของบัญชี |

**ตัวอย่าง Response (200 OK):**

```json
{
  "success": true,
  "message": "Balance retrieved.",
  "data": {
    "balance": 50000.00,
    "availableBalance": 50000.00,
    "currency": "THB",
    "source": "cache"
  }
}
```

> `source` จะเป็น `"cache"` ถ้าดึงจาก Redis หรือ `"database"` ถ้าดึงจาก DB โดยตรง

---

### 3.4 สร้างบัญชีใหม่

```
POST /api/accounts
```

**Request Body:**

| ฟิลด์ | ชนิด | จำเป็น | ค่าเริ่มต้น | เงื่อนไข |
|-------|------|--------|-----------|---------|
| `userId` | Guid | ใช่ | - | ID ของผู้ใช้ |
| `type` | string | ไม่ | `"Savings"` | `Savings`, `Checking`, `FixedDeposit` |
| `currency` | string | ไม่ | `"THB"` | รหัสสกุลเงิน |

**ตัวอย่าง Request:**

```json
{
  "userId": "a1b2c3d4-...",
  "type": "Savings",
  "currency": "THB"
}
```

**ตัวอย่าง Response (201 Created):**

```json
{
  "success": true,
  "message": "Account created.",
  "data": {
    "id": "e5f6g7h8-...",
    "accountNumber": "1234-5678-9012",
    "type": "Savings",
    "currency": "THB",
    "balance": 0.00,
    "availableBalance": 0.00,
    "status": "Active",
    "createdAt": "2026-03-31T10:00:00Z"
  }
}
```

> เลขบัญชีจะถูกสร้างอัตโนมัติในรูปแบบ `XXXX-XXXX-XXXX`

---

## 4. Transactions (ธุรกรรม)

> ทุก endpoint ในส่วนนี้ **ต้องใช้ Token**

### 4.1 ฝากเงิน (Deposit)

```
POST /api/transactions/deposit
```

**Request Body:**

| ฟิลด์ | ชนิด | จำเป็น | เงื่อนไข |
|-------|------|--------|---------|
| `accountId` | Guid | ใช่ | ID บัญชีที่ต้องการฝาก |
| `amount` | decimal | ใช่ | มากกว่า 0, ไม่เกิน 1,000,000, ทศนิยมไม่เกิน 2 ตำแหน่ง |
| `description` | string | ไม่ | สูงสุด 500 ตัวอักษร |

**ตัวอย่าง Request:**

```json
{
  "accountId": "e5f6g7h8-...",
  "amount": 10000.00,
  "description": "ฝากเงินเดือน"
}
```

**ตัวอย่าง Response (200 OK):**

```json
{
  "success": true,
  "message": "Deposit of 10,000.00 THB completed.",
  "data": {
    "id": "t1x2n3-...",
    "referenceNumber": "TXN-20260331-A1B2C3",
    "type": "Deposit",
    "amount": 10000.00,
    "balanceBefore": 50000.00,
    "balanceAfter": 60000.00,
    "status": "Completed",
    "description": "ฝากเงินเดือน",
    "createdAt": "2026-03-31T10:30:00Z"
  }
}
```

---

### 4.2 ถอนเงิน (Withdraw)

```
POST /api/transactions/withdraw
```

**Request Body:**

| ฟิลด์ | ชนิด | จำเป็น | เงื่อนไข |
|-------|------|--------|---------|
| `accountId` | Guid | ใช่ | ID บัญชีที่ต้องการถอน |
| `amount` | decimal | ใช่ | มากกว่า 0, ไม่เกิน 1,000,000, ทศนิยมไม่เกิน 2 ตำแหน่ง |
| `description` | string | ไม่ | สูงสุด 500 ตัวอักษร |

**ตัวอย่าง Request:**

```json
{
  "accountId": "e5f6g7h8-...",
  "amount": 5000.00,
  "description": "ถอนค่าใช้จ่าย"
}
```

**ข้อจำกัดทางธุรกิจ:**
- วงเงินถอนต่อวัน: 50,000 บาท
- บัญชีต้องไม่ถูกอายัด (Frozen)
- ยอดเงินต้องเพียงพอ

**Response:** เหมือนรูปแบบ Deposit โดย `type` จะเป็น `"Withdrawal"`

---

### 4.3 โอนเงิน (Transfer)

```
POST /api/transactions/transfer
```

**Request Body:**

| ฟิลด์ | ชนิด | จำเป็น | เงื่อนไข |
|-------|------|--------|---------|
| `fromAccountId` | Guid | ใช่ | บัญชีต้นทาง |
| `toAccountId` | Guid | ใช่ | บัญชีปลายทาง (ต้องไม่ใช่บัญชีเดียวกัน) |
| `amount` | decimal | ใช่ | มากกว่า 0, ไม่เกิน 1,000,000, ทศนิยมไม่เกิน 2 ตำแหน่ง |
| `description` | string | ไม่ | สูงสุด 500 ตัวอักษร |

**ตัวอย่าง Request:**

```json
{
  "fromAccountId": "e5f6g7h8-...",
  "toAccountId": "i9j0k1l2-...",
  "amount": 3000.00,
  "description": "โอนให้เพื่อน"
}
```

**ตัวอย่าง Response (200 OK):**

```json
{
  "success": true,
  "message": "Transfer of 3,000.00 THB completed.",
  "data": {
    "id": "t1x2n3-...",
    "referenceNumber": "TXN-20260331-D4E5F6",
    "type": "TransferOut",
    "amount": 3000.00,
    "balanceBefore": 60000.00,
    "balanceAfter": 57000.00,
    "status": "Completed",
    "description": "โอนให้เพื่อน",
    "createdAt": "2026-03-31T11:00:00Z"
  }
}
```

> ระบบจะสร้างธุรกรรมคู่: `TransferOut` (บัญชีต้นทาง) และ `TransferIn` (บัญชีปลายทาง)
> ใช้ Distributed Lock (Redis) ป้องกัน race condition

---

### 4.4 ดูประวัติธุรกรรม (Transaction History)

```
GET /api/transactions?accountId={accountId}&page={page}&pageSize={pageSize}
```

**Query Parameters:**

| พารามิเตอร์ | ชนิด | จำเป็น | ค่าเริ่มต้น | คำอธิบาย |
|-------------|------|--------|-----------|---------|
| `accountId` | Guid | ใช่ | - | ID ของบัญชี |
| `page` | int | ไม่ | 1 | หน้าที่ต้องการ |
| `pageSize` | int | ไม่ | 20 | จำนวนรายการต่อหน้า |

**ตัวอย่าง Response (200 OK):**

```json
{
  "success": true,
  "message": "Transaction history retrieved.",
  "data": {
    "items": [
      {
        "id": "t1x2n3-...",
        "referenceNumber": "TXN-20260331-A1B2C3",
        "type": "Deposit",
        "amount": 10000.00,
        "balanceBefore": 50000.00,
        "balanceAfter": 60000.00,
        "status": "Completed",
        "description": "ฝากเงินเดือน",
        "createdAt": "2026-03-31T10:30:00Z"
      }
    ],
    "totalCount": 45,
    "page": 1,
    "pageSize": 20,
    "totalPages": 3
  }
}
```

---

## 5. Admin (ผู้ดูแลระบบ)

> ทุก endpoint ในส่วนนี้ **ต้องใช้ Token**

### 5.1 ดู Dashboard

```
GET /api/admin/dashboard
```

**ตัวอย่าง Response (200 OK):**

```json
{
  "success": true,
  "message": "Dashboard data retrieved.",
  "data": {
    "totalUsers": 150,
    "totalAccounts": 230,
    "activeAccounts": 210,
    "frozenAccounts": 5,
    "totalBalance": 15000000.00,
    "lockedUsers": 3
  }
}
```

---

### 5.2 อายัดบัญชี (Freeze Account)

```
POST /api/admin/accounts/{id}/freeze
```

| พารามิเตอร์ | ชนิด | คำอธิบาย |
|-------------|------|---------|
| `id` | Guid | ID ของบัญชี |

**Response:** `200 OK` — `"Account 1234-5678-9012 has been frozen."`

---

### 5.3 ปลดอายัดบัญชี (Unfreeze Account)

```
POST /api/admin/accounts/{id}/unfreeze
```

| พารามิเตอร์ | ชนิด | คำอธิบาย |
|-------------|------|---------|
| `id` | Guid | ID ของบัญชี |

**Response:** `200 OK` — `"Account 1234-5678-9012 has been unfrozen."`

---

### 5.4 ปลดล็อกผู้ใช้ (Unlock User)

```
POST /api/admin/users/{id}/unlock
```

| พารามิเตอร์ | ชนิด | คำอธิบาย |
|-------------|------|---------|
| `id` | Guid | ID ของผู้ใช้ |

**Response:** `200 OK` — `"User สมชาย ใจดี has been unlocked."`

---

## 6. SignalR Real-time Notifications

### การเชื่อมต่อ

```
Hub URL: /hubs/notifications
```

เนื่องจาก WebSocket ไม่รองรับ custom headers ให้ส่ง JWT ผ่าน query string:

```
wss://localhost:7001/hubs/notifications?access_token=eyJhbGciOi...
```

### ตัวอย่างการเชื่อมต่อ (JavaScript)

```javascript
import * as signalR from "@microsoft/signalr";

const connection = new signalR.HubConnectionBuilder()
  .withUrl("https://localhost:7001/hubs/notifications", {
    accessTokenFactory: () => "your-jwt-token-here"
  })
  .withAutomaticReconnect()
  .build();

// เริ่มเชื่อมต่อ
await connection.start();
```

### Methods ที่ Client เรียกได้

#### สมัครรับการแจ้งเตือนบัญชี

```javascript
// เข้าร่วมกลุ่มเพื่อรับ update ของบัญชีเฉพาะ
await connection.invoke("JoinAccountGroup", "account-id-here");

// ออกจากกลุ่ม
await connection.invoke("LeaveAccountGroup", "account-id-here");
```

### Events ที่ Server ส่งมา

#### BalanceUpdated — ยอดเงินเปลี่ยนแปลง

```javascript
connection.on("BalanceUpdated", (data) => {
  console.log("บัญชี:", data.accountId);
  console.log("ยอดใหม่:", data.newBalance);
  console.log("ยอดพร้อมใช้:", data.newAvailableBalance);
});
```

#### TransactionCompleted — ธุรกรรมสำเร็จ

```javascript
connection.on("TransactionCompleted", (data) => {
  console.log("ประเภท:", data.type);       // "Deposit", "Withdrawal", "TransferIn", "TransferOut"
  console.log("จำนวน:", data.amount);
  console.log("เลขอ้างอิง:", data.referenceNumber);
});
```

---

## 7. Error Handling

### รูปแบบ Error Response

```json
{
  "success": false,
  "message": "คำอธิบาย error",
  "data": null
}
```

### HTTP Status Codes

| Status Code | ความหมาย | ตัวอย่างสถานการณ์ |
|-------------|---------|-----------------|
| `200` | สำเร็จ | ดำเนินการเรียบร้อย |
| `201` | สร้างสำเร็จ | สร้างบัญชีใหม่สำเร็จ |
| `400` | Bad Request | ข้อมูลไม่ถูกต้อง, ยอดเงินไม่เพียงพอ, เกินวงเงินต่อวัน |
| `401` | Unauthorized | ไม่ได้ส่ง Token หรือ Token หมดอายุ |
| `403` | Forbidden | บัญชีถูกอายัด, ผู้ใช้ถูกล็อก |
| `404` | Not Found | ไม่พบบัญชีหรือผู้ใช้ |
| `409` | Conflict | ข้อมูลซ้ำ (เช่น email ซ้ำ) |
| `429` | Too Many Requests | ส่ง request เกินจำนวนที่กำหนด |
| `500` | Server Error | เกิดข้อผิดพลาดภายในระบบ |

### Custom Exceptions

| Exception | Status | คำอธิบาย |
|-----------|--------|---------|
| `NotFoundException` | 404 | ไม่พบข้อมูลที่ร้องขอ |
| `InsufficientFundsException` | 400 | ยอดเงินไม่เพียงพอ |
| `DailyLimitExceededException` | 400 | เกินวงเงินถอนต่อวัน (50,000 บาท) |
| `AccountFrozenException` | 403 | บัญชีถูกอายัด |
| `AccountLockedException` | 403 | บัญชีถูกล็อก |
| `DuplicateException` | 409 | มีข้อมูลซ้ำในระบบ |

---

## 8. Rate Limiting

ระบบจำกัดจำนวน request เพื่อป้องกัน abuse:

| การตั้งค่า | ค่าเริ่มต้น |
|-----------|-----------|
| จำนวน request สูงสุด | 10 ครั้ง |
| ช่วงเวลา | 60 วินาที |

- จำกัดตาม **User ID** (ถ้า login แล้ว) หรือ **IP Address** (ถ้ายังไม่ login)
- เมื่อเกินจำนวน จะได้ `429 Too Many Requests`

---

## 9. Data Models

### ประเภทบัญชี (Account Type)

| ค่า | คำอธิบาย |
|-----|---------|
| `Savings` | บัญชีออมทรัพย์ — ดอกเบี้ยสูง, จำกัดการถอน |
| `Checking` | บัญชีกระแสรายวัน — ไม่มีดอกเบี้ย, ถอนไม่จำกัด |
| `FixedDeposit` | บัญชีเงินฝากประจำ — ดอกเบี้ยสูงสุด, มีค่าปรับถ้าถอนก่อนกำหนด |

### สถานะบัญชี (Account Status)

| ค่า | คำอธิบาย |
|-----|---------|
| `Active` | ใช้งานปกติ |
| `Frozen` | ถูกอายัด (ปัญหาทางกฎหมาย, สอบสวนการฉ้อโกง) |
| `Closed` | ปิดบัญชีแล้ว |

### ประเภทธุรกรรม (Transaction Type)

| ค่า | คำอธิบาย |
|-----|---------|
| `Deposit` | ฝากเงิน |
| `Withdrawal` | ถอนเงิน |
| `TransferIn` | รับโอนเงิน |
| `TransferOut` | โอนเงินออก |
| `Fee` | ค่าธรรมเนียม |
| `Interest` | ดอกเบี้ย |

### สถานะธุรกรรม (Transaction Status)

| ค่า | คำอธิบาย |
|-----|---------|
| `Pending` | รอดำเนินการ |
| `Processing` | กำลังดำเนินการ |
| `Completed` | สำเร็จ |
| `Failed` | ล้มเหลว |
| `Reversed` | ถูกยกเลิก/ย้อนกลับ |

### สถานะ KYC (Know Your Customer)

| ค่า | คำอธิบาย |
|-----|---------|
| `Pending` | รอการตรวจสอบ |
| `Verified` | ผ่านการตรวจสอบ |
| `Rejected` | ไม่ผ่านการตรวจสอบ |

### รูปแบบข้อมูลสำคัญ

| ข้อมูล | รูปแบบ | ตัวอย่าง |
|--------|--------|---------|
| เลขบัญชี | `XXXX-XXXX-XXXX` | `1234-5678-9012` |
| เลขอ้างอิงธุรกรรม | `TXN-YYYYMMDD-XXXXXX` | `TXN-20260331-A1B2C3` |
| เบอร์โทร | `0XXXXXXXXX` | `0812345678` |

---

## Quick Start Guide

### ขั้นตอนการใช้งาน API

```
1. สมัครสมาชิก     POST /api/auth/register
2. เข้าสู่ระบบ      POST /api/auth/login          → ได้ accessToken
3. สร้างบัญชี       POST /api/accounts            → ได้ accountId
4. ฝากเงิน          POST /api/transactions/deposit
5. ถอนเงิน          POST /api/transactions/withdraw
6. โอนเงิน          POST /api/transactions/transfer
7. ดูประวัติ         GET  /api/transactions?accountId=...
8. เชื่อมต่อ SignalR  /hubs/notifications           → รับ real-time updates
```

### ตัวอย่าง cURL

```bash
# 1. Login
curl -X POST https://localhost:7001/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"somchai@example.com","password":"MyPass123"}'

# 2. ดูบัญชี (ใส่ token ที่ได้จาก login)
curl https://localhost:7001/api/accounts?userId=YOUR_USER_ID \
  -H "Authorization: Bearer YOUR_ACCESS_TOKEN"

# 3. ฝากเงิน
curl -X POST https://localhost:7001/api/transactions/deposit \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer YOUR_ACCESS_TOKEN" \
  -d '{"accountId":"YOUR_ACCOUNT_ID","amount":10000,"description":"ฝากเงิน"}'

# 4. โอนเงิน
curl -X POST https://localhost:7001/api/transactions/transfer \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer YOUR_ACCESS_TOKEN" \
  -d '{"fromAccountId":"FROM_ID","toAccountId":"TO_ID","amount":3000,"description":"โอนเงิน"}'
```
