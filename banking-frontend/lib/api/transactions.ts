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