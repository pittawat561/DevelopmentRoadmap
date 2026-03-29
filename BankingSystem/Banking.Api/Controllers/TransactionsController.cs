using Banking.Application.DTOs;
using Banking.Application.Services;
using Banking.Domain.Exceptions;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using System.Runtime.InteropServices;
using static System.Net.Mime.MediaTypeNames;

namespace Banking.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TransactionsController : ControllerBase
{
    private readonly TransactionService _transactionService;
    private readonly IValidator<DepositRequest> _depositValidator;

    public TransactionsController(TransactionService transactionService, IValidator<DepositRequest> depositValidator)
    {
        _transactionService = transactionService;
        _depositValidator = depositValidator;
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

        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        var result = await _transactionService.DepositAsync(request, ipAddress, ct);
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
        try
        {
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
            var result = await _transactionService.WithdrawAsync(request, ipAddress, ct);

            return Ok(new ApiResponse<TransactionResponse>(
                Success: true,
                Message: $"Withdrawal of {request.Amount:N2} THB completed.",
                Data: result
            ));
        }
        catch (NotFoundException ex)
        {
            return NotFound(new ApiResponse<object>(false, ex.Message));
        }
        catch (InsufficientFundsException ex)
        {
            return BadRequest(new ApiResponse<object>(false, ex.Message));
        }
        catch (DailyLimitExceededException ex)
        {
            return BadRequest(new ApiResponse<object>(false, ex.Message));
        }
        catch (AccountFrozenException ex)
        {
            return BadRequest(new ApiResponse<object>(false, ex.Message));
        }
    }

    /// <summary>
    /// โอนเงิน
    /// POST /api/transactions/transfer
    /// </summary>
    [HttpPost("transfer")]
    public async Task<IActionResult> Transfer(
        [FromBody] TransferRequest request, CancellationToken ct)
    {
        try
        {
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
            var result = await _transactionService.TransferAsync(request, ipAddress, ct);

            return Ok(new ApiResponse<TransactionResponse>(
                Success: true,
                Message: $"Transfer of {request.Amount:N2} THB completed.",
                Data: result
            ));
        }
        catch (NotFoundException ex)
        {
            return NotFound(new ApiResponse<object>(false, ex.Message));
        }
        catch (InsufficientFundsException ex)
        {
            return BadRequest(new ApiResponse<object>(false, ex.Message));
        }
        catch (DailyLimitExceededException ex)
        {
            return BadRequest(new ApiResponse<object>(false, ex.Message));
        }
        catch (AccountFrozenException ex)
        {
            return BadRequest(new ApiResponse<object>(false, ex.Message));
        }
    }

    /// <summary>
    /// ดูประวัติธุรกรรม (Pagination)
    /// GET /api/transactions?accountId=xxx&page=1&pageSize=20
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
            Success: true,
            Message: "Transaction history retrieved.",
            Data: result
        ));
    }
}