using BCOBCA.BLL.Common;
using BCOBCA.BLL.DTOs;
using BCOBCA.BLL.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BCOBCA.API.Controllers;

[ApiController, Route("api/dealers")]
public sealed class DealersController(IDealerService dealers) : ControllerBase
{
    private Guid? AccountId => User.Identity?.IsAuthenticated == true && Guid.TryParse(User.FindFirst("sub")?.Value, out var id) ? id : null;
    [Authorize, HttpGet("mine")]
    public async Task<IActionResult> Mine(CancellationToken ct)
        => AccountId is { } accountId ? Ok(ApiResponse<IReadOnlyList<DealerProfileDto>>.Ok(await dealers.OwnedAsync(accountId, ct))) : Unauthorized();
    [Authorize, HttpPut("{id:guid}/provinces")]
    public async Task<IActionResult> UpdateProvinces(Guid id, DealerProvincesInput input, CancellationToken ct)
        => AccountId is { } accountId ? Ok(ApiResponse<DealerProfileDto>.Ok(await dealers.UpdateProvincesAsync(id, accountId, input, ct))) : Unauthorized();
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] DealerQuery query, CancellationToken ct)
        => Ok(ApiResponse<PageResult<DealerDto>>.Ok(await dealers.ListAsync(query, AccountId, ct)));
    [HttpGet("regions")]
    public async Task<IActionResult> Regions(CancellationToken ct)
        => Ok(ApiResponse<IReadOnlyList<string>>.Ok(await dealers.RegionsAsync(ct)));
    [HttpGet("{slug}")]
    public async Task<IActionResult> Detail(string slug, CancellationToken ct)
        => Ok(ApiResponse<DealerDetailDto>.Ok(await dealers.DetailAsync(slug, AccountId, ct)));
    [Authorize, HttpPost("{id:guid}/follow")]
    public async Task<IActionResult> Follow(Guid id, CancellationToken ct) => await ChangeFollow(id, true, ct);
    [Authorize, HttpDelete("{id:guid}/follow")]
    public async Task<IActionResult> Unfollow(Guid id, CancellationToken ct) => await ChangeFollow(id, false, ct);
    private async Task<IActionResult> ChangeFollow(Guid id, bool following, CancellationToken ct)
        => AccountId is { } accountId ? Ok(ApiResponse<DealerFollowDto>.Ok(await dealers.FollowAsync(id, accountId, following, ct))) : Unauthorized();
}
