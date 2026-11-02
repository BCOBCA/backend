using BCOBCA.BLL.Common;
using BCOBCA.BLL.DTOs;
using BCOBCA.BLL.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BCOBCA.API.Controllers;

[ApiController, Authorize, Route("api")]
public sealed class FinanceController(IFinanceService finance, IAuthService auth) : ControllerBase
{
    private Guid AccountId => Guid.Parse(User.FindFirst("sub")!.Value);
    private Task<Guid> UserId(CancellationToken ct) => auth.GetUserIdAsync(AccountId, ct);
    [HttpPost("affiliate/withdrawals")]
    public async Task<IActionResult> Withdraw(WithdrawalRequest input, CancellationToken ct) => StatusCode(201, ApiResponse<ResourceIdDto>.Ok(new(await finance.WithdrawAsync(await UserId(ct), Request.Headers["Idempotency-Key"].ToString(), input, ct))));
    [HttpGet("affiliate/withdrawals")]
    public async Task<IActionResult> Withdrawals([FromQuery] CatalogQuery query, CancellationToken ct) => Ok(ApiResponse<PageResult<WithdrawalDto>>.Ok(await finance.WithdrawalsAsync(await UserId(ct), query, ct)));
    [HttpGet("affiliate/transactions")]
    public async Task<IActionResult> Transactions([FromQuery] CatalogQuery query, CancellationToken ct) => Ok(ApiResponse<PageResult<WalletTransactionDto>>.Ok(await finance.TransactionsAsync(await UserId(ct), query, ct)));
    [HttpGet("affiliate/commissions")]
    public async Task<IActionResult> Commissions([FromQuery] CatalogQuery query, CancellationToken ct) => Ok(ApiResponse<PageResult<CommissionDto>>.Ok(await finance.CommissionsAsync(await UserId(ct), query, ct)));
    [HttpGet("affiliate/leaderboard")]
    public async Task<IActionResult> Leaderboard([FromQuery] CatalogQuery query, CancellationToken ct) => Ok(ApiResponse<PageResult<LeaderboardDto>>.Ok(await finance.LeaderboardAsync(query, ct)));
    [Authorize(Roles = "Admin,SuperAdmin"), HttpPost("admin/affiliate/commissions/{id:guid}/approve")]
    public async Task<IActionResult> Approve(Guid id, CancellationToken ct) { await finance.ApproveCommissionAsync(AccountId, id, ct); return Ok(ApiResponse<object>.Ok(new { })); }
    [Authorize(Roles = "Admin,SuperAdmin"), HttpPut("admin/affiliate/withdrawals/{id:guid}/status")]
    public async Task<IActionResult> WithdrawalStatus(Guid id, WithdrawalStatusRequest input, CancellationToken ct) { await finance.SetWithdrawalStatusAsync(AccountId, id, input, ct); return Ok(ApiResponse<object>.Ok(new { })); }
    [Authorize(Roles = "Admin,SuperAdmin"), HttpGet("admin/affiliate/withdrawals/{id:guid}/bank-details")]
    public async Task<IActionResult> Bank(Guid id, CancellationToken ct) => Ok(ApiResponse<BankDetailsDto>.Ok(await finance.BankDetailsAsync(AccountId, id, ct)));
}
