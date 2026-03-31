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
}