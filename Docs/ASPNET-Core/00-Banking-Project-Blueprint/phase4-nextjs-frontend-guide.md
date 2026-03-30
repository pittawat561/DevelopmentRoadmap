# Phase 4: Next.js Frontend — คู่มือทำทีละขั้นตอน

> Next.js 15 (App Router) + Tailwind CSS + shadcn/ui + React Query + SignalR + Zustand
> ทุกขั้นตอนอธิบายว่า "ทำไม" ต้องทำ + สร้างเพื่ออะไร

---

## สิ่งที่ต้องเสร็จก่อน (จาก Phase 3)

```
☑ Backend API ทำงานครบ: Auth, Accounts, Transactions, Admin
☑ JWT Authentication + Token Blacklist (Redis)
☑ Redis: Balance Cache, Distributed Lock, Rate Limiting
☑ SignalR Hub: /hubs/notifications (real-time)
☑ CORS configured สำหรับ http://localhost:3000
☑ API รันได้ + ทดสอบผ่าน Swagger
```

---

## ภาพรวม Phase 4 — Frontend Architecture

```
┌─────────────────────────────────────────────────────────┐
│                    Next.js 15 App                        │
│                                                          │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐  │
│  │  App Router   │  │  Middleware   │  │  Layouts      │  │
│  │  (Pages)      │  │  (Auth Guard)│  │  (Shared UI)  │  │
│  └──────┬───────┘  └──────────────┘  └──────────────┘  │
│         │                                                │
│  ┌──────▼────────────────────────────────────────────┐  │
│  │              React Query (TanStack Query)          │  │
│  │         API State Management + Caching             │  │
│  └──────┬────────────────────────┬───────────────────┘  │
│         │                        │                       │
│  ┌──────▼───────┐  ┌────────────▼───────────────────┐  │
│  │  API Client   │  │  SignalR Client                 │  │
│  │  (Axios/Fetch)│  │  (Real-time WebSocket)          │  │
│  └──────┬───────┘  └────────────┬───────────────────┘  │
│         │                        │                       │
│  ┌──────▼───────┐  ┌────────────▼───────────────────┐  │
│  │  Zustand      │  │  React Hook Form + Zod         │  │
│  │  (Client State│  │  (Form Validation)              │  │
│  │  Auth, Theme) │  │                                 │  │
│  └──────────────┘  └────────────────────────────────┘  │
│                                                          │
│  ┌────────────────────────────────────────────────────┐ │
│  │           shadcn/ui + Tailwind CSS                  │ │
│  │         Component Library + Styling                  │ │
│  └────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────┘
                          │
                          ▼
              ┌──────────────────────┐
              │   ASP.NET Core API    │
              │   (Backend Phase 1-3) │
              └──────────────────────┘
```

```
ทำไมเลือก Tech Stack นี้:

Next.js 15 (App Router):
  - Server Components → โหลดเร็ว (render ฝั่ง server)
  - File-based routing → สร้าง page = สร้างไฟล์
  - Middleware → ป้องกัน route ได้ก่อน render
  - ใช้กันแพร่หลาย, ตลาดงานเยอะ

shadcn/ui + Tailwind CSS:
  - shadcn/ui = component library ที่ copy code เข้า project (ไม่ใช่ dependency)
  - Tailwind CSS = utility-first CSS (เขียน style ใน className)
  - สวย, responsive, accessibility ดี, customize ง่าย

React Query (TanStack Query):
  - จัดการ server state (data จาก API)
  - Auto caching, refetching, error handling
  - ไม่ต้องเขียน useEffect + useState สำหรับ fetch data

Zustand:
  - จัดการ client state (auth, theme, sidebar)
  - เบากว่า Redux มาก, ไม่ต้องเขียน boilerplate
  - ใช้ง่าย — สร้าง store = 5 บรรทัด

React Hook Form + Zod:
  - Form management ที่เร็ว (ไม่ re-render ทุก keystroke)
  - Zod = schema validation แบบ TypeScript-first
  - ใช้คู่กัน → type-safe form validation
```

---

## ขั้นตอนที่ 1: สร้าง Next.js Project

### 1.1 สร้าง Project

```bash
# สร้าง Next.js project ใน folder frontend (อยู่ข้างๆ BankingSystem)
cd D:\Development\DevelopmentRoadmap
npx create-next-app@latest banking-frontend

# ตอบคำถาม:
# ✔ Would you like to use TypeScript? → Yes
# ✔ Would you like to use ESLint? → Yes
# ✔ Would you like to use Tailwind CSS? → Yes
# ✔ Would you like your code inside a `src/` directory? → Yes
# ✔ Would you like to use App Router? → Yes
# ✔ Would you like to use Turbopack for next dev? → Yes
# ✔ Would you like to customize the import alias? → Yes (@/*)
```

```
โครงสร้างที่ได้:

banking-frontend/
├── src/
│   ├── app/                    ← App Router (pages)
│   │   ├── layout.tsx          ← Root layout
│   │   ├── page.tsx            ← Landing page (/)
│   │   └── globals.css         ← Global styles
│   └── ...
├── public/                     ← Static files
├── package.json
├── tailwind.config.ts
├── tsconfig.json
└── next.config.ts
```

### 1.2 ติดตั้ง Dependencies

```bash
cd banking-frontend

# ===== UI =====
# shadcn/ui CLI (ติดตั้ง component library)
npx shadcn@latest init

# ตอบคำถาม:
# ✔ Style → New York
# ✔ Base color → Neutral
# ✔ CSS variables → Yes

# ติดตั้ง shadcn components ที่ใช้บ่อย
npx shadcn@latest add button card input label
npx shadcn@latest add form dialog dropdown-menu
npx shadcn@latest add table badge separator
npx shadcn@latest add toast tabs avatar
npx shadcn@latest add sheet skeleton alert

# ===== State Management & Data Fetching =====
npm install @tanstack/react-query          # React Query
npm install zustand                         # Client state
npm install axios                           # HTTP client

# ===== Forms & Validation =====
npm install react-hook-form                 # Form management
npm install @hookform/resolvers             # Zod resolver สำหรับ react-hook-form
npm install zod                             # Schema validation

# ===== Real-time =====
npm install @microsoft/signalr              # SignalR client

# ===== Utilities =====
npm install date-fns                        # Date formatting
npm install lucide-react                    # Icons (มากับ shadcn)
npm install js-cookie                       # Cookie management
npm install @types/js-cookie -D             # TypeScript types
```

```
ทำไมแต่ละ package:

@tanstack/react-query:
  ❌ ไม่ใช้: useEffect + useState + loading + error + fetch
  ✅ ใช้: const { data, isLoading, error } = useQuery(...)
  → Auto cache, refetch, retry, stale-while-revalidate

zustand:
  ❌ ไม่ใช้: React Context + Provider + useReducer (boilerplate เยอะ)
  ✅ ใช้: const useAuthStore = create((set) => ({ ... }))
  → Simple, performant, no Provider needed

axios:
  ❌ ไม่ใช้: fetch (ไม่มี interceptor, ไม่ auto parse JSON)
  ✅ ใช้: axios.get('/api/...') + interceptor สำหรับ auth token
  → Request/response interceptors, auto JSON, timeout, cancel

react-hook-form + zod:
  ❌ ไม่ใช้: controlled inputs (re-render ทุก keystroke)
  ✅ ใช้: useForm + zodResolver → validate + submit
  → Performance ดี, type-safe, error messages อัตโนมัติ

@microsoft/signalr:
  Official client library สำหรับ SignalR
  Auto reconnect, WebSocket → SSE → Long Polling fallback
```

---

## ขั้นตอนที่ 2: โครงสร้าง Folder

```
📁 สร้าง folder structure ทั้งหมด

ทำไม: จัดระเบียบ code ตาม feature/responsibility
ไม่กองไฟล์รวมกัน → หาง่าย, แก้ง่าย
```

```
src/
├── app/                           ← Pages (App Router)
│   ├── (auth)/                    ← Group: Auth pages (ไม่ต้อง login)
│   │   ├── login/
│   │   │   └── page.tsx
│   │   ├── register/
│   │   │   └── page.tsx
│   │   └── layout.tsx             ← Auth layout (centered form)
│   │
│   ├── (dashboard)/               ← Group: Protected pages (ต้อง login)
│   │   ├── dashboard/
│   │   │   └── page.tsx           ← หน้าหลัก: ยอดเงิน, กราฟ, recent txn
│   │   ├── accounts/
│   │   │   ├── page.tsx           ← รายการบัญชี
│   │   │   └── [id]/
│   │   │       └── page.tsx       ← รายละเอียด + statement
│   │   ├── deposit/
│   │   │   └── page.tsx           ← ฝากเงิน
│   │   ├── withdraw/
│   │   │   └── page.tsx           ← ถอนเงิน
│   │   ├── transfer/
│   │   │   └── page.tsx           ← โอนเงิน
│   │   ├── transactions/
│   │   │   └── page.tsx           ← ประวัติ transactions
│   │   ├── settings/
│   │   │   └── page.tsx           ← ตั้งค่า profile
│   │   └── layout.tsx             ← Dashboard layout (sidebar + header)
│   │
│   ├── admin/                     ← Admin pages (role-based)
│   │   ├── page.tsx               ← Admin dashboard
│   │   └── layout.tsx
│   │
│   ├── layout.tsx                 ← Root layout (providers)
│   ├── page.tsx                   ← Landing page (/)
│   └── globals.css
│
├── components/                    ← Shared components
│   ├── ui/                        ← shadcn/ui components (auto-generated)
│   │   ├── button.tsx
│   │   ├── card.tsx
│   │   ├── input.tsx
│   │   └── ...
│   ├── layout/                    ← Layout components
│   │   ├── sidebar.tsx
│   │   ├── header.tsx
│   │   ├── mobile-nav.tsx
│   │   └── user-menu.tsx
│   ├── dashboard/                 ← Dashboard-specific components
│   │   ├── balance-card.tsx
│   │   ├── recent-transactions.tsx
│   │   └── quick-actions.tsx
│   ├── transactions/              ← Transaction components
│   │   ├── transaction-form.tsx
│   │   ├── transaction-list.tsx
│   │   └── transaction-item.tsx
│   └── shared/                    ← Shared/generic components
│       ├── loading-spinner.tsx
│       ├── error-message.tsx
│       ├── page-header.tsx
│       └── empty-state.tsx
│
├── lib/                           ← Utilities & Configuration
│   ├── api/                       ← API client & endpoints
│   │   ├── client.ts              ← Axios instance + interceptors
│   │   ├── auth.ts                ← Auth API calls
│   │   ├── accounts.ts            ← Account API calls
│   │   └── transactions.ts        ← Transaction API calls
│   ├── hooks/                     ← Custom React hooks
│   │   ├── use-auth.ts            ← Auth hook (login, logout, user)
│   │   ├── use-accounts.ts        ← Account queries
│   │   ├── use-transactions.ts    ← Transaction queries & mutations
│   │   └── use-signalr.ts         ← SignalR connection hook
│   ├── stores/                    ← Zustand stores
│   │   ├── auth-store.ts          ← Auth state (token, user)
│   │   └── ui-store.ts            ← UI state (sidebar, theme)
│   ├── validations/               ← Zod schemas
│   │   ├── auth.ts                ← Login/Register schemas
│   │   └── transaction.ts         ← Deposit/Withdraw/Transfer schemas
│   ├── types/                     ← TypeScript types
│   │   ├── api.ts                 ← API response types
│   │   ├── auth.ts                ← Auth types
│   │   └── transaction.ts         ← Transaction types
│   └── utils/                     ← Helper functions
│       ├── format.ts              ← Format currency, date
│       └── cn.ts                  ← className utility (shadcn)
│
├── providers/                     ← React Providers
│   ├── query-provider.tsx         ← React Query Provider
│   ├── signalr-provider.tsx       ← SignalR Connection Provider
│   └── theme-provider.tsx         ← Theme Provider (dark mode)
│
└── middleware.ts                   ← Next.js Middleware (Auth Guard)
```

```
ทำไมจัดแบบนี้:

(auth) / (dashboard) — Route Groups:
  วงเล็บ = group แต่ไม่เพิ่ม segment ใน URL
  /login ไม่ใช่ /(auth)/login
  ใช้แยก layout: auth มี centered form, dashboard มี sidebar

lib/ — ไม่ใช่ component:
  api/: HTTP calls (ไม่มี React)
  hooks/: Custom hooks (มี React แต่ไม่ render)
  stores/: State management (Zustand)
  types/: TypeScript definitions

components/ — Render UI:
  ui/: shadcn/ui primitives (Button, Input)
  layout/: Sidebar, Header
  dashboard/: Dashboard-specific
  shared/: ใช้ทุกที่ (Loading, Error)
```

---

## ขั้นตอนที่ 3: TypeScript Types

```
📁 src/lib/types/

ทำไม: กำหนด type ให้ตรงกับ API response
TypeScript บอก error ตั้งแต่เขียน ไม่ต้องรอ runtime
```

### 3.1 API Response Types

```typescript
// src/lib/types/api.ts

/// <summary>
/// API Response มาตรฐานจาก Backend — ตรงกับ ApiResponse<T> ใน C#
/// </summary>
export interface ApiResponse<T> {
  success: boolean;
  message: string;
  data?: T;
}

/// <summary>
/// Paged Response — ตรงกับ PagedResponse<T> ใน C#
/// </summary>
export interface PagedResponse<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

/// <summary>
/// API Error — สำหรับ catch block
/// </summary>
export interface ApiError {
  success: false;
  message: string;
  statusCode: number;
}
```

### 3.2 Auth Types

```typescript
// src/lib/types/auth.ts

export interface User {
  id: string;
  firstName: string;
  lastName: string;
  email: string;
  phone: string;
  kycStatus: "Pending" | "Verified" | "Rejected";
  createdAt: string;
}

export interface AuthResponse {
  userId: string;
  fullName: string;
  email: string;
  accessToken: string;
  refreshToken: string;
  accessTokenExpiry: string;
}

export interface LoginRequest {
  email: string;
  password: string;
}

export interface RegisterRequest {
  firstName: string;
  lastName: string;
  email: string;
  phone: string;
  password: string;
  confirmPassword: string;
}
```

### 3.3 Transaction Types

```typescript
// src/lib/types/transaction.ts

export interface Account {
  id: string;
  accountNumber: string;
  type: "Savings" | "Checking" | "FixedDeposit";
  currency: string;
  balance: number;
  availableBalance: number;
  status: "Active" | "Frozen" | "Closed";
  createdAt: string;
}

export interface Transaction {
  id: string;
  referenceNumber: string;
  type: "Deposit" | "Withdrawal" | "TransferIn" | "TransferOut" | "Fee" | "Interest";
  amount: number;
  balanceBefore: number;
  balanceAfter: number;
  status: "Pending" | "Processing" | "Completed" | "Failed" | "Reversed";
  description?: string;
  createdAt: string;
}

export interface DepositRequest {
  accountId: string;
  amount: number;
  description?: string;
}

export interface WithdrawRequest {
  accountId: string;
  amount: number;
  description?: string;
}

export interface TransferRequest {
  fromAccountId: string;
  toAccountId: string;
  amount: number;
  description?: string;
}

export interface BalanceInfo {
  balance: number;
  availableBalance: number;
  currency: string;
  source: "cache" | "database";
}
```

---

## ขั้นตอนที่ 4: API Client (Axios)

```
📁 src/lib/api/client.ts

ทำไม: สร้าง Axios instance กลาง
- ตั้ง base URL ที่เดียว
- Interceptor: แนบ JWT token ทุก request อัตโนมัติ
- Interceptor: จับ 401 → redirect ไป login
```

```typescript
// src/lib/api/client.ts

import axios from "axios";
import Cookies from "js-cookie";

/// <summary>
/// Axios Instance — HTTP client กลางสำหรับทุก API call
///
/// ทำไมใช้ instance แทน axios ตรง:
///   axios.get('/api/...') → ต้องตั้ง baseURL ทุกครั้ง
///   apiClient.get('/accounts') → ใช้ baseURL ที่ตั้งไว้
///
/// Interceptors:
///   Request: แนบ Authorization header อัตโนมัติ
///   Response: จับ 401 → clear token → redirect login
/// </summary>
const apiClient = axios.create({
  baseURL: process.env.NEXT_PUBLIC_API_URL || "https://localhost:7001/api",
  headers: {
    "Content-Type": "application/json",
  },
  timeout: 15000, // 15 seconds
});

// ===== Request Interceptor: แนบ JWT token =====
apiClient.interceptors.request.use(
  (config) => {
    const token = Cookies.get("accessToken");
    if (token) {
      config.headers.Authorization = `Bearer ${token}`;
    }
    return config;
  },
  (error) => Promise.reject(error)
);

// ===== Response Interceptor: จับ error =====
apiClient.interceptors.response.use(
  (response) => response,
  async (error) => {
    // 401 Unauthorized → token หมดอายุหรือถูก revoke
    if (error.response?.status === 401) {
      Cookies.remove("accessToken");
      Cookies.remove("refreshToken");

      // redirect ไป login (ถ้าอยู่ฝั่ง client)
      if (typeof window !== "undefined") {
        window.location.href = "/login";
      }
    }

    return Promise.reject(error);
  }
);

export default apiClient;
```

### 4.1 Auth API

```typescript
// src/lib/api/auth.ts

import apiClient from "./client";
import type { ApiResponse } from "@/lib/types/api";
import type { AuthResponse, LoginRequest, RegisterRequest, User } from "@/lib/types/auth";

export const authApi = {
  register: (data: RegisterRequest) =>
    apiClient.post<ApiResponse<AuthResponse>>("/auth/register", data),

  login: (data: LoginRequest) =>
    apiClient.post<ApiResponse<AuthResponse>>("/auth/login", data),

  getProfile: () =>
    apiClient.get<ApiResponse<User>>("/auth/profile"),

  logout: () =>
    apiClient.post<ApiResponse<null>>("/auth/logout"),
};
```

### 4.2 Accounts API

```typescript
// src/lib/api/accounts.ts

import apiClient from "./client";
import type { ApiResponse } from "@/lib/types/api";
import type { Account, BalanceInfo } from "@/lib/types/transaction";

export const accountsApi = {
  getByUserId: (userId: string) =>
    apiClient.get<ApiResponse<Account[]>>(`/accounts?userId=${userId}`),

  getById: (id: string) =>
    apiClient.get<ApiResponse<Account>>(`/accounts/${id}`),

  getBalance: (id: string) =>
    apiClient.get<ApiResponse<BalanceInfo>>(`/accounts/${id}/balance`),

  create: (data: { userId: string; type?: string; currency?: string }) =>
    apiClient.post<ApiResponse<Account>>("/accounts", data),
};
```

### 4.3 Transactions API

```typescript
// src/lib/api/transactions.ts

import apiClient from "./client";
import type { ApiResponse, PagedResponse } from "@/lib/types/api";
import type {
  Transaction,
  DepositRequest,
  WithdrawRequest,
  TransferRequest,
} from "@/lib/types/transaction";

export const transactionsApi = {
  deposit: (data: DepositRequest) =>
    apiClient.post<ApiResponse<Transaction>>("/transactions/deposit", data),

  withdraw: (data: WithdrawRequest) =>
    apiClient.post<ApiResponse<Transaction>>("/transactions/withdraw", data),

  transfer: (data: TransferRequest) =>
    apiClient.post<ApiResponse<Transaction>>("/transactions/transfer", data),

  getHistory: (accountId: string, page = 1, pageSize = 20) =>
    apiClient.get<ApiResponse<PagedResponse<Transaction>>>(
      `/transactions?accountId=${accountId}&page=${page}&pageSize=${pageSize}`
    ),
};
```

---

## ขั้นตอนที่ 5: Zustand Store (Auth State)

```
📁 src/lib/stores/auth-store.ts

ทำไม: เก็บ auth state (user, token) ไว้ที่ส่วนกลาง
ทุก component เข้าถึงได้ ไม่ต้อง prop drilling
```

```typescript
// src/lib/stores/auth-store.ts

import { create } from "zustand";
import Cookies from "js-cookie";
import type { AuthResponse } from "@/lib/types/auth";

/// <summary>
/// Auth Store — Zustand
///
/// เก็บ state:
///   user: ข้อมูล user ที่ login อยู่
///   isAuthenticated: login อยู่ไหม
///
/// เก็บ token ใน Cookie (ไม่ใช่ Zustand):
///   Cookie → ส่งไปกับทุก request อัตโนมัติ
///   + Middleware อ่านได้ (Server-side)
///   + httpOnly ป้องกัน XSS (ถ้าตั้ง server-side)
/// </summary>
interface AuthState {
  user: {
    id: string;
    fullName: string;
    email: string;
  } | null;
  isAuthenticated: boolean;

  // Actions
  setAuth: (auth: AuthResponse) => void;
  clearAuth: () => void;
  initialize: () => void;
}

export const useAuthStore = create<AuthState>((set) => ({
  user: null,
  isAuthenticated: false,

  /// <summary>
  /// Login สำเร็จ → เก็บ token + user
  /// </summary>
  setAuth: (auth: AuthResponse) => {
    // เก็บ token ใน Cookie
    Cookies.set("accessToken", auth.accessToken, {
      expires: 1 / 96, // 15 minutes (1/96 ของ 1 วัน)
      secure: process.env.NODE_ENV === "production",
      sameSite: "lax",
    });
    Cookies.set("refreshToken", auth.refreshToken, {
      expires: 7, // 7 days
      secure: process.env.NODE_ENV === "production",
      sameSite: "lax",
    });

    // เก็บ user ใน Zustand
    set({
      user: {
        id: auth.userId,
        fullName: auth.fullName,
        email: auth.email,
      },
      isAuthenticated: true,
    });
  },

  /// <summary>
  /// Logout → ลบทุกอย่าง
  /// </summary>
  clearAuth: () => {
    Cookies.remove("accessToken");
    Cookies.remove("refreshToken");
    set({ user: null, isAuthenticated: false });
  },

  /// <summary>
  /// Initialize — เช็คว่ามี token อยู่ไหม (hydrate จาก Cookie)
  /// เรียกตอน app load ครั้งแรก
  /// </summary>
  initialize: () => {
    const token = Cookies.get("accessToken");
    if (token) {
      // TODO: decode JWT เพื่อดึง user info
      // หรือเรียก /auth/profile
      set({ isAuthenticated: true });
    }
  },
}));
```

---

## ขั้นตอนที่ 6: Zod Validation Schemas

```
📁 src/lib/validations/

ทำไม: validate form ฝั่ง client ก่อนส่งไป API
Zod → type-safe + error messages ภาษาไทย/อังกฤษ
```

```typescript
// src/lib/validations/auth.ts

import { z } from "zod";

export const loginSchema = z.object({
  email: z
    .string()
    .min(1, "Email is required.")
    .email("Invalid email format."),
  password: z
    .string()
    .min(1, "Password is required."),
});

export const registerSchema = z
  .object({
    firstName: z
      .string()
      .min(1, "First name is required.")
      .max(100),
    lastName: z
      .string()
      .min(1, "Last name is required.")
      .max(100),
    email: z
      .string()
      .min(1, "Email is required.")
      .email("Invalid email format.")
      .max(255),
    phone: z
      .string()
      .min(1, "Phone is required.")
      .regex(/^0\d{8,9}$/, "Invalid Thai phone number format."),
    password: z
      .string()
      .min(8, "Password must be at least 8 characters.")
      .regex(/[A-Z]/, "Must contain at least one uppercase letter.")
      .regex(/[a-z]/, "Must contain at least one lowercase letter.")
      .regex(/\d/, "Must contain at least one digit."),
    confirmPassword: z
      .string()
      .min(1, "Please confirm your password."),
  })
  .refine((data) => data.password === data.confirmPassword, {
    message: "Passwords do not match.",
    path: ["confirmPassword"],
  });

export type LoginFormValues = z.infer<typeof loginSchema>;
export type RegisterFormValues = z.infer<typeof registerSchema>;
```

```typescript
// src/lib/validations/transaction.ts

import { z } from "zod";

export const depositSchema = z.object({
  accountId: z.string().min(1, "Account is required."),
  amount: z
    .number({ invalid_type_error: "Amount must be a number." })
    .positive("Amount must be greater than 0.")
    .max(1_000_000, "Amount cannot exceed 1,000,000 per transaction."),
  description: z.string().max(500).optional(),
});

export const withdrawSchema = z.object({
  accountId: z.string().min(1, "Account is required."),
  amount: z
    .number({ invalid_type_error: "Amount must be a number." })
    .positive("Amount must be greater than 0.")
    .max(1_000_000, "Amount cannot exceed 1,000,000 per transaction."),
  description: z.string().max(500).optional(),
});

export const transferSchema = z
  .object({
    fromAccountId: z.string().min(1, "Source account is required."),
    toAccountId: z.string().min(1, "Destination account is required."),
    amount: z
      .number({ invalid_type_error: "Amount must be a number." })
      .positive("Amount must be greater than 0.")
      .max(1_000_000, "Amount cannot exceed 1,000,000 per transaction."),
    description: z.string().max(500).optional(),
  })
  .refine((data) => data.fromAccountId !== data.toAccountId, {
    message: "Cannot transfer to the same account.",
    path: ["toAccountId"],
  });

export type DepositFormValues = z.infer<typeof depositSchema>;
export type WithdrawFormValues = z.infer<typeof withdrawSchema>;
export type TransferFormValues = z.infer<typeof transferSchema>;
```

---

## ขั้นตอนที่ 7: React Query Hooks

```
📁 src/lib/hooks/

ทำไม: Custom hooks ครอบ React Query
ทุก component ใช้ hook เดียวกัน → ไม่เขียน fetch ซ้ำ
```

### 7.1 Auth Hooks

```typescript
// src/lib/hooks/use-auth.ts

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { authApi } from "@/lib/api/auth";
import { useAuthStore } from "@/lib/stores/auth-store";
import { useRouter } from "next/navigation";
import type { LoginRequest, RegisterRequest } from "@/lib/types/auth";

/// <summary>
/// useLogin — mutation สำหรับ login
///
/// useMutation vs useQuery:
///   useQuery = GET (อ่านข้อมูล, auto fetch)
///   useMutation = POST/PUT/DELETE (เปลี่ยนข้อมูล, เรียกเมื่อ submit)
/// </summary>
export function useLogin() {
  const router = useRouter();
  const setAuth = useAuthStore((state) => state.setAuth);

  return useMutation({
    mutationFn: (data: LoginRequest) => authApi.login(data),
    onSuccess: (response) => {
      const auth = response.data.data!;
      setAuth(auth);
      router.push("/dashboard");
    },
  });
}

export function useRegister() {
  const router = useRouter();
  const setAuth = useAuthStore((state) => state.setAuth);

  return useMutation({
    mutationFn: (data: RegisterRequest) => authApi.register(data),
    onSuccess: (response) => {
      const auth = response.data.data!;
      setAuth(auth);
      router.push("/dashboard");
    },
  });
}

export function useLogout() {
  const router = useRouter();
  const clearAuth = useAuthStore((state) => state.clearAuth);
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: () => authApi.logout(),
    onSuccess: () => {
      clearAuth();
      queryClient.clear(); // ลบ cache ทั้งหมด
      router.push("/login");
    },
  });
}

export function useProfile() {
  return useQuery({
    queryKey: ["profile"],
    queryFn: () => authApi.getProfile().then((res) => res.data.data!),
    retry: false,
  });
}
```

### 7.2 Account Hooks

```typescript
// src/lib/hooks/use-accounts.ts

import { useQuery } from "@tanstack/react-query";
import { accountsApi } from "@/lib/api/accounts";

/// <summary>
/// useAccounts — ดึงบัญชีทั้งหมดของ user
///
/// queryKey: ["accounts", userId]
///   React Query ใช้ key เป็น cache key
///   ถ้า userId เปลี่ยน → fetch ใหม่
///   ถ้า userId เดิม → ใช้ cache (stale-while-revalidate)
/// </summary>
export function useAccounts(userId: string) {
  return useQuery({
    queryKey: ["accounts", userId],
    queryFn: () =>
      accountsApi.getByUserId(userId).then((res) => res.data.data!),
    enabled: !!userId, // ไม่ fetch ถ้าไม่มี userId
  });
}

export function useAccount(id: string) {
  return useQuery({
    queryKey: ["account", id],
    queryFn: () =>
      accountsApi.getById(id).then((res) => res.data.data!),
    enabled: !!id,
  });
}

/// <summary>
/// useBalance — ดึงยอดเงิน (จาก Redis cache ฝั่ง backend)
///
/// refetchInterval: 30000 = auto refetch ทุก 30 วินาที
/// + SignalR จะ invalidate cache เมื่อ balance เปลี่ยน (ขั้นตอนที่ 9)
/// </summary>
export function useBalance(accountId: string) {
  return useQuery({
    queryKey: ["balance", accountId],
    queryFn: () =>
      accountsApi.getBalance(accountId).then((res) => res.data.data!),
    enabled: !!accountId,
    refetchInterval: 30_000, // Auto refetch ทุก 30 วินาที
  });
}
```

### 7.3 Transaction Hooks

```typescript
// src/lib/hooks/use-transactions.ts

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { transactionsApi } from "@/lib/api/transactions";
import type {
  DepositRequest,
  WithdrawRequest,
  TransferRequest,
} from "@/lib/types/transaction";

/// <summary>
/// useDeposit — mutation สำหรับฝากเงิน
///
/// onSuccess: invalidateQueries → บอก React Query ว่า data เปลี่ยนแล้ว
///   ["accounts"] → refetch รายการบัญชี (balance เปลี่ยน)
///   ["balance"] → refetch ยอดเงิน
///   ["transactions"] → refetch ประวัติ
/// </summary>
export function useDeposit() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (data: DepositRequest) => transactionsApi.deposit(data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["accounts"] });
      queryClient.invalidateQueries({ queryKey: ["balance"] });
      queryClient.invalidateQueries({ queryKey: ["transactions"] });
    },
  });
}

export function useWithdraw() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (data: WithdrawRequest) => transactionsApi.withdraw(data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["accounts"] });
      queryClient.invalidateQueries({ queryKey: ["balance"] });
      queryClient.invalidateQueries({ queryKey: ["transactions"] });
    },
  });
}

export function useTransfer() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (data: TransferRequest) => transactionsApi.transfer(data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["accounts"] });
      queryClient.invalidateQueries({ queryKey: ["balance"] });
      queryClient.invalidateQueries({ queryKey: ["transactions"] });
    },
  });
}

/// <summary>
/// useTransactionHistory — ดึงประวัติแบบ pagination
///
/// keepPreviousData: true → เมื่อเปลี่ยนหน้า ยังแสดง data เดิมจน data ใหม่มา
/// ไม่กระพริบ loading ทุกครั้งที่เปลี่ยนหน้า
/// </summary>
export function useTransactionHistory(
  accountId: string,
  page: number = 1,
  pageSize: number = 20
) {
  return useQuery({
    queryKey: ["transactions", accountId, page, pageSize],
    queryFn: () =>
      transactionsApi
        .getHistory(accountId, page, pageSize)
        .then((res) => res.data.data!),
    enabled: !!accountId,
    placeholderData: (previousData) => previousData, // keep previous data
  });
}
```

---

## ขั้นตอนที่ 8: SignalR Client Hook

```
📁 src/lib/hooks/use-signalr.ts

ทำไม: เชื่อมต่อ SignalR Hub → รับ real-time notifications
ยอดเงินอัปเดตทันทีเมื่อมี transaction
```

```typescript
// src/lib/hooks/use-signalr.ts

"use client";

import { useEffect, useRef } from "react";
import * as signalR from "@microsoft/signalr";
import { useQueryClient } from "@tanstack/react-query";
import Cookies from "js-cookie";

/// <summary>
/// useSignalR — เชื่อมต่อ SignalR Hub
///
/// Flow:
///   1. สร้าง connection ไปยัง /hubs/notifications
///   2. ส่ง JWT token ผ่าน query string (WebSocket ไม่มี header)
///   3. ลงทะเบียน event handlers:
///      - "BalanceUpdated" → invalidate balance query
///      - "TransactionCompleted" → invalidate transactions query + show toast
///   4. Auto reconnect เมื่อหลุด
///
/// ทำไมใช้ useRef:
///   connection ต้องเก็บข้าม re-render
///   ถ้าใช้ useState → re-render → สร้าง connection ใหม่!
///   useRef → เก็บ value โดยไม่ trigger re-render
/// </summary>
export function useSignalR() {
  const connectionRef = useRef<signalR.HubConnection | null>(null);
  const queryClient = useQueryClient();

  useEffect(() => {
    const token = Cookies.get("accessToken");
    if (!token) return;

    const apiUrl = process.env.NEXT_PUBLIC_API_URL || "https://localhost:7001";

    // สร้าง connection
    const connection = new signalR.HubConnectionBuilder()
      .withUrl(`${apiUrl}/hubs/notifications`, {
        accessTokenFactory: () => token,
      })
      .withAutomaticReconnect([0, 2000, 5000, 10000, 30000]) // retry: ทันที, 2s, 5s, 10s, 30s
      .configureLogging(signalR.LogLevel.Information)
      .build();

    // ===== Event: Balance Updated =====
    connection.on("BalanceUpdated", (data) => {
      console.log("BalanceUpdated:", data);

      // Invalidate queries → React Query จะ refetch
      queryClient.invalidateQueries({ queryKey: ["balance", data.accountId] });
      queryClient.invalidateQueries({ queryKey: ["accounts"] });
    });

    // ===== Event: Transaction Completed =====
    connection.on("TransactionCompleted", (data) => {
      console.log("TransactionCompleted:", data);

      queryClient.invalidateQueries({ queryKey: ["transactions"] });

      // TODO: แสดง toast notification
      // toast({ title: `${data.type}: ${data.amount} THB`, ... })
    });

    // ===== Event: Reconnected =====
    connection.onreconnected(() => {
      console.log("SignalR reconnected");
      // Refetch ทุกอย่างหลัง reconnect (อาจพลาด events ระหว่าง disconnect)
      queryClient.invalidateQueries();
    });

    // เริ่ม connection
    connection
      .start()
      .then(() => console.log("SignalR connected"))
      .catch((err) => console.error("SignalR connection error:", err));

    connectionRef.current = connection;

    // Cleanup: หยุด connection เมื่อ component unmount
    return () => {
      connection.stop();
    };
  }, []); // eslint-disable-line react-hooks/exhaustive-deps

  return connectionRef.current;
}
```

---

## ขั้นตอนที่ 9: Providers

```
📁 src/providers/

ทำไม: Wrap app ด้วย providers ที่จำเป็น
React Query, Theme, SignalR ต้อง provide context ให้ทั้ง app
```

```typescript
// src/providers/query-provider.tsx

"use client";

import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { ReactQueryDevtools } from "@tanstack/react-query-devtools";
import { useState } from "react";

/// <summary>
/// React Query Provider
///
/// QueryClient: ตั้งค่า default behavior
///   staleTime: 60s → data ถือว่า "สด" 1 นาที (ไม่ refetch)
///   gcTime: 5 นาที → เก็บ cache 5 นาทีหลังไม่มีใครใช้
///   retry: 1 → retry 1 ครั้งเมื่อ error
///   refetchOnWindowFocus: true → refetch เมื่อกลับมาที่ tab
/// </summary>
export function QueryProvider({ children }: { children: React.ReactNode }) {
  const [queryClient] = useState(
    () =>
      new QueryClient({
        defaultOptions: {
          queries: {
            staleTime: 60 * 1000,      // 1 minute
            gcTime: 5 * 60 * 1000,     // 5 minutes
            retry: 1,
            refetchOnWindowFocus: true,
          },
        },
      })
  );

  return (
    <QueryClientProvider client={queryClient}>
      {children}
      <ReactQueryDevtools initialIsOpen={false} />
    </QueryClientProvider>
  );
}
```

```typescript
// src/providers/signalr-provider.tsx

"use client";

import { useSignalR } from "@/lib/hooks/use-signalr";
import { useAuthStore } from "@/lib/stores/auth-store";

/// <summary>
/// SignalR Provider — เชื่อมต่อ SignalR เมื่อ user login แล้ว
///
/// ทำไมแยกเป็น Provider:
///   SignalR connection ควรมีแค่ 1 ต่อ app
///   ถ้าใส่ใน component → สร้าง connection ทุกครั้งที่ render
///   Provider อยู่ root → สร้าง 1 ครั้ง ใช้ทั้ง app
/// </summary>
export function SignalRProvider({ children }: { children: React.ReactNode }) {
  const isAuthenticated = useAuthStore((state) => state.isAuthenticated);

  // เชื่อมต่อ SignalR เฉพาะเมื่อ login แล้ว
  if (isAuthenticated) {
    return <SignalRConnector>{children}</SignalRConnector>;
  }

  return <>{children}</>;
}

function SignalRConnector({ children }: { children: React.ReactNode }) {
  useSignalR(); // เชื่อมต่อ SignalR Hub
  return <>{children}</>;
}
```

---

## ขั้นตอนที่ 10: Root Layout + Middleware

### 10.1 Root Layout

```typescript
// src/app/layout.tsx

import type { Metadata } from "next";
import { Inter } from "next/font/google";
import "./globals.css";
import { QueryProvider } from "@/providers/query-provider";
import { SignalRProvider } from "@/providers/signalr-provider";
import { Toaster } from "@/components/ui/toaster";

const inter = Inter({ subsets: ["latin"] });

export const metadata: Metadata = {
  title: "Banking System",
  description: "Secure online banking platform",
};

/// <summary>
/// Root Layout — ครอบทุก page
///
/// ลำดับ Providers สำคัญ:
///   QueryProvider → ให้ React Query context
///     SignalRProvider → ใช้ React Query (invalidateQueries)
///       {children} → pages
///   Toaster → toast notifications (อยู่นอก providers ก็ได้)
/// </summary>
export default function RootLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  return (
    <html lang="en">
      <body className={inter.className}>
        <QueryProvider>
          <SignalRProvider>
            {children}
          </SignalRProvider>
        </QueryProvider>
        <Toaster />
      </body>
    </html>
  );
}
```

### 10.2 Next.js Middleware (Auth Guard)

```typescript
// src/middleware.ts

import { NextResponse } from "next/server";
import type { NextRequest } from "next/server";

/// <summary>
/// Next.js Middleware — ป้องกัน route ก่อน render
///
/// ทำงานที่ Edge (ก่อนถึง page component):
///   /dashboard → เช็ค token → ถ้าไม่มี → redirect /login
///   /login → เช็ค token → ถ้ามี → redirect /dashboard
///
/// ทำไมใช้ Middleware แทน useEffect ใน page:
///   useEffect: render page ก่อน → เช็ค → redirect (เห็น flash)
///   Middleware: เช็คก่อน render → redirect ทันที (ไม่เห็น flash)
/// </summary>
export function middleware(request: NextRequest) {
  const token = request.cookies.get("accessToken")?.value;
  const { pathname } = request.nextUrl;

  // Protected routes — ต้อง login
  const protectedPaths = [
    "/dashboard",
    "/accounts",
    "/deposit",
    "/withdraw",
    "/transfer",
    "/transactions",
    "/settings",
    "/admin",
  ];

  const isProtected = protectedPaths.some((path) =>
    pathname.startsWith(path)
  );

  // ไม่มี token + เข้า protected route → redirect login
  if (isProtected && !token) {
    const loginUrl = new URL("/login", request.url);
    loginUrl.searchParams.set("callbackUrl", pathname);
    return NextResponse.redirect(loginUrl);
  }

  // มี token + เข้า auth pages → redirect dashboard
  const authPaths = ["/login", "/register"];
  if (authPaths.includes(pathname) && token) {
    return NextResponse.redirect(new URL("/dashboard", request.url));
  }

  return NextResponse.next();
}

export const config = {
  matcher: [
    // Match ทุก path ยกเว้น static files, api, _next
    "/((?!api|_next/static|_next/image|favicon.ico).*)",
  ],
};
```

---

## ขั้นตอนที่ 11: สร้างหน้าหลัก

### 11.1 Login Page

```typescript
// src/app/(auth)/login/page.tsx

"use client";

import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { loginSchema, type LoginFormValues } from "@/lib/validations/auth";
import { useLogin } from "@/lib/hooks/use-auth";
import Link from "next/link";

import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Alert, AlertDescription } from "@/components/ui/alert";

export default function LoginPage() {
  const login = useLogin();

  const form = useForm<LoginFormValues>({
    resolver: zodResolver(loginSchema),
    defaultValues: { email: "", password: "" },
  });

  const onSubmit = (data: LoginFormValues) => {
    login.mutate(data);
  };

  return (
    <div className="flex min-h-screen items-center justify-center bg-gray-50 px-4">
      <Card className="w-full max-w-md">
        <CardHeader className="text-center">
          <CardTitle className="text-2xl font-bold">Welcome Back</CardTitle>
          <CardDescription>Sign in to your banking account</CardDescription>
        </CardHeader>
        <CardContent>
          <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-4">
            {/* Error Alert */}
            {login.isError && (
              <Alert variant="destructive">
                <AlertDescription>
                  {(login.error as any)?.response?.data?.message ||
                    "Login failed. Please try again."}
                </AlertDescription>
              </Alert>
            )}

            {/* Email */}
            <div className="space-y-2">
              <Label htmlFor="email">Email</Label>
              <Input
                id="email"
                type="email"
                placeholder="your@email.com"
                {...form.register("email")}
              />
              {form.formState.errors.email && (
                <p className="text-sm text-red-500">
                  {form.formState.errors.email.message}
                </p>
              )}
            </div>

            {/* Password */}
            <div className="space-y-2">
              <Label htmlFor="password">Password</Label>
              <Input
                id="password"
                type="password"
                placeholder="Enter your password"
                {...form.register("password")}
              />
              {form.formState.errors.password && (
                <p className="text-sm text-red-500">
                  {form.formState.errors.password.message}
                </p>
              )}
            </div>

            {/* Submit */}
            <Button
              type="submit"
              className="w-full"
              disabled={login.isPending}
            >
              {login.isPending ? "Signing in..." : "Sign In"}
            </Button>
          </form>

          <p className="mt-4 text-center text-sm text-gray-600">
            Don&apos;t have an account?{" "}
            <Link href="/register" className="text-blue-600 hover:underline">
              Register
            </Link>
          </p>
        </CardContent>
      </Card>
    </div>
  );
}
```

### 11.2 Dashboard Page

```typescript
// src/app/(dashboard)/dashboard/page.tsx

"use client";

import { useAuthStore } from "@/lib/stores/auth-store";
import { useAccounts } from "@/lib/hooks/use-accounts";
import { BalanceCard } from "@/components/dashboard/balance-card";
import { RecentTransactions } from "@/components/dashboard/recent-transactions";
import { QuickActions } from "@/components/dashboard/quick-actions";
import { Skeleton } from "@/components/ui/skeleton";

export default function DashboardPage() {
  const user = useAuthStore((state) => state.user);
  const { data: accounts, isLoading } = useAccounts(user?.id || "");

  if (isLoading) {
    return (
      <div className="space-y-6 p-6">
        <Skeleton className="h-8 w-64" />
        <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-3">
          <Skeleton className="h-36" />
          <Skeleton className="h-36" />
          <Skeleton className="h-36" />
        </div>
      </div>
    );
  }

  return (
    <div className="space-y-6 p-6">
      {/* Header */}
      <div>
        <h1 className="text-3xl font-bold">
          Welcome, {user?.fullName}
        </h1>
        <p className="text-gray-500">
          Here&apos;s your financial overview
        </p>
      </div>

      {/* Balance Cards */}
      <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-3">
        {accounts?.map((account) => (
          <BalanceCard key={account.id} account={account} />
        ))}
      </div>

      {/* Quick Actions */}
      <QuickActions />

      {/* Recent Transactions */}
      {accounts && accounts.length > 0 && (
        <RecentTransactions accountId={accounts[0].id} />
      )}
    </div>
  );
}
```

### 11.3 Balance Card Component

```typescript
// src/components/dashboard/balance-card.tsx

"use client";

import { useBalance } from "@/lib/hooks/use-accounts";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import type { Account } from "@/lib/types/transaction";

/// <summary>
/// Balance Card — แสดงยอดเงินของบัญชี
///
/// ใช้ useBalance hook → ดึงจาก Redis cache (เร็ว < 1ms)
/// + SignalR จะ invalidate query เมื่อ balance เปลี่ยน
/// → ยอดเงินอัปเดต real-time ไม่ต้อง refresh
/// </summary>
export function BalanceCard({ account }: { account: Account }) {
  const { data: balance } = useBalance(account.id);

  const displayBalance = balance?.balance ?? account.balance;
  const displayAvailable = balance?.availableBalance ?? account.availableBalance;

  return (
    <Card>
      <CardHeader className="flex flex-row items-center justify-between pb-2">
        <CardTitle className="text-sm font-medium text-gray-500">
          {account.type} Account
        </CardTitle>
        <Badge variant={account.status === "Active" ? "default" : "destructive"}>
          {account.status}
        </Badge>
      </CardHeader>
      <CardContent>
        <div className="text-2xl font-bold">
          {formatCurrency(displayBalance)}
        </div>
        <p className="text-xs text-gray-500 mt-1">
          Available: {formatCurrency(displayAvailable)}
        </p>
        <p className="text-xs text-gray-400 mt-2">
          {account.accountNumber}
        </p>
      </CardContent>
    </Card>
  );
}

function formatCurrency(amount: number): string {
  return new Intl.NumberFormat("th-TH", {
    style: "currency",
    currency: "THB",
  }).format(amount);
}
```

### 11.4 Quick Actions Component

```typescript
// src/components/dashboard/quick-actions.tsx

"use client";

import Link from "next/link";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { ArrowDownToLine, ArrowUpFromLine, ArrowLeftRight, History } from "lucide-react";

export function QuickActions() {
  const actions = [
    { label: "Deposit",      href: "/deposit",      icon: ArrowDownToLine, color: "text-green-600" },
    { label: "Withdraw",     href: "/withdraw",     icon: ArrowUpFromLine, color: "text-red-600" },
    { label: "Transfer",     href: "/transfer",     icon: ArrowLeftRight,  color: "text-blue-600" },
    { label: "Transactions", href: "/transactions", icon: History,         color: "text-gray-600" },
  ];

  return (
    <Card>
      <CardHeader>
        <CardTitle>Quick Actions</CardTitle>
      </CardHeader>
      <CardContent>
        <div className="grid grid-cols-2 gap-4 md:grid-cols-4">
          {actions.map((action) => (
            <Link key={action.href} href={action.href}>
              <Button
                variant="outline"
                className="flex h-24 w-full flex-col items-center justify-center gap-2"
              >
                <action.icon className={`h-6 w-6 ${action.color}`} />
                <span>{action.label}</span>
              </Button>
            </Link>
          ))}
        </div>
      </CardContent>
    </Card>
  );
}
```

### 11.5 Deposit Page (ตัวอย่าง Transaction Form)

```typescript
// src/app/(dashboard)/deposit/page.tsx

"use client";

import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { depositSchema, type DepositFormValues } from "@/lib/validations/transaction";
import { useDeposit } from "@/lib/hooks/use-transactions";
import { useAccounts } from "@/lib/hooks/use-accounts";
import { useAuthStore } from "@/lib/stores/auth-store";

import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from "@/components/ui/card";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { CheckCircle } from "lucide-react";

export default function DepositPage() {
  const user = useAuthStore((state) => state.user);
  const { data: accounts } = useAccounts(user?.id || "");
  const deposit = useDeposit();

  const form = useForm<DepositFormValues>({
    resolver: zodResolver(depositSchema),
    defaultValues: { accountId: "", amount: 0, description: "" },
  });

  const onSubmit = (data: DepositFormValues) => {
    deposit.mutate(data, {
      onSuccess: () => {
        form.reset();
      },
    });
  };

  return (
    <div className="mx-auto max-w-lg p-6">
      <Card>
        <CardHeader>
          <CardTitle>Deposit Money</CardTitle>
          <CardDescription>Add funds to your account</CardDescription>
        </CardHeader>
        <CardContent>
          {/* Success Message */}
          {deposit.isSuccess && (
            <Alert className="mb-4 border-green-200 bg-green-50">
              <CheckCircle className="h-4 w-4 text-green-600" />
              <AlertDescription className="text-green-800">
                Deposit of{" "}
                {formatCurrency(deposit.data?.data?.data?.amount || 0)}{" "}
                completed successfully!
              </AlertDescription>
            </Alert>
          )}

          {/* Error Message */}
          {deposit.isError && (
            <Alert variant="destructive" className="mb-4">
              <AlertDescription>
                {(deposit.error as any)?.response?.data?.message ||
                  "Deposit failed. Please try again."}
              </AlertDescription>
            </Alert>
          )}

          <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-4">
            {/* Account Select */}
            <div className="space-y-2">
              <Label>Account</Label>
              <select
                className="w-full rounded-md border p-2"
                {...form.register("accountId")}
              >
                <option value="">Select an account</option>
                {accounts?.map((acc) => (
                  <option key={acc.id} value={acc.id}>
                    {acc.accountNumber} ({acc.type}) —{" "}
                    {formatCurrency(acc.balance)}
                  </option>
                ))}
              </select>
              {form.formState.errors.accountId && (
                <p className="text-sm text-red-500">
                  {form.formState.errors.accountId.message}
                </p>
              )}
            </div>

            {/* Amount */}
            <div className="space-y-2">
              <Label>Amount (THB)</Label>
              <Input
                type="number"
                step="0.01"
                placeholder="0.00"
                {...form.register("amount", { valueAsNumber: true })}
              />
              {form.formState.errors.amount && (
                <p className="text-sm text-red-500">
                  {form.formState.errors.amount.message}
                </p>
              )}
            </div>

            {/* Description */}
            <div className="space-y-2">
              <Label>Description (Optional)</Label>
              <Input
                placeholder="e.g., Monthly savings"
                {...form.register("description")}
              />
            </div>

            <Button
              type="submit"
              className="w-full"
              disabled={deposit.isPending}
            >
              {deposit.isPending ? "Processing..." : "Deposit"}
            </Button>
          </form>
        </CardContent>
      </Card>
    </div>
  );
}

function formatCurrency(amount: number): string {
  return new Intl.NumberFormat("th-TH", {
    style: "currency",
    currency: "THB",
  }).format(amount);
}
```

---

## ขั้นตอนที่ 12: Dashboard Layout (Sidebar + Header)

```typescript
// src/app/(dashboard)/layout.tsx

"use client";

import { Sidebar } from "@/components/layout/sidebar";
import { Header } from "@/components/layout/header";

/// <summary>
/// Dashboard Layout — ใช้กับทุก page ที่ต้อง login
///
/// โครงสร้าง:
/// ┌──────────┬────────────────────────────────┐
/// │          │  Header (user menu, search)     │
/// │ Sidebar  ├────────────────────────────────┤
/// │ (nav)    │                                │
/// │          │  Page Content                  │
/// │          │  {children}                    │
/// │          │                                │
/// └──────────┴────────────────────────────────┘
/// </summary>
export default function DashboardLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  return (
    <div className="flex h-screen">
      {/* Sidebar — ซ่อนบน mobile */}
      <Sidebar className="hidden md:flex" />

      {/* Main Content */}
      <div className="flex flex-1 flex-col overflow-hidden">
        <Header />
        <main className="flex-1 overflow-y-auto bg-gray-50">
          {children}
        </main>
      </div>
    </div>
  );
}
```

```typescript
// src/components/layout/sidebar.tsx

"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { cn } from "@/lib/utils";
import {
  LayoutDashboard, Wallet, ArrowDownToLine,
  ArrowUpFromLine, ArrowLeftRight, History,
  Settings, ShieldCheck,
} from "lucide-react";

const navItems = [
  { label: "Dashboard",    href: "/dashboard",    icon: LayoutDashboard },
  { label: "Accounts",     href: "/accounts",     icon: Wallet },
  { label: "Deposit",      href: "/deposit",      icon: ArrowDownToLine },
  { label: "Withdraw",     href: "/withdraw",     icon: ArrowUpFromLine },
  { label: "Transfer",     href: "/transfer",     icon: ArrowLeftRight },
  { label: "Transactions", href: "/transactions", icon: History },
  { label: "Settings",     href: "/settings",     icon: Settings },
  { label: "Admin",        href: "/admin",        icon: ShieldCheck },
];

export function Sidebar({ className }: { className?: string }) {
  const pathname = usePathname();

  return (
    <aside className={cn("flex w-64 flex-col border-r bg-white", className)}>
      {/* Logo */}
      <div className="flex h-16 items-center border-b px-6">
        <Link href="/dashboard" className="text-xl font-bold text-blue-600">
          BankingApp
        </Link>
      </div>

      {/* Navigation */}
      <nav className="flex-1 space-y-1 p-4">
        {navItems.map((item) => {
          const isActive = pathname.startsWith(item.href);
          return (
            <Link
              key={item.href}
              href={item.href}
              className={cn(
                "flex items-center gap-3 rounded-lg px-3 py-2 text-sm transition-colors",
                isActive
                  ? "bg-blue-50 text-blue-600 font-medium"
                  : "text-gray-600 hover:bg-gray-100"
              )}
            >
              <item.icon className="h-4 w-4" />
              {item.label}
            </Link>
          );
        })}
      </nav>
    </aside>
  );
}
```

```typescript
// src/components/layout/header.tsx

"use client";

import { useAuthStore } from "@/lib/stores/auth-store";
import { useLogout } from "@/lib/hooks/use-auth";
import { Button } from "@/components/ui/button";
import { Avatar, AvatarFallback } from "@/components/ui/avatar";
import {
  DropdownMenu, DropdownMenuContent,
  DropdownMenuItem, DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { LogOut, User, Settings } from "lucide-react";
import Link from "next/link";

export function Header() {
  const user = useAuthStore((state) => state.user);
  const logout = useLogout();

  const initials = user?.fullName
    ?.split(" ")
    .map((n) => n[0])
    .join("")
    .toUpperCase() || "U";

  return (
    <header className="flex h-16 items-center justify-between border-b bg-white px-6">
      <div />

      {/* User Menu */}
      <DropdownMenu>
        <DropdownMenuTrigger asChild>
          <Button variant="ghost" className="flex items-center gap-2">
            <Avatar className="h-8 w-8">
              <AvatarFallback>{initials}</AvatarFallback>
            </Avatar>
            <span className="hidden md:inline">{user?.fullName}</span>
          </Button>
        </DropdownMenuTrigger>
        <DropdownMenuContent align="end">
          <DropdownMenuItem asChild>
            <Link href="/settings">
              <User className="mr-2 h-4 w-4" />
              Profile
            </Link>
          </DropdownMenuItem>
          <DropdownMenuItem asChild>
            <Link href="/settings">
              <Settings className="mr-2 h-4 w-4" />
              Settings
            </Link>
          </DropdownMenuItem>
          <DropdownMenuItem
            onClick={() => logout.mutate()}
            className="text-red-600"
          >
            <LogOut className="mr-2 h-4 w-4" />
            Logout
          </DropdownMenuItem>
        </DropdownMenuContent>
      </DropdownMenu>
    </header>
  );
}
```

---

## ขั้นตอนที่ 13: Environment Variables

```
📁 banking-frontend/.env.local

ทำไม: แยก config ออกจาก code
เปลี่ยน API URL ได้โดยไม่ต้องแก้ code
```

```bash
# .env.local — ไม่ commit ลง git

NEXT_PUBLIC_API_URL=https://localhost:7001/api
```

```bash
# .env.production

NEXT_PUBLIC_API_URL=https://api.yourbank.com/api
```

```
NEXT_PUBLIC_ prefix:
  ตัวแปรที่ขึ้นต้นด้วย NEXT_PUBLIC_ จะถูก expose ให้ฝั่ง client
  ตัวแปรที่ไม่มี prefix จะใช้ได้แค่ฝั่ง server (API routes, middleware)
```

---

## ขั้นตอนที่ 14: ทดสอบ

### 14.1 Run Frontend + Backend

```bash
# Terminal 1: Run Backend
cd BankingSystem
dotnet run --project Banking.Api

# Terminal 2: Run Frontend
cd banking-frontend
npm run dev

# เปิด browser: http://localhost:3000
```

### 14.2 ทดสอบ Flow

```
1. Register:
   เข้า /register → กรอกข้อมูล → สมัคร
   → redirect ไป /dashboard

2. Login:
   เข้า /login → ใส่ email/password
   → redirect ไป /dashboard
   → เห็น balance card + quick actions

3. Deposit:
   คลิก Deposit → เลือกบัญชี → ใส่จำนวน → กด Deposit
   → เห็น success message
   → Balance card อัปเดตทันที (SignalR)

4. Withdraw:
   คลิก Withdraw → เลือกบัญชี → ใส่จำนวน → กด Withdraw
   → ถ้าเงินไม่พอ → เห็น error message
   → ถ้าสำเร็จ → Balance อัปเดต

5. Transfer:
   คลิก Transfer → เลือกบัญชีต้นทาง + ปลายทาง → ใส่จำนวน
   → ถ้าบัญชีเดียวกัน → เห็น error
   → ถ้าสำเร็จ → Balance ทั้ง 2 บัญชีอัปเดต

6. Transaction History:
   คลิก Transactions → เห็นรายการ transaction ทั้งหมด
   → pagination (เลื่อนหน้า)

7. Logout:
   คลิก avatar → Logout → redirect ไป /login
   → กดปุ่ม back → ไม่กลับไป dashboard (middleware redirect)

8. Auth Guard:
   เข้า /dashboard โดยไม่ login → redirect ไป /login
   Login แล้วเข้า /login → redirect ไป /dashboard
```

---

## Checklist — สิ่งที่ต้องเสร็จก่อนไป Phase 5

```
Setup:
☐ Next.js 15 project สร้างแล้ว (App Router + TypeScript)
☐ shadcn/ui init + components ติดตั้งแล้ว
☐ Dependencies ครบ: react-query, zustand, axios, zod, react-hook-form, signalr
☐ Folder structure จัดแล้ว (app, components, lib, providers)

Types & Validation:
☐ TypeScript types ตรงกับ API response (api.ts, auth.ts, transaction.ts)
☐ Zod schemas สำหรับ login, register, deposit, withdraw, transfer

API Layer:
☐ Axios client + interceptors (token, 401 redirect)
☐ API functions: auth, accounts, transactions

State Management:
☐ Zustand auth store (setAuth, clearAuth, initialize)
☐ React Query hooks: useLogin, useRegister, useLogout, useProfile
☐ React Query hooks: useAccounts, useBalance, useAccount
☐ React Query hooks: useDeposit, useWithdraw, useTransfer, useTransactionHistory
☐ React Query provider + config (staleTime, gcTime)

Real-time:
☐ useSignalR hook — เชื่อมต่อ Hub + event handlers
☐ SignalR Provider — เชื่อมต่อเมื่อ login
☐ BalanceUpdated → invalidate balance query
☐ TransactionCompleted → invalidate transactions query

Pages:
☐ Landing page (/)
☐ Login page (/login)
☐ Register page (/register)
☐ Dashboard (/dashboard) — balance cards, quick actions, recent transactions
☐ Accounts (/accounts) — รายการบัญชี
☐ Account Detail (/accounts/[id]) — รายละเอียด + statement
☐ Deposit (/deposit) — form ฝากเงิน
☐ Withdraw (/withdraw) — form ถอนเงิน
☐ Transfer (/transfer) — form โอนเงิน
☐ Transactions (/transactions) — ประวัติ + pagination
☐ Settings (/settings) — profile
☐ Admin (/admin) — dashboard สถิติ

Components:
☐ Sidebar + Header layout
☐ Balance Card (real-time)
☐ Quick Actions
☐ Transaction List / Item
☐ Loading states (Skeleton)
☐ Error states (Alert)

Security:
☐ Next.js Middleware (auth guard)
☐ Token ใน Cookie (ไม่ใช่ localStorage)
☐ 401 interceptor → redirect login
☐ Protected routes → redirect login ถ้าไม่มี token

Testing:
☐ npm run dev ทำงาน (0 errors)
☐ Register → Login → Dashboard → Deposit → Withdraw → Transfer
☐ Auth guard: เข้า /dashboard ไม่ login → redirect /login
☐ Real-time: ฝากเงิน → balance card อัปเดตทันที
☐ Logout → token ถูกลบ → ไม่กลับ dashboard ได้

เมื่อ checklist ครบ → พร้อมไป Phase 5: Load Balancing & Scaling
```

---

## Troubleshooting

### "CORS error" เมื่อ frontend เรียก API
```
Backend Program.cs ต้องมี:
  .WithOrigins("http://localhost:3000")
  .AllowCredentials()  // สำหรับ SignalR

ตรวจว่า NEXT_PUBLIC_API_URL ตรงกับ URL ของ API
```

### "Hydration mismatch" error
```
ปัญหา: Server render ≠ Client render
สาเหตุ: ใช้ Cookies.get() ตอน render (server ไม่มี cookie)
แก้: ใช้ useEffect เพื่ออ่าน cookie ฝั่ง client เท่านั้น
หรือใช้ "use client" directive
```

### React Query ไม่ refetch หลัง mutation
```
ตรวจว่า onSuccess ใน mutation มี:
  queryClient.invalidateQueries({ queryKey: ["accounts"] })

invalidateQueries → mark cache เป็น stale → trigger refetch
```

### SignalR เชื่อมต่อไม่ได้
```
1. ตรวจว่า backend มี: app.MapHub<NotificationHub>("/hubs/notifications")
2. ตรวจว่า CORS AllowCredentials() เปิดอยู่
3. ตรวจว่า JWT token ส่งผ่าน query string:
   accessTokenFactory: () => token
4. ตรวจว่า backend มี OnMessageReceived event handler ใน JWT config
```

### "Module not found: @/components/ui/..."
```
shadcn/ui components ยังไม่ได้ติดตั้ง:
  npx shadcn@latest add button card input ...

ตรวจว่า tsconfig.json มี paths:
  "@/*": ["./src/*"]
```

### Token หมดอายุแล้วแต่ไม่ redirect
```
ตรวจว่า Axios interceptor จับ 401:
  if (error.response?.status === 401) { ... redirect ... }

ตรวจว่า Cookie ตั้ง expires ถูกต้อง:
  accessToken: 15 minutes = 1/96 days
```

### Build error: "useEffect is not defined"
```
Component ใช้ hooks แต่ไม่มี "use client" directive
เพิ่ม "use client" บรรทัดแรกของไฟล์
```
