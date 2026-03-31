/// <summary>
/// API Response มาตรฐานจาก Backend — ตรงกับ ApiResponse<T> ใน C#
/// </summary>
export interface ApiResponse<T> {
    success: boolean;
    message: string;
    data?: T;
}

/// <summary>
/// Paged Response — ตรงกับ PagedResponse<T> ใน C#
/// </summary>
export interface PagedResponse<T> {
    items: T[];
    totalCount: number;
    page: number;
    pageSize: number;
    totalPages: number;
}

/// <summary>
/// API Error — สำหรับ catch block
/// </summary>
export interface ApiError {
    success: false;
    message: string;
    statusCode: number;
}