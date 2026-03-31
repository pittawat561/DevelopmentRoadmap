using Banking.Application.DTOs;
using Banking.Application.Services;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Banking.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TransactionsController : ControllerBase
{
    private readonly TransactionService _transactionService;
    private readonly IValidator<DepositRequest> _depositValidator;
    private readonly IValidator<WithdrawRequest> _withdrawValidator;
    private readonly IValidator<TransferRequest> _transferValidator;

    public TransactionsController(
        TransactionService transactionService,
        IValidator<DepositRequest> depositValidator,
        IValidator<WithdrawRequest> withdrawValidator,
        IValidator<TransferRequest> transferValidator)
    {
        _transactionService = transactionService;
        _depositValidator = depositValidator;
        _withdrawValidator = withdrawValidator;
        _transferValidator = transferValidator;
    }

    /// <summary>
    /// ฝากเงิน
    /// POST /api/transactions/deposit
    /// </summary>
    [HttpPost("deposit")]
    public async Task<IActionResult> Deposit(
        [FromBody] DepositRequest request, CancellationToken ct)
    {
        var validation = await _depositValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return BadRequest(new ApiResponse<object>(false,
                string.Join("; ", validation.Errors.Select(e => e.ErrorMessage))));

        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        var result = await _transactionService.DepositAsync(
            request, userId.Value, request.Pin, ipAddress, ct);
        return Ok(new ApiResponse<TransactionResponse>(
            true, $"Deposit of {request.Amount:N2} THB completed.", result));
    }

    /// <summary>
    /// ถอนเงิน
    /// POST /api/transactions/withdraw
    /// </summary>
    [HttpPost("withdraw")]
    public async Task<IActionResult> Withdraw(
        [FromBody] WithdrawRequest request, CancellationToken ct)
    {
        var validation = await _withdrawValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return BadRequest(new ApiResponse<object>(false,
                string.Join("; ", validation.Errors.Select(e => e.ErrorMessage))));

        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        var result = await _transactionService.WithdrawAsync(
            request, userId.Value, request.Pin, ipAddress, ct);
        return Ok(new ApiResponse<TransactionResponse>(
            true, $"Withdrawal of {request.Amount:N2} THB completed.", result));
    }

    /// <summary>
    /// โอนเงิน
    /// POST /api/transactions/transfer
    /// </summary>
    [HttpPost("transfer")]
    public async Task<IActionResult> Transfer(
        [FromBody] TransferRequest request, CancellationToken ct)
    {
        var validation = await _transferValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return BadRequest(new ApiResponse<object>(false,
                string.Join("; ", validation.Errors.Select(e => e.ErrorMessage))));

        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        var result = await _transactionService.TransferAsync(
            request, userId.Value, request.Pin, ipAddress, ct);
        return Ok(new ApiResponse<TransactionResponse>(
            true, $"Transfer of {request.Amount:N2} THB completed.", result));
    }

    /// <summary>
    /// ดูประวัติธุรกรรม (Pagination)
    /// GET /api/transactions?accountId=xxx&amp;page=1&amp;pageSize=20
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetHistory(
        [FromQuery] Guid accountId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _transactionService.GetHistoryAsync(
            accountId, page, pageSize, ct);

        return Ok(new ApiResponse<PagedResponse<TransactionResponse>>(
            true, "Transaction history retrieved.", result));
    }

    private Guid? GetUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return claim is not null ? Guid.Parse(claim) : null;
    }
}
