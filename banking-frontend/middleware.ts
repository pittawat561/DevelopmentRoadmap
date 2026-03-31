import { NextResponse } from "next/server";
import type { NextRequest } from "next/server";

/// <summary>
/// Next.js Middleware — ป้องกัน route ก่อน render
///
/// ทำงานที่ Edge (ก่อนถึง page component):
///   /dashboard → เช็ค token → ถ้าไม่มี → redirect /login
///   /login → เช็ค token → ถ้ามี → redirect /dashboard
///
/// ทำไมใช้ Middleware แทน useEffect ใน page:
///   useEffect: render page ก่อน → เช็ค → redirect (เห็น flash)
///   Middleware: เช็คก่อน render → redirect ทันที (ไม่เห็น flash)
/// </summary>
export function middleware(request: NextRequest) {
    const token = request.cookies.get("accessToken")?.value;
    const { pathname } = request.nextUrl;

    // Protected routes — ต้อง login
    const protectedPaths = [
        "/dashboard",
        "/accounts",
        "/deposit",
        "/withdraw",
        "/transfer",
        "/transactions",
        "/settings",
        "/admin",
    ];

    const isProtected = protectedPaths.some((path) =>
        pathname.startsWith(path)
    );

    // ไม่มี token + เข้า protected route → redirect login
    if (isProtected && !token) {
        const loginUrl = new URL("/login", request.url);
        loginUrl.searchParams.set("callbackUrl", pathname);
        return NextResponse.redirect(loginUrl);
    }

    // มี token + เข้า auth pages → redirect dashboard
    const authPaths = ["/login", "/register"];
    if (authPaths.includes(pathname) && token) {
        return NextResponse.redirect(new URL("/dashboard", request.url));
    }

    return NextResponse.next();
}

export const config = {
    matcher: [
        // Match ทุก path ยกเว้น static files, api, _next
        "/((?!api|_next/static|_next/image|favicon.ico).*)",
    ],
};