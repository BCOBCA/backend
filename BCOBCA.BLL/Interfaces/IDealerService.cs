using BCOBCA.BLL.Common;
using BCOBCA.BLL.DTOs;

namespace BCOBCA.BLL.Interfaces;

public interface IDealerService
{
    Task<PageResult<DealerDto>> ListAsync(DealerQuery query, Guid? accountId, CancellationToken ct);
    Task<DealerDetailDto> DetailAsync(string slug, Guid? accountId, CancellationToken ct);
    Task<IReadOnlyList<string>> RegionsAsync(CancellationToken ct);
    Task<DealerFollowDto> FollowAsync(Guid id, Guid accountId, bool following, CancellationToken ct);
}
