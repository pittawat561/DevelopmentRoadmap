using Banking.Application.DTOs;
using Banking.Domain.Exceptions;
using Banking.Domain.Interfaces;

namespace Banking.Application.Services;

/// <summary>
/// PIN Service — จัดการ PIN สำหรับ transaction verification
///
/// Flow การใช้ PIN:
///   1. Register → ยังไม่มี PIN
///   2. เข้า Dashboard → ระบบบังคับตั้ง PIN (SetPin)
///   3. ทำธุรกรรม → ต้องใส่ PIN ทุกครั้ง (VerifyPin)
///   4. PIN ผิด 3 ครั้ง → ล็อกธุรกรรม → ต้อง Reset PIN ผ่าน Admin/OTP
/// </summary>
public class PinService
{
    private readonly IUnitOfWork _unitOfWork;
    private const int MaxPinAttempts = 3;

    public PinService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    /// <summary>
    /// ตั้ง PIN ครั้งแรก
    /// </summary>
    public async Task SetPinAsync(Guid userId, SetPinRequest request, CancellationToken ct = default)
    {
        var user = await _unitOfWork.Users.GetByIdAsync(userId, ct)
            ?? throw new NotFoundException("User", userId);

        if (user.PinHash is not null)
            throw new InvalidOperationException("PIN is already set. Use change PIN instead.");

        user.PinHash = BCrypt.Net.BCrypt.HashPassword(request.Pin);
        _unitOfWork.Users.Update(user);
        await _unitOfWork.SaveChangesAsync(ct);
    }

    /// <summary>
    /// เปลี่ยน PIN — ต้องยืนยัน PIN เก่าก่อน
    /// </summary>
    public async Task ChangePinAsync(Guid userId, ChangePinRequest request, CancellationToken ct = default)
    {
        var user = await _unitOfWork.Users.GetByIdAsync(userId, ct)
            ?? throw new NotFoundException("User", userId);

        if (user.PinHash is null)
            throw new InvalidOperationException("PIN is not set yet. Use set PIN first.");

        if (!BCrypt.Net.BCrypt.Verify(request.CurrentPin, user.PinHash))
            throw new ArgumentException("Current PIN is incorrect.");

        user.PinHash = BCrypt.Net.BCrypt.HashPassword(request.NewPin);
        user.FailedPinAttempts = 0;
        _unitOfWork.Users.Update(user);
        await _unitOfWork.SaveChangesAsync(ct);
    }

    /// <summary>
    /// ⚠️ CRITICAL: ตรวจ PIN ก่อนทำธุรกรรม
    ///
    /// เรียกจาก TransactionService ก่อนทุก deposit/withdraw/transfer
    ///
    /// Flow:
    ///   1. เช็คว่ามี PIN ไหม (ยังไม่ตั้ง → error)
    ///   2. เช็คว่าถูกล็อกธุรกรรมไหม (PIN ผิดเกิน → error)
    ///   3. Verify PIN
    ///      - ถูก → reset counter, return true
    ///      - ผิด → เพิ่ม counter (ถึง 3 → ล็อก)
    /// </summary>
    public async Task VerifyPinAsync(Guid userId, string pin, CancellationToken ct = default)
    {
        var user = await _unitOfWork.Users.GetByIdAsync(userId, ct)
            ?? throw new NotFoundException("User", userId);

        if (user.PinHash is null)
            throw new InvalidOperationException(
                "PIN is not set. Please set your PIN before making transactions.");

        if (user.IsTransactionLocked)
            throw new InvalidOperationException(
                "Transactions are locked due to too many failed PIN attempts. Contact support.");

        if (!BCrypt.Net.BCrypt.Verify(pin, user.PinHash))
        {
            user.FailedPinAttempts++;

            if (user.FailedPinAttempts >= MaxPinAttempts)
            {
                user.IsTransactionLocked = true;
            }

            _unitOfWork.Users.Update(user);
            await _unitOfWork.SaveChangesAsync(ct);

            var remaining = MaxPinAttempts - user.FailedPinAttempts;
            if (remaining > 0)
                throw new ArgumentException(
                    $"Incorrect PIN. {remaining} attempt(s) remaining.");
            else
                throw new InvalidOperationException(
                    "Incorrect PIN. Transactions have been locked. Contact support.");
        }

        // PIN ถูก → reset counter
        if (user.FailedPinAttempts > 0)
        {
            user.FailedPinAttempts = 0;
            _unitOfWork.Users.Update(user);
            await _unitOfWork.SaveChangesAsync(ct);
        }
    }
}