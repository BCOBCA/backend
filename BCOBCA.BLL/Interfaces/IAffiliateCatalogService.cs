using BCOBCA.BLL.Common;
using BCOBCA.BLL.DTOs;

namespace BCOBCA.BLL.Interfaces;

public interface IAffiliateCatalogService
{
    Task<PageResult<AffiliateProductDto>> ProductsAsync(Guid userId, CatalogQuery query, CancellationToken ct);
}
