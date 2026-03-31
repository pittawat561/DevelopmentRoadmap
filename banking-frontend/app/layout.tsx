import type { Metadata } from "next";
import { Inter } from "next/font/google";
import "./globals.css";
import { QueryProvider } from "@/providers/query-provider";
import { SignalRProvider } from "@/providers/signalr-provider";
import { Toaster } from "@/components/ui/sonner";

const inter = Inter({ subsets: ["latin"] });

export const metadata: Metadata = {
  title: "Banking System",
  description: "Secure online banking platform",
};

/// <summary>
/// Root Layout — ครอบทุก page
///
/// ลำดับ Providers สำคัญ:
///   QueryProvider → ให้ React Query context
///     SignalRProvider → ใช้ React Query (invalidateQueries)
///       {children} → pages
///   Toaster → toast notifications (อยู่นอก providers ก็ได้)
/// </summary>
export default function RootLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  return (
    <html lang="en">
      <body className={inter.className}>
        <QueryProvider>
          <SignalRProvider>
            {children}
          </SignalRProvider>
        </QueryProvider>
        <Toaster />
      </body>
    </html>
  );
}