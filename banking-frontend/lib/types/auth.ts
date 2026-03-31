export interface User {
    id: string;
    firstName: string;
    lastName: string;
    email: string;
    phone: string;
    kycStatus: "Pending" | "Verified" | "Rejected";
    createdAt: string;
}

export interface AuthResponse {
    userId: string;
    fullName: string;
    email: string;
    accessToken: string;
    refreshToken: string;
    accessTokenExpiry: string;
}

export interface LoginRequest {
    email: string;
    password: string;
}

export interface RegisterRequest {
    firstName: string;
    lastName: string;
    email: string;
    phone: string;
    password: string;
    confirmPassword: string;
}