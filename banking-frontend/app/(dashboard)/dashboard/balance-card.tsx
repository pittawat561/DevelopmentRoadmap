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