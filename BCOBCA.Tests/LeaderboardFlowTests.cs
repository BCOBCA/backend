using BCOBCA.BLL.Common;
using BCOBCA.BLL.DTOs;
using BCOBCA.BLL.Services;
using BCOBCA.DAL.Context;
using BCOBCA.DAL.Entities;
using BCOBCA.DAL.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Xunit;

namespace BCOBCA.Tests;

public sealed class LeaderboardFlowTests
{
    [Fact]
    public async Task PeriodRankingKeepsLifetimeTiersAndFindsOwnRankOutsideThePage()
    {
        EnvironmentBootstrap.Load();
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__Default");
        var database = "bcobca_leaderboard_test_" + Guid.NewGuid().ToString("N");
        await using var admin = new NpgsqlConnection(connection); await admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE DATABASE \"{database}\"", admin)) await create.ExecuteNonQueryAsync();
        try
        {
            await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(new NpgsqlConnectionStringBuilder(connection) { Database = database }.ConnectionString).Options);
            await db.Database.MigrateAsync();
            var now = DateTimeOffset.UtcNow;
            var range = LeaderboardPeriods.Range("week", now);
            User User(string name)
            {
                var account = new Account { Email = name + "@example.test", PasswordHash = "test-not-for-login", Role = "User", Status = 1, CreatedAt = now, UpdatedAt = now };
                var user = new User { AccountId = account.Id, Nickname = name, ReferralCode = name, AvatarUrl = "https://example.test/avatar.png", CreatedAt = now, UpdatedAt = now };
                db.AddRange(account, user); return user;
            }
            var a = User("An"); var b = User("Binh"); var c = User("Chi"); var inactive = User("Inactive");
            var pa = new AffiliateProfile { Id = Guid.Parse("10000000-0000-0000-0000-000000000001"), UserId = a.Id, AffiliateCode = "AAA001", Status = "ACTIVE", JoinedAt = now };
            var pb = new AffiliateProfile { Id = Guid.Parse("20000000-0000-0000-0000-000000000001"), UserId = b.Id, AffiliateCode = "BBB001", Status = "ACTIVE", JoinedAt = now };
            var pc = new AffiliateProfile { Id = Guid.Parse("30000000-0000-0000-0000-000000000001"), UserId = c.Id, AffiliateCode = "CCC001", Status = "ACTIVE", JoinedAt = now };
            db.AddRange(pa, pb, pc, new AffiliateProfile { UserId = inactive.Id, AffiliateCode = "DDD001", Status = "INACTIVE", JoinedAt = now });
            var ranks = await db.Set<AffiliateRank>().OrderBy(x => x.Rank).ToListAsync();
            for (var i = 0; i < ranks.Count; i++) { ranks[i].MinSuccessfulReferrals = i; ranks[i].CommissionRate = (i + 1) / 100m; }
            Order Referral(User referrer, string buyerName, DateTimeOffset qualified, string status = "COMPLETED")
            {
                var buyer = User(buyerName);
                db.Add(new AffiliateReferral { ReferrerUserId = referrer.Id, ReferredUserId = buyer.Id, ReferralCode = referrer.ReferralCode, Status = "QUALIFIED", QualifiedAt = qualified, CreatedAt = qualified });
                var order = new Order { UserId = buyer.Id, OrderCode = Guid.NewGuid().ToString("N"), IdempotencyKey = Guid.NewGuid().ToString("N"), Status = status, PaymentStatus = "PAID", CreatedAt = qualified, CompletedAt = qualified, ShippingAddress = "Test address" };
                db.Add(order); return order;
            }
            var recent = Referral(a, "a-recent", now);
            Referral(a, "a-old", now.AddYears(-1));
            Referral(a, "a-refund", now, "REFUNDED");
            Referral(b, "b-start", range.Start!.Value);
            Referral(b, "b-end", range.End!.Value);
            await db.SaveChangesAsync();
            var finance = new FinanceService(new CommerceStore(db), new ConfigurationBuilder().Build());
            var week = await finance.LeaderboardAsync(new(PageSize: 1), default, "week", c.Id);
            Assert.Equal(3, week.TotalItems); Assert.Equal(3, week.TotalPages);
            var first = Assert.Single(week.Items); Assert.Equal(pa.Id, first.Id); Assert.Equal(1, first.Referrals);
            Assert.Equal(3, first.TierRank); Assert.Equal(0.03m, first.CommissionRate);
            Assert.Equal(pc.Id, week.MyEntry!.Id); Assert.Equal(3, week.MyEntry.Rank); Assert.True(week.MyEntry.IsMe);
            var second = await finance.LeaderboardAsync(new(Page: 2, PageSize: 1), default, "week", b.Id);
            Assert.Equal(pb.Id, Assert.Single(second.Items).Id); Assert.Equal(2, second.MyEntry!.Rank); Assert.True(second.Items[0].IsMe);
            var all = await finance.LeaderboardAsync(new(), default, "all", a.Id);
            Assert.Equal(2, all.MyEntry!.Referrals); Assert.Equal(1, all.MyEntry.Rank);
            Assert.NotEmpty(all.MyEntry.AvatarUrl);
            recent.Status = "REFUNDED"; await db.SaveChangesAsync();
            var afterRefund = await finance.LeaderboardAsync(new(), default, "week", a.Id);
            Assert.Equal(0, afterRefund.MyEntry!.Referrals); Assert.Equal(2, afterRefund.MyEntry.TierRank);
            Assert.Null((await finance.LeaderboardAsync(new(), default, "all", inactive.Id)).MyEntry);
            Assert.Equal(400, (await Assert.ThrowsAsync<BusinessException>(() => finance.LeaderboardAsync(new(), default, "invalid", a.Id))).StatusCode);
        }
        finally
        {
            NpgsqlConnection.ClearAllPools();
            await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{database}\" WITH (FORCE)", admin); await drop.ExecuteNonQueryAsync();
        }
    }
}
