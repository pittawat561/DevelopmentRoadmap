"use client";

import Link from "next/link";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { ArrowDownToLine, ArrowUpFromLine, ArrowLeftRight, History } from "lucide-react";

export function QuickActions() {
    const actions = [
        { label: "Deposit", href: "/deposit", icon: ArrowDownToLine, color: "text-green-600" },
        { label: "Withdraw", href: "/withdraw", icon: ArrowUpFromLine, color: "text-red-600" },
        { label: "Transfer", href: "/transfer", icon: ArrowLeftRight, color: "text-blue-600" },
        { label: "Transactions", href: "/transactions", icon: History, color: "text-gray-600" },
    ];

    return (
        <Card>
            <CardHeader>
                <CardTitle>Quick Actions</CardTitle>
            </CardHeader>
            <CardContent>
                <div className="grid grid-cols-2 gap-4 md:grid-cols-4">
                    {actions.map((action) => (
                        <Link key={action.href} href={action.href}>
                            <Button
                                variant="outline"
                                className="flex h-24 w-full flex-col items-center justify-center gap-2"
                            >
                                <action.icon className={`h-6 w-6 ${action.color}`} />
                                <span>{action.label}</span>
                            </Button>
                        </Link>
                    ))}
                </div>
            </CardContent>
        </Card>
    );
}