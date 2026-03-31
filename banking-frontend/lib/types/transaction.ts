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