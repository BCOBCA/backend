using BCOBCA.BLL.Common;
using BCOBCA.DAL.Context;
using BCOBCA.DAL.Entities;
using BCOBCA.DAL.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BCOBCA.Tests;

public sealed class RewardLedgerTests
{
    [Fact]
    public void RefundRecordsPointDebtAndFutureRewardsRecoverItWithoutNegativeAvailablePoints()
    {
        // Only tracked domain state is exercised; this test never opens a connection.
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql("Host=localhost;Database=unused;Username=unused").Options);
        var store = new CommerceStore(db);
        var member = new UserMembership { AvailablePoints = 3, LifetimeEarnedPoints = 10 };
        RewardLedger.Reverse(store, member, 10, Guid.NewGuid());
        Assert.Equal(0, member.AvailablePoints); Assert.Equal(7, member.PointDebt); Assert.Equal(0, member.LifetimeEarnedPoints);
        var repaid = RewardLedger.Earn(store, member, 5, "CHECK_IN", "DailyCheckIn", Guid.NewGuid(), "Test reward");
        Assert.Equal(5, repaid); Assert.Equal(0, member.AvailablePoints); Assert.Equal(2, member.PointDebt);
        RewardLedger.Earn(store, member, 4, "ORDER_EARN", "Order", Guid.NewGuid(), "Test reward");
        Assert.Equal(2, member.AvailablePoints); Assert.Equal(0, member.PointDebt);
    }
}
