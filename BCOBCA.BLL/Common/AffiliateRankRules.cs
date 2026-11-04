using BCOBCA.BLL.DTOs;

namespace BCOBCA.BLL.Common;

public static class AffiliateRankRules
{
    public static void Validate(IReadOnlyList<AffiliateRankDto> ranks)
    {
        if (ranks is null || ranks.Count != 5 || ranks.Any(x => x is null)) throw new BusinessException("Cần cấu hình đủ 5 hạng giới thiệu");
        var sorted = ranks.OrderBy(x => x.Rank).ToArray();
        for (var index = 0; index < sorted.Length; index++)
        {
            var rank = sorted[index];
            if (rank.Rank != index + 1 || (index == 0 ? rank.MinSuccessfulReferrals != 0 : rank.MinSuccessfulReferrals <= sorted[index - 1].MinSuccessfulReferrals))
                throw new BusinessException("Hạng phải từ 1 đến 5, số lượt bắt đầu từ 0 và tăng dần");
            if (string.IsNullOrWhiteSpace(rank.Name) || rank.Name.Length > 100 || rank.CommissionRate < 0 || rank.CommissionRate > 1 || decimal.Round(rank.CommissionRate, 4) != rank.CommissionRate
                || index > 0 && rank.CommissionRate < sorted[index - 1].CommissionRate)
                throw new BusinessException("Tên hạng hoặc tỷ lệ hoa hồng không hợp lệ; tỷ lệ từ 0 đến 1 và không giảm theo hạng");
        }
        if (sorted.Select(x => x.Name.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() != 5) throw new BusinessException("Tên các hạng không được trùng nhau");
    }

    public static AffiliateRankProgressDto Calculate(int successfulReferrals, IReadOnlyList<AffiliateRankDto> ranks)
    {
        Validate(ranks);
        if (successfulReferrals < 0) throw new BusinessException("Số lượt giới thiệu không hợp lệ", 409);
        var sorted = ranks.OrderBy(x => x.Rank).ToArray();
        var current = sorted.Last(x => x.MinSuccessfulReferrals <= successfulReferrals);
        var next = sorted.FirstOrDefault(x => x.Rank > current.Rank);
        return new(current.Rank, current.Name, successfulReferrals, current.CommissionRate, next?.Name, next?.MinSuccessfulReferrals,
            next is null ? 0 : next.MinSuccessfulReferrals - successfulReferrals);
    }
}
