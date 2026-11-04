using BCOBCA.DAL.Entities;
using BCOBCA.DAL.Interfaces;

namespace BCOBCA.BLL.Common;

public static class RewardLedger
{
    private static void Entry(ICommerceStore store, UserMembership member, string type, long points, long balance, string referenceType, Guid referenceId, string description)
        => store.Set<PointTransaction>().Add(new PointTransaction { MembershipId = member.Id, Type = type, Points = points, BalanceAfter = balance, ReferenceType = referenceType, ReferenceId = referenceId, Description = description, CreatedAt = DateTimeOffset.UtcNow });

    public static long Earn(ICommerceStore store, UserMembership member, long points, string type, string referenceType, Guid referenceId, string description)
    {
        if (points < 0) throw new BusinessException("Số điểm thưởng không hợp lệ");
        member.AvailablePoints = checked(member.AvailablePoints + points); member.LifetimeEarnedPoints = checked(member.LifetimeEarnedPoints + points);
        Entry(store, member, type, points, member.AvailablePoints, referenceType, referenceId, description);
        var repayment = Math.Min(member.PointDebt, points);
        if (repayment > 0)
        {
            member.AvailablePoints -= repayment; member.PointDebt -= repayment;
            Entry(store, member, "DEBT_REPAY", -repayment, member.AvailablePoints, referenceType, referenceId, "Cấn trừ điểm đã thu hồi");
            Entry(store, member, "DEBT_DEBIT", -repayment, member.PointDebt, referenceType, referenceId, "Giảm điểm cần thu hồi");
        }
        return repayment;
    }

    public static void Reverse(ICommerceStore store, UserMembership member, long points, Guid refundId)
    {
        if (points < 0 || member.LifetimeEarnedPoints < points) throw new BusinessException("Sổ cái điểm không khớp", 409);
        var removed = Math.Min(member.AvailablePoints, points);
        member.AvailablePoints -= removed; member.LifetimeEarnedPoints -= points;
        Entry(store, member, "ORDER_REVERSE", -removed, member.AvailablePoints, "Refund", refundId, "Thu hồi điểm đơn hoàn tiền");
        if (removed < points)
        {
            member.PointDebt = checked(member.PointDebt + points - removed);
            Entry(store, member, "DEBT_CREDIT", points - removed, member.PointDebt, "Refund", refundId, "Điểm cần cấn trừ từ lần thưởng sau");
        }
    }

    public static void RedeemVoucher(ICommerceStore store, UserMembership member, long points, Guid claimId)
    {
        if (!store.HasTransaction) throw new InvalidOperationException("Point redemption requires a transaction.");
        if (points <= 0) throw new BusinessException("Số điểm đổi ưu đãi không hợp lệ");
        if (member.PointDebt > 0) throw new BusinessException("Cần cấn trừ điểm đã thu hồi trước khi đổi ưu đãi", 409);
        if (member.AvailablePoints < points) throw new BusinessException("Điểm có thể sử dụng không đủ để đổi ưu đãi", 409);
        member.AvailablePoints -= points;
        Entry(store, member, "VOUCHER_REDEEM", -points, member.AvailablePoints, "UserVoucher", claimId, "Đổi điểm lấy voucher");
    }
}
