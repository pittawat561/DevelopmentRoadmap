import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { authApi } from "@/lib/api/auth";
import { useAuthStore } from "@/lib/stores/auth-store";
import { useRouter } from "next/navigation";
import type { LoginRequest, RegisterRequest } from "@/lib/types/auth";

/// <summary>
/// useLogin — mutation สำหรับ login
///
/// useMutation vs useQuery:
///   useQuery = GET (อ่านข้อมูล, auto fetch)
///   useMutation = POST/PUT/DELETE (เปลี่ยนข้อมูล, เรียกเมื่อ submit)
/// </summary>
export function useLogin() {
    const router = useRouter();
    const setAuth = useAuthStore((state) => state.setAuth);

    return useMutation({
        mutationFn: (data: LoginRequest) => authApi.login(data),
        onSuccess: (response) => {
            const auth = response.data.data!;
            setAuth(auth);
            router.push("/dashboard");
        },
    });
}

export function useRegister() {
    const router = useRouter();
    const setAuth = useAuthStore((state) => state.setAuth);

    return useMutation({
        mutationFn: (data: RegisterRequest) => authApi.register(data),
        onSuccess: (response) => {
            const auth = response.data.data!;
            setAuth(auth);
            router.push("/dashboard");
        },
    });
}

export function useLogout() {
    const router = useRouter();
    const clearAuth = useAuthStore((state) => state.clearAuth);
    const queryClient = useQueryClient();

    return useMutation({
        mutationFn: () => authApi.logout(),
        onSuccess: () => {
            clearAuth();
            queryClient.clear(); // ลบ cache ทั้งหมด
            router.push("/login");
        },
    });
}

export function useProfile() {
    return useQuery({
        queryKey: ["profile"],
        queryFn: () => authApi.getProfile().then((res) => res.data.data!),
        retry: false,
    });
}