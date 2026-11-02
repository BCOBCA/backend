using BCOBCA.BLL.Common;
using BCOBCA.BLL.DTOs;
using BCOBCA.BLL.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BCOBCA.API.Controllers;

[ApiController, Route("api")]
public sealed class CatalogController(ICatalogService catalog, IAuthService auth) : ControllerBase
{
    private async Task<Guid?> OptionalUserId(CancellationToken ct) => User.Identity?.IsAuthenticated == true ? await auth.GetUserIdAsync(Guid.Parse(User.FindFirst("sub")!.Value), ct) : null;
    [HttpGet("brands")]
    public async Task<IActionResult> Brands([FromQuery] CatalogQuery query, CancellationToken ct) => Ok(ApiResponse<PageResult<BrandDto>>.Ok(await catalog.BrandsAsync(query, ct)));
    [HttpGet("brands/{id:guid}")]
    public async Task<IActionResult> Brand(Guid id, CancellationToken ct) => Ok(ApiResponse<BrandDto>.Ok(await catalog.BrandAsync(id, ct)));
    [HttpGet("categories")]
    public async Task<IActionResult> Categories([FromQuery] CatalogQuery query, CancellationToken ct) => Ok(ApiResponse<PageResult<CategoryDto>>.Ok(await catalog.CategoriesAsync(query, ct)));
    [HttpGet("products")]
    public async Task<IActionResult> Products([FromQuery] CatalogQuery query, CancellationToken ct) => Ok(ApiResponse<PageResult<CatalogItemDto>>.Ok(await catalog.ProductsAsync(query, ct, await OptionalUserId(ct))));
    [HttpGet("products/{id:guid}")]
    public async Task<IActionResult> Product(Guid id, CancellationToken ct) => Ok(ApiResponse<CatalogItemDto>.Ok(await catalog.ProductAsync(id, ct, await OptionalUserId(ct))));
    [HttpGet("services")]
    public async Task<IActionResult> Services([FromQuery] CatalogQuery query, CancellationToken ct) => Ok(ApiResponse<PageResult<CatalogItemDto>>.Ok(await catalog.ServicesAsync(query, ct)));
    [HttpGet("services/{id:guid}")]
    public async Task<IActionResult> Service(Guid id, CancellationToken ct) => Ok(ApiResponse<CatalogItemDto>.Ok(await catalog.ServiceAsync(id, ct)));
    [Authorize, HttpPost("services/requests")]
    public async Task<IActionResult> Consult(ConsultationRequest input, [FromServices] IAuthService auth, CancellationToken ct)
    {
        var userId = await auth.GetUserIdAsync(Guid.Parse(User.FindFirst("sub")!.Value), ct);
        return StatusCode(201, ApiResponse<object>.Ok(new { id = await catalog.ConsultAsync(userId, input, ct) }));
    }
    [HttpGet("banners")]
    public async Task<IActionResult> Banners(CancellationToken ct) => Ok(ApiResponse<IReadOnlyList<BannerDto>>.Ok(await catalog.BannersAsync(ct)));
    [HttpGet("articles")]
    public async Task<IActionResult> Articles([FromQuery] CatalogQuery query, CancellationToken ct) => Ok(ApiResponse<PageResult<ArticleDto>>.Ok(await catalog.ArticlesAsync(query, ct)));
    [HttpGet("articles/{id:guid}")]
    public async Task<IActionResult> Article(Guid id, CancellationToken ct) => Ok(ApiResponse<ArticleDto>.Ok(await catalog.ArticleAsync(id, ct)));
}
