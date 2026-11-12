using BCOBCA.BLL.Common;
using BCOBCA.BLL.DTOs;
using BCOBCA.BLL.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BCOBCA.API.Controllers;

[ApiController, Authorize, Route("api/affiliate/products")]
public sealed class AffiliateProductsController(IAffiliateCatalogService catalog, IAuthService auth) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Products([FromQuery] CatalogQuery query, CancellationToken ct)
    {
        var userId = await auth.GetUserIdAsync(Guid.Parse(User.FindFirst("sub")!.Value), ct);
        return Ok(ApiResponse<PageResult<AffiliateProductDto>>.Ok(await catalog.ProductsAsync(userId, query, ct)));
    }
}
