using BCOBCA.BLL.Common;
using BCOBCA.BLL.DTOs;

namespace BCOBCA.BLL.Interfaces;

public interface ISellerService
{
    Task<IReadOnlyList<SellerStoreDto>> StoresAsync(Guid accountId, CancellationToken ct);
    Task<SellerStoreDto> OnboardAsync(Guid accountId, CancellationToken ct);
    Task<SellerStoreProfileDto> ProfileAsync(Guid accountId, Guid storeId, CancellationToken ct);
    Task<SellerStoreProfileDto> UpdateProfileAsync(Guid accountId, Guid storeId, SellerStoreProfileInput input, CancellationToken ct);
    Task<SellerStoreProfileDto> SaveBranchAsync(Guid accountId, Guid storeId, Guid? branchId, SellerBranchInput input, CancellationToken ct);
    Task<SellerStoreProfileDto> SelectPickupBranchAsync(Guid accountId, Guid storeId, SellerPickupBranchInput input, CancellationToken ct);
    Task<SellerStoreProfileDto> SelectMainBranchAsync(Guid accountId, Guid storeId, SellerPickupBranchInput input, CancellationToken ct);
    Task<SellerStoreProfileDto> SelectReturnBranchAsync(Guid accountId, Guid storeId, SellerPickupBranchInput input, CancellationToken ct);
    Task<SellerSummaryDto> SummaryAsync(Guid accountId, Guid storeId, CancellationToken ct);
    Task<PageResult<SellerProductDto>> ProductsAsync(Guid accountId, Guid storeId, CatalogQuery query, CancellationToken ct);
    Task<SellerProductDetailDto> ProductAsync(Guid accountId, Guid storeId, Guid id, CancellationToken ct);
    Task<SellerProductDetailDto> UpdateProductSectionAsync(Guid accountId, Guid storeId, Guid id, string section, SellerProductSectionInput input, CancellationToken ct);
    Task<SellerProductOptionsDto> ProductOptionsAsync(Guid accountId, Guid storeId, CancellationToken ct);
    Task<SellerProductDto> CreateProductAsync(Guid accountId, Guid storeId, SellerCreateProductInput input, CancellationToken ct);
    Task<PageResult<SellerOrderDto>> OrdersAsync(Guid accountId, Guid storeId, CatalogQuery query, CancellationToken ct);
    Task<SellerOrderDto> OrderAsync(Guid accountId, Guid storeId, Guid id, CancellationToken ct);
}
