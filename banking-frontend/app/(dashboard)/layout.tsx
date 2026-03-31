"use client";

import { Sidebar } from "@/components/layout/sidebar";
import { Header } from "@/components/layout/header";

/// <summary>
/// Dashboard Layout — ใช้กับทุก page ที่ต้อง login
///
/// โครงสร้าง:
/// ┌──────────┬────────────────────────────────┐
/// │          │  Header (user menu, search)     │
/// │ Sidebar  ├────────────────────────────────┤
/// │ (nav)    │                                │
/// │          │  Page Content                  │
/// │          │  {children}                    │
/// │          │                                │
/// └──────────┴────────────────────────────────┘
/// </summary>
export default function DashboardLayout({
    children,
}: {
    children: React.ReactNode;
}) {
    return (
        <div className="flex h-screen">
            {/* Sidebar — ซ่อนบน mobile */}
            <Sidebar className="hidden md:flex" />

            {/* Main Content */}
            <div className="flex flex-1 flex-col overflow-hidden">
                <Header />
                <main className="flex-1 overflow-y-auto bg-gray-50">
                    {children}
                </main>
            </div>
        </div>
    );
}