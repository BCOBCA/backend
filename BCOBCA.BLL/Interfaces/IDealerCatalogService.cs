using BCOBCA.BLL.Common;
using BCOBCA.BLL.DTOs;

namespace BCOBCA.BLL.Interfaces;

public interface IDealerCatalogService
{
    Task<PageResult<DealerProductDto>> ProductsAsync(Guid storeId, DealerProductQuery query, CancellationToken ct);
    Task<IReadOnlyList<DealerProductCategoryDto>> CategoriesAsync(Guid storeId, CancellationToken ct);
}
