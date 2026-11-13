using BCOBCA.BLL.Common;
using BCOBCA.BLL.DTOs;

namespace BCOBCA.BLL.Interfaces;

public interface ISellerService
{
    Task<IReadOnlyList<SellerStoreDto>> StoresAsync(Guid accountId, CancellationToken ct);
    Task<SellerStoreDto> OnboardAsync(Guid accountId, CancellationToken ct);
    Task<SellerSummaryDto> SummaryAsync(Guid accountId, Guid storeId, CancellationToken ct);
    Task<PageResult<SellerProductDto>> ProductsAsync(Guid accountId, Guid storeId, CatalogQuery query, CancellationToken ct);
    Task<PageResult<SellerOrderDto>> OrdersAsync(Guid accountId, Guid storeId, CatalogQuery query, CancellationToken ct);
    Task<SellerOrderDto> OrderAsync(Guid accountId, Guid storeId, Guid id, CancellationToken ct);
}
