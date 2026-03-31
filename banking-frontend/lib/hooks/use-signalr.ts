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