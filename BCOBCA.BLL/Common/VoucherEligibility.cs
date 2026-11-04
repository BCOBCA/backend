using BCOBCA.DAL.Entities;
using BCOBCA.DAL.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BCOBCA.BLL.Common;

public static class VoucherEligibility
{
    public static async Task RequireLevelAsync(ICommerceStore store, Guid userId, int minimum, CancellationToken ct)
    {
        if (minimum <= 1) return;
        var earned = await store.Set<UserMembership>().Where(x => x.UserId == userId).Select(x => x.LifetimeEarnedPoints).SingleAsync(ct);
        if (MembershipLevelRules.Calculate(earned, await LoyaltyQueries.MembershipLevelsAsync(store, ct)).Level < minimum)
            throw new BusinessException($"Ưu đãi dành cho thành viên từ Lv.{minimum}", 409);
    }
}
