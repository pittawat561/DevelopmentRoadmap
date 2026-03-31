namespace Banking.Application.DTOs;

/// <summary>
/// ตั้ง PIN ครั้งแรก หรือเปลี่ยน PIN
/// </summary>
public record SetPinRequest(
    string Pin,
    string ConfirmPin
);

/// <summary>
/// เปลี่ยน PIN — ต้องใส่ PIN เก่าด้วย
/// </summary>
public record ChangePinRequest(
    string CurrentPin,
    string NewPin,
    string ConfirmNewPin
);

/// <summary>
/// ยืนยัน PIN ก่อนทำธุรกรรม — เพิ่มใน request เดิม
/// </summary>
public record DepositWithPinRequest(
    Guid AccountId,
    decimal Amount,
    string? Description,
    string Pin
);

public record WithdrawWithPinRequest(
    Guid AccountId,
    decimal Amount,
    string? Description,
    string Pin
);

public record TransferWithPinRequest(
    Guid FromAccountId,
    Guid ToAccountId,
    decimal Amount,
    string? Description,
    string Pin
);