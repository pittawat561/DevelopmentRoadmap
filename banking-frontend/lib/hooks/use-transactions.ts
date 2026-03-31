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