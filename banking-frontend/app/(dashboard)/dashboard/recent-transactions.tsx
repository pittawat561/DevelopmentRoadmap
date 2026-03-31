"use client";

import { useTransactionHistory } from "@/lib/hooks/use-transactions";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Skeleton } from "@/components/ui/skeleton";

/// <summary>
/// RecentTransactions — แสดง 5 รายการล่าสุดของบัญชี
///
/// ใช้ useTransactionHistory hook → ดึงจาก API (page 1, size 5)
/// + SignalR invalidate → อัปเดตอัตโนมัติเมื่อมี transaction ใหม่
/// </summary>
export function RecentTransactions({ accountId }: { accountId: string }) {
    const { data, isLoading } = useTransactionHistory(accountId, 1, 5);

    if (isLoading) {
        return (
            <Card>
                <CardHeader>
                    <CardTitle>Recent Transactions</CardTitle>
                </CardHeader>
                <CardContent className="space-y-3">
                    {Array.from({ length: 3 }).map((_, i) => (
                        <Skeleton key={i} className="h-12 w-full" />
                    ))}
                </CardContent>
            </Card>
        );
    }

    const transactions = data?.items ?? [];

    return (
        <Card>
            <CardHeader>
                <CardTitle>Recent Transactions</CardTitle>
            </CardHeader>
            <CardContent>
                {transactions.length === 0 ? (
                    <p className="text-sm text-gray-500">No transactions yet.</p>
                ) : (
                    <div className="space-y-3">
                        {transactions.map((txn) => (
                            <div
                                key={txn.id}
                                className="flex items-center justify-between rounded-lg border p-3"
                            >
                                <div className="flex flex-col">
                                    <span className="text-sm font-medium">
                                        {txn.type}
                                    </span>
                                    <span className="text-xs text-gray-500">
                                        {new Date(txn.createdAt).toLocaleDateString("th-TH", {
                                            day: "numeric",
                                            month: "short",
                                            year: "numeric",
                                            hour: "2-digit",
                                            minute: "2-digit",
                                        })}
                                    </span>
                                </div>
                                <div className="flex items-center gap-2">
                                    <span
                                        className={`text-sm font-semibold ${
                                            txn.type === "Deposit" || txn.type === "TransferIn"
                                                ? "text-green-600"
                                                : "text-red-600"
                                        }`}
                                    >
                                        {txn.type === "Deposit" || txn.type === "TransferIn"
                                            ? "+"
                                            : "-"}
                                        {formatCurrency(txn.amount)}
                                    </span>
                                    <Badge
                                        variant={
                                            txn.status === "Completed" ? "default" : "secondary"
                                        }
                                        className="text-xs"
                                    >
                                        {txn.status}
                                    </Badge>
                                </div>
                            </div>
                        ))}
                    </div>
                )}
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
