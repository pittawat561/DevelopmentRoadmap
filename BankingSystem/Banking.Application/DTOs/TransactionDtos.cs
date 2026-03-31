namespace Banking.Application.DTOs;

/// <summary>
/// Request ฝากเงิน — client ส่งมา
/// </summary>
public record DepositRequest(
    Guid AccountId,
    decimal Amount,
    string? Description,
    string Pin
);

/// <summary>
/// Request ถอนเงิน — client ส่งมา
/// </summary>
public record WithdrawRequest(
    Guid AccountId,
    decimal Amount,
    string? Description,
    string Pin
);

/// <summary>
/// Request โอนเงิน — client ส่งมา
/// </summary>
public record TransferRequest(
    Guid FromAccountId,
    Guid ToAccountId,
    decimal Amount,
    string? Description,
    string Pin
);

/// <summary>
/// Response ธุรกรรม — ส่งกลับ client
/// ไม่มี PasswordHash, IsDeleted ฯลฯ
/// </summary>
public record TransactionResponse(
    Guid Id,
    string ReferenceNumber,
    string Type,
    decimal Amount,
    decimal BalanceBefore,
    decimal BalanceAfter,
    string Status,
    string? Description,
    DateTime CreatedAt
);

/// <summary>
/// Response บัญชี — ส่งกลับ client
/// </summary>
public record AccountResponse(
    Guid Id,
    string AccountNumber,
    string Type,
    string Currency,
    decimal Balance,
    decimal AvailableBalance,
    string Status,
    DateTime CreatedAt
);

/// <summary>
/// Response แบบมี pagination — สำหรับ list endpoints
/// </summary>
public record PagedResponse<T>(
    List<T> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages
);

/// <summary>
/// Response มาตรฐาน — ใช้กับทุก API
/// </summary>
public record ApiResponse<T>(
    bool Success,
    string Message,
    T? Data = default
);