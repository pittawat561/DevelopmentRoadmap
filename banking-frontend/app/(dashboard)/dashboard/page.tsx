"use client";

import { useAuthStore } from "@/lib/stores/auth-store";
import { useAccounts } from "@/lib/hooks/use-accounts";
import { BalanceCard } from "@/app/(dashboard)/dashboard/balance-card";
import { RecentTransactions } from "@/app/(dashboard)/dashboard/recent-transactions";
import { QuickActions } from "@/app/(dashboard)/dashboard/quick-actions";
import { Skeleton } from "@/components/ui/skeleton";

export default function DashboardPage() {
    const user = useAuthStore((state) => state.user);
    const { data: accounts, isLoading } = useAccounts(user?.id || "");

    if (isLoading) {
        return (
            <div className="space-y-6 p-6">
                <Skeleton className="h-8 w-64" />
                <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-3">
                    <Skeleton className="h-36" />
                    <Skeleton className="h-36" />
                    <Skeleton className="h-36" />
                </div>
            </div>
        );
    }

    return (
        <div className="space-y-6 p-6">
            {/* Header */}
            <div>
                <h1 className="text-3xl font-bold">
                    Welcome, {user?.fullName}
                </h1>
                <p className="text-gray-500">
                    Here&apos;s your financial overview
                </p>
            </div>

            {/* Balance Cards */}
            <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-3">
                {accounts?.map((account) => (
                    <BalanceCard key={account.id} account={account} />
                ))}
            </div>

            {/* Quick Actions */}
            <QuickActions />

            {/* Recent Transactions */}
            {accounts && accounts.length > 0 && (
                <RecentTransactions accountId={accounts[0].id} />
            )}
        </div>
    );
}