using BCOBCA.BLL.DTOs;

namespace BCOBCA.BLL.Interfaces;

public interface IAdminCatalogService
{
    Task SetStoreTestShippingAsync(Guid accountId, Guid storeId, StoreTestShippingInput input, CancellationToken ct);
    Task<Guid> SaveBrandAsync(Guid accountId, Guid? id, BrandInput input, CancellationToken ct);
    Task<Guid> SaveCategoryAsync(Guid accountId, Guid? id, CategoryInput input, CancellationToken ct);
    Task<Guid> SaveProductAsync(Guid accountId, Guid? id, ProductInput input, CancellationToken ct);
    Task<Guid> SaveServiceAsync(Guid accountId, Guid? id, ServiceInput input, CancellationToken ct);
    Task<Guid> SaveBannerAsync(Guid accountId, Guid? id, BannerInput input, CancellationToken ct);
    Task<Guid> SaveArticleAsync(Guid accountId, Guid? id, ArticleInput input, CancellationToken ct);
    Task<Guid> SaveVoucherAsync(Guid accountId, Guid? id, VoucherInput input, CancellationToken ct);
    Task<Guid> QuoteAsync(Guid accountId, QuotationInput input, CancellationToken ct);
    Task ArchiveProductAsync(Guid accountId, Guid id, CancellationToken ct);
    Task SetRoleAsync(Guid accountId, AccountRoleInput input, CancellationToken ct);
}
