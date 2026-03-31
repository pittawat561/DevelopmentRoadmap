import axios from "axios";
import Cookies from "js-cookie";

/// <summary>
/// Axios Instance — HTTP client กลางสำหรับทุก API call
///
/// ทำไมใช้ instance แทน axios ตรง:
///   axios.get('/api/...') → ต้องตั้ง baseURL ทุกครั้ง
///   apiClient.get('/accounts') → ใช้ baseURL ที่ตั้งไว้
///
/// Interceptors:
///   Request: แนบ Authorization header อัตโนมัติ
///   Response: จับ 401 → clear token → redirect login
/// </summary>
const apiClient = axios.create({
    baseURL: process.env.NEXT_PUBLIC_API_URL || "https://localhost:7001/api",
    headers: {
        "Content-Type": "application/json",
    },
    timeout: 15000, // 15 seconds
});

// ===== Request Interceptor: แนบ JWT token =====
apiClient.interceptors.request.use(
    (config) => {
        const token = Cookies.get("accessToken");
        if (token) {
            config.headers.Authorization = `Bearer ${token}`;
        }
        return config;
    },
    (error) => Promise.reject(error)
);

// ===== Response Interceptor: จับ error =====
apiClient.interceptors.response.use(
    (response) => response,
    async (error) => {
        // 401 Unauthorized → token หมดอายุหรือถูก revoke
        if (error.response?.status === 401) {
            Cookies.remove("accessToken");
            Cookies.remove("refreshToken");

            // redirect ไป login (ถ้าอยู่ฝั่ง client)
            if (typeof window !== "undefined") {
                window.location.href = "/login";
            }
        }

        return Promise.reject(error);
    }
);

export default apiClient;