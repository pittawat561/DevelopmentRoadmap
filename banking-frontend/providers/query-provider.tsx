"use client";

import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { ReactQueryDevtools } from "@tanstack/react-query-devtools";
import { useState } from "react";

/// <summary>
/// React Query Provider
///
/// QueryClient: ตั้งค่า default behavior
///   staleTime: 60s → data ถือว่า "สด" 1 นาที (ไม่ refetch)
///   gcTime: 5 นาที → เก็บ cache 5 นาทีหลังไม่มีใครใช้
///   retry: 1 → retry 1 ครั้งเมื่อ error
///   refetchOnWindowFocus: true → refetch เมื่อกลับมาที่ tab
/// </summary>
export function QueryProvider({ children }: { children: React.ReactNode }) {
    const [queryClient] = useState(
        () =>
            new QueryClient({
                defaultOptions: {
                    queries: {
                        staleTime: 60 * 1000,      // 1 minute
                        gcTime: 5 * 60 * 1000,     // 5 minutes
                        retry: 1,
                        refetchOnWindowFocus: true,
                    },
                },
            })
    );

    return (
        <QueryClientProvider client={queryClient}>
            {children}
            <ReactQueryDevtools initialIsOpen={false} />
        </QueryClientProvider>
    );
}