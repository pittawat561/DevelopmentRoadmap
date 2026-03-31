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