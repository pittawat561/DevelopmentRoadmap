"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { cn } from "@/lib/utils";
import {
    LayoutDashboard, Wallet, ArrowDownToLine,
    ArrowUpFromLine, ArrowLeftRight, History,
    Settings, ShieldCheck,
} from "lucide-react";

const navItems = [
    { label: "Dashboard", href: "/dashboard", icon: LayoutDashboard },
    { label: "Accounts", href: "/accounts", icon: Wallet },
    { label: "Deposit", href: "/deposit", icon: ArrowDownToLine },
    { label: "Withdraw", href: "/withdraw", icon: ArrowUpFromLine },
    { label: "Transfer", href: "/transfer", icon: ArrowLeftRight },
    { label: "Transactions", href: "/transactions", icon: History },
    { label: "Settings", href: "/settings", icon: Settings },
    { label: "Admin", href: "/admin", icon: ShieldCheck },
];

export function Sidebar({ className }: { className?: string }) {
    const pathname = usePathname();

    return (
        <aside className={cn("flex w-64 flex-col border-r bg-white", className)}>
            {/* Logo */}
            <div className="flex h-16 items-center border-b px-6">
                <Link href="/dashboard" className="text-xl font-bold text-blue-600">
                    BankingApp
                </Link>
            </div>

            {/* Navigation */}
            <nav className="flex-1 space-y-1 p-4">
                {navItems.map((item) => {
                    const isActive = pathname.startsWith(item.href);
                    return (
                        <Link
                            key={item.href}
                            href={item.href}
                            className={cn(
                                "flex items-center gap-3 rounded-lg px-3 py-2 text-sm transition-colors",
                                isActive
                                    ? "bg-blue-50 text-blue-600 font-medium"
                                    : "text-gray-600 hover:bg-gray-100"
                            )}
                        >
                            <item.icon className="h-4 w-4" />
                            {item.label}
                        </Link>
                    );
                })}
            </nav>
        </aside>
    );
}