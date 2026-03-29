using Banking.Application.DTOs;
using Banking.Application.Services;
using Banking.Domain.Enums;
using Banking.Domain.Exceptions;
using Banking.Domain.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Banking.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AccountsController : ControllerBase
{
    private readonly IUnitOfWork _unitOfWork;

    public AccountsController(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    /// <summary>
    /// ดูบัญชีทั้งหมดของ user
    /// GET /api/accounts?userId=xxx
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetByUserId(
        [FromQuery] Guid userId, CancellationToken ct)
    {
        var accounts = await _unitOfWork.Accounts.GetByUserIdAsync(userId, ct);

        var response = accounts.Select(a => new AccountResponse(
            Id: a.Id,
            AccountNumber: a.AccountNumber,
            Type: a.Type.ToString(),
            Currency: a.Currency,
            Balance: a.Balance,
            AvailableBalance: a.AvailableBalance,
            Status: a.Status.ToString(),
            CreatedAt: a.CreatedAt
        )).ToList();

        return Ok(new ApiResponse<List<AccountResponse>>(
            true, "Accounts retrieved.", response));
    }

    /// <summary>
    /// ดูรายละเอียดบัญชี
    /// GET /api/accounts/{id}
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var account = await _unitOfWork.Accounts.GetByIdAsync(id, ct);
        if (account is null)
            return NotFound(new ApiResponse<object>(false, "Account not found."));

        var response = new AccountResponse(
            Id: account.Id,
            AccountNumber: account.AccountNumber,
            Type: account.Type.ToString(),
            Currency: account.Currency,
            Balance: account.Balance,
            AvailableBalance: account.AvailableBalance,
            Status: account.Status.ToString(),
            CreatedAt: account.CreatedAt
        );

        return Ok(new ApiResponse<AccountResponse>(true, "Account retrieved.", response));
    }

    /// <summary>
    /// ดูยอดเงินคงเหลือ
    /// GET /api/accounts/{id}/balance
    /// </summary>
    [HttpGet("{id:guid}/balance")]
    public async Task<IActionResult> GetBalance(Guid id, CancellationToken ct)
    {
        var account = await _unitOfWork.Accounts.GetByIdAsync(id, ct);
        if (account is null)
            return NotFound(new ApiResponse<object>(false, "Account not found."));

        return Ok(new ApiResponse<object>(true, "Balance retrieved.", new
        {
            account.Balance,
            account.AvailableBalance,
            account.Currency
        }));
    }

    /// <summary>
    /// สร้างบัญชีใหม่
    /// POST /api/accounts
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateAccountRequest request, CancellationToken ct)
    {
        // สร้างเลขบัญชีที่ไม่ซ้ำ
        string accountNumber;
        do
        {
            accountNumber = AccountNumberGenerator.Generate();
        } while (await _unitOfWork.Accounts.AccountNumberExistsAsync(accountNumber, ct));

        var account = new Domain.Entities.Account
        {
            UserId = request.UserId,
            AccountNumber = accountNumber,
            Type = Enum.Parse<AccountType>(request.Type),
            Currency = request.Currency ?? "THB",
            Status = AccountStatus.Active
        };

        await _unitOfWork.Accounts.AddAsync(account, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Created($"/api/accounts/{account.Id}",
            new ApiResponse<AccountResponse>(true, "Account created.", new AccountResponse(
                account.Id, account.AccountNumber, account.Type.ToString(),
                account.Currency, account.Balance, account.AvailableBalance,
                account.Status.ToString(), account.CreatedAt
            )));
    }
}

public record CreateAccountRequest(
    Guid UserId,
    string Type = "Savings",
    string? Currency = "THB"
);