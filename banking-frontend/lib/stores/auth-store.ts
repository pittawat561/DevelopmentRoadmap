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