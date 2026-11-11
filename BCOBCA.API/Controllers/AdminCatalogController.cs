using BCOBCA.BLL.Common;
using BCOBCA.BLL.DTOs;
using BCOBCA.BLL.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BCOBCA.API.Controllers;

[ApiController, Authorize, Route("api/admin")]
public sealed class AdminCatalogController(IAdminCatalogService admin) : ControllerBase
{
    private Guid AccountId => Guid.Parse(User.FindFirst("sub")!.Value);
    private IActionResult Resource(Guid id) => Ok(ApiResponse<ResourceIdDto>.Ok(new(id)));
    [HttpPut("stores/{id:guid}/shipping-test")] public async Task<IActionResult> StoreShipping(Guid id, StoreTestShippingInput input, CancellationToken ct) { await admin.SetStoreTestShippingAsync(AccountId, id, input, ct); return Ok(ApiResponse<object>.Ok(new { })); }
    [HttpPost("brands")] public async Task<IActionResult> AddBrand(BrandInput input, CancellationToken ct) => Resource(await admin.SaveBrandAsync(AccountId, null, input, ct));
    [HttpPut("brands/{id:guid}")] public async Task<IActionResult> PutBrand(Guid id, BrandInput input, CancellationToken ct) => Resource(await admin.SaveBrandAsync(AccountId, id, input, ct));
    [HttpPost("categories")] public async Task<IActionResult> AddCategory(CategoryInput input, CancellationToken ct) => Resource(await admin.SaveCategoryAsync(AccountId, null, input, ct));
    [HttpPut("categories/{id:guid}")] public async Task<IActionResult> PutCategory(Guid id, CategoryInput input, CancellationToken ct) => Resource(await admin.SaveCategoryAsync(AccountId, id, input, ct));
    [HttpPost("products")] public async Task<IActionResult> AddProduct(ProductInput input, CancellationToken ct) => Resource(await admin.SaveProductAsync(AccountId, null, input, ct));
    [HttpPut("products/{id:guid}")] public async Task<IActionResult> PutProduct(Guid id, ProductInput input, CancellationToken ct) => Resource(await admin.SaveProductAsync(AccountId, id, input, ct));
    [HttpDelete("products/{id:guid}")] public async Task<IActionResult> Archive(Guid id, CancellationToken ct) { await admin.ArchiveProductAsync(AccountId, id, ct); return Ok(ApiResponse<object>.Ok(new { })); }
    [HttpPost("services")] public async Task<IActionResult> AddService(ServiceInput input, CancellationToken ct) => Resource(await admin.SaveServiceAsync(AccountId, null, input, ct));
    [HttpPut("services/{id:guid}")] public async Task<IActionResult> PutService(Guid id, ServiceInput input, CancellationToken ct) => Resource(await admin.SaveServiceAsync(AccountId, id, input, ct));
    [HttpPost("banners")] public async Task<IActionResult> AddBanner(BannerInput input, CancellationToken ct) => Resource(await admin.SaveBannerAsync(AccountId, null, input, ct));
    [HttpPut("banners/{id:guid}")] public async Task<IActionResult> PutBanner(Guid id, BannerInput input, CancellationToken ct) => Resource(await admin.SaveBannerAsync(AccountId, id, input, ct));
    [HttpPost("articles")] public async Task<IActionResult> AddArticle(ArticleInput input, CancellationToken ct) => Resource(await admin.SaveArticleAsync(AccountId, null, input, ct));
    [HttpPut("articles/{id:guid}")] public async Task<IActionResult> PutArticle(Guid id, ArticleInput input, CancellationToken ct) => Resource(await admin.SaveArticleAsync(AccountId, id, input, ct));
    [HttpPost("vouchers")] public async Task<IActionResult> AddVoucher(VoucherInput input, CancellationToken ct) => Resource(await admin.SaveVoucherAsync(AccountId, null, input, ct));
    [HttpPut("vouchers/{id:guid}")] public async Task<IActionResult> PutVoucher(Guid id, VoucherInput input, CancellationToken ct) => Resource(await admin.SaveVoucherAsync(AccountId, id, input, ct));
    [HttpPost("quotations")] public async Task<IActionResult> Quote(QuotationInput input, CancellationToken ct) => Resource(await admin.QuoteAsync(AccountId, input, ct));
    [HttpPut("account-roles")] public async Task<IActionResult> Role(AccountRoleInput input, CancellationToken ct) { await admin.SetRoleAsync(AccountId, input, ct); return Ok(ApiResponse<object>.Ok(new { })); }
}
