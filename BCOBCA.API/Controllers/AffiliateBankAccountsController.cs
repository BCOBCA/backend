using BCOBCA.BLL.Common;
using BCOBCA.BLL.DTOs;
using BCOBCA.BLL.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BCOBCA.API.Controllers;

[ApiController, Authorize, Route("api/affiliate/bank-accounts")]
public sealed class AffiliateBankAccountsController(IAffiliateBankAccountService banks, IAuthService auth) : ControllerBase
{
    private Task<Guid> UserId(CancellationToken ct) => auth.GetUserIdAsync(Guid.Parse(User.FindFirst("sub")!.Value), ct);
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) => Ok(ApiResponse<IReadOnlyList<AffiliateBankAccountDto>>.Ok(await banks.ListAsync(await UserId(ct), ct)));
    [HttpPost]
    public async Task<IActionResult> Add(BankDetailsDto input, CancellationToken ct) => StatusCode(201, ApiResponse<AffiliateBankAccountDto>.Ok(await banks.AddAsync(await UserId(ct), input, ct)));
}
