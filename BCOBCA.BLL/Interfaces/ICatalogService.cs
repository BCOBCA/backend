using BCOBCA.BLL.Common;
using BCOBCA.BLL.DTOs;

namespace BCOBCA.BLL.Interfaces;

public interface ICatalogService
{
    Task<PageResult<BrandDto>> BrandsAsync(CatalogQuery query, CancellationToken ct);
    Task<BrandDto> BrandAsync(Guid id, CancellationToken ct);
    Task<PageResult<CategoryDto>> CategoriesAsync(CatalogQuery query, CancellationToken ct);
    Task<PageResult<CatalogItemDto>> ProductsAsync(CatalogQuery query, CancellationToken ct, Guid? userId = null);
    Task<CatalogItemDto> ProductAsync(Guid id, CancellationToken ct, Guid? userId = null);
    Task<PageResult<CatalogItemDto>> ServicesAsync(CatalogQuery query, CancellationToken ct);
    Task<CatalogItemDto> ServiceAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<BannerDto>> BannersAsync(CancellationToken ct);
    Task<PageResult<ArticleDto>> ArticlesAsync(CatalogQuery query, CancellationToken ct);
    Task<ArticleDto> ArticleAsync(Guid id, CancellationToken ct);
    Task<Guid> ConsultAsync(Guid? userId, ConsultationRequest input, CancellationToken ct);
}
