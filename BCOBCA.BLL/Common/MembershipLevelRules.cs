using BCOBCA.BLL.DTOs;

namespace BCOBCA.BLL.Common;

public static class MembershipLevelRules
{
    public static void Validate(IReadOnlyList<MembershipLevelDto> levels)
    {
        if (levels is null || levels.Count != 10 || levels.Any(x => x is null)) throw new BusinessException("Cần cấu hình đủ 10 level thành viên");
        var sorted = levels.OrderBy(x => x.Level).ToArray();
        for (var index = 0; index < sorted.Length; index++)
        {
            var level = sorted[index];
            if (level.Level != index + 1 || (index == 0 ? level.MinPoints != 0 : level.MinPoints <= sorted[index - 1].MinPoints))
                throw new BusinessException("Level phải từ 1 đến 10, bắt đầu từ 0 điểm và tăng dần");
            if (level.Benefits is null || level.Benefits.Count > 20 || level.Benefits.Any(x => string.IsNullOrWhiteSpace(x) || x.Length > 500))
                throw new BusinessException("Mỗi level có tối đa 20 ưu đãi, mỗi ưu đãi từ 1 đến 500 ký tự");
        }
    }

    public static MembershipProgressDto Calculate(long availablePoints, IReadOnlyList<MembershipLevelDto> levels)
    {
        Validate(levels);
        if (availablePoints < 0) throw new BusinessException("Số dư điểm thành viên không hợp lệ", 409);
        var sorted = levels.OrderBy(x => x.Level).ToArray();
        var current = sorted.Last(x => x.MinPoints <= availablePoints);
        var next = sorted.FirstOrDefault(x => x.Level > current.Level);
        var remaining = next is null ? 0 : next.MinPoints - availablePoints;
        var progress = next is null ? 100m : decimal.Round((availablePoints - current.MinPoints) * 100m / (next.MinPoints - current.MinPoints), 2);
        return new(current.Level, current.MinPoints, next?.Level, next?.MinPoints, remaining, progress, current.Benefits);
    }
}
