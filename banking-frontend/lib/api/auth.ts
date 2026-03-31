import apiClient from "./client";
import type { ApiResponse } from "@/lib/types/api";
import type { AuthResponse, LoginRequest, RegisterRequest, User } from "@/lib/types/auth";

export const authApi = {
    register: (data: RegisterRequest) =>
        apiClient.post<ApiResponse<AuthResponse>>("/auth/register", data),

    login: (data: LoginRequest) =>
        apiClient.post<ApiResponse<AuthResponse>>("/auth/login", data),

    getProfile: () =>
        apiClient.get<ApiResponse<User>>("/auth/profile"),

    logout: () =>
        apiClient.post<ApiResponse<null>>("/auth/logout"),
};