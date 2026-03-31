using Banking.Application.DTOs;
using Banking.Domain.Enums;
using Banking.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Banking.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AdminController : ControllerBase
{
    private readonly IUnitOfWork _unitOfWork;

    public AdminController(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    /// <summary>
    /// Dashboard สถิติระบบ — GET /api/admin/dashboard
    /// </summary>
    [HttpGet("dashboard")]
    public async Task<IActionResult> Dashboard(CancellationToken ct)
    {
        var users = await _unitOfWork.Users.GetAllAsync(ct);
        var accounts = await _unitOfWork.Accounts.GetAllAsync(ct);

        var dashboard = new
        {
            TotalUsers = users.Count,
            TotalAccounts = accounts.Count,
            ActiveAccounts = accounts.Count(a => a.Status == AccountStatus.Active),
            FrozenAccounts = accounts.Count(a => a.Status == AccountStatus.Frozen),
            TotalBalance = accounts.Sum(a => a.Balance),
            LockedUsers = users.Count(u => u.IsLocked)
        };

        return Ok(new ApiResponse<object>(true, "Dashboard data retrieved.", dashboard));
    }

    /// <summary>
    /// อายัดบัญชี — POST /api/admin/accounts/{id}/freeze
    /// </summary>
    [HttpPost("accounts/{id:guid}/freeze")]
    public async Task<IActionResult> FreezeAccount(Guid id, CancellationToken ct)
    {
        var account = await _unitOfWork.Accounts.GetByIdAsync(id, ct);
        if (account is null)
            return NotFound(new ApiResponse<object>(false, "Account not found."));

        if (account.Status == AccountStatus.Frozen)
            return BadRequest(new ApiResponse<object>(false, "Account is already frozen."));

        account.Status = AccountStatus.Frozen;
        _unitOfWork.Accounts.Update(account);
        await _unitOfWork.SaveChangesAsync(ct);

        return Ok(new ApiResponse<object>(true,
            $"Account {account.AccountNumber} has been frozen."));
    }

    /// <summary>
    /// ปลดอายัด — POST /api/admin/accounts/{id}/unfreeze
    /// </summary>
    [HttpPost("accounts/{id:guid}/unfreeze")]
    public async Task<IActionResult> UnfreezeAccount(Guid id, CancellationToken ct)
    {
        var account = await _unitOfWork.Accounts.GetByIdAsync(id, ct);
        if (account is null)
            return NotFound(new ApiResponse<object>(false, "Account not found."));

        if (account.Status != AccountStatus.Frozen)
            return BadRequest(new ApiResponse<object>(false, "Account is not frozen."));

        account.Status = AccountStatus.Active;
        _unitOfWork.Accounts.Update(account);
        await _unitOfWork.SaveChangesAsync(ct);

        return Ok(new ApiResponse<object>(true,
            $"Account {account.AccountNumber} has been unfrozen."));
    }

    /// <summary>
    /// ปลด lock user — POST /api/admin/users/{id}/unlock
    /// </summary>
    [HttpPost("users/{id:guid}/unlock")]
    public async Task<IActionResult> UnlockUser(Guid id, CancellationToken ct)
    {
        var user = await _unitOfWork.Users.GetByIdAsync(id, ct);
        if (user is null)
            return NotFound(new ApiResponse<object>(false, "User not found."));

        if (!user.IsLocked)
            return BadRequest(new ApiResponse<object>(false, "User is not locked."));

        user.IsLocked = false;
        user.FailedLoginAttempts = 0;
        _unitOfWork.Users.Update(user);
        await _unitOfWork.SaveChangesAsync(ct);

        return Ok(new ApiResponse<object>(true,
            $"User {user.FullName} has been unlocked."));
    }

    [HttpPost("user/{id:guid}/reset-pin-lock")]
    public async Task<IActionResult> ResetPinLock(Guid id, CancellationToken ct)
    {
        var user = await _unitOfWork.Users.GetByIdAsync(id, ct);
        if (user is null)
            return NotFound(new ApiResponse<object>(false, "User not found."));

        user.IsTransactionLocked = false;
        user.FailedLoginAttempts = 0;
        user.PinHash = null;
        _unitOfWork.Users.Update(user);
        await _unitOfWork.SaveChangesAsync(ct);

        return Ok(new ApiResponse<object>(true, $"Transaction lock reset for {user.FullName}. User must set a new PIN."));

    }
}