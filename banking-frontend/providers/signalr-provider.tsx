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