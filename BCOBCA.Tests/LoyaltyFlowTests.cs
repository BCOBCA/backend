using BCOBCA.BLL.Common;
using BCOBCA.BLL.DTOs;
using BCOBCA.BLL.Services;
using BCOBCA.BLL.Validators;
using BCOBCA.DAL.Context;
using BCOBCA.DAL.Entities;
using BCOBCA.DAL.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Xunit;

namespace BCOBCA.Tests;

public sealed class LoyaltyFlowTests
{
    [Fact]
    public async Task LevelsRanksAndAdminConfigurationStayIndependentAndMigrationsCanRollBack()
    {
        EnvironmentBootstrap.Load();
        var databaseName = "bcobca_test_" + Guid.NewGuid().ToString("N");
        await using var admin = new NpgsqlConnection(Environment.GetEnvironmentVariable("ConnectionStrings__Default"));
        await admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE DATABASE \"{databaseName}\"", admin)) await create.ExecuteNonQueryAsync();
        try
        {
            var connection = new NpgsqlConnectionStringBuilder(admin.ConnectionString) { Database = databaseName }.ConnectionString;
            var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection).Options;
            await using var db = new ApplicationDbContext(options);
            await db.Database.MigrateAsync();
            Assert.False(db.Database.HasPendingModelChanges());
            var store = new CommerceStore(db);
            var config = new ConfigurationBuilder().AddEnvironmentVariables().Build();
            var auth = new AuthService(store, config, new RegisterValidator(), new LoginValidator(), new ProfileValidator());
            var owner = await auth.RegisterAsync(new RegisterRequest("owner@example.com", "StrongPassword123!", "Owner"), default);
            var memberService = new MemberService(store);
            var loyalty = new LoyaltyConfigurationService(store);
            var levels = await loyalty.MembershipLevelsAsync(default);
            Assert.Equal(new[] { 0, 100, 300, 600, 1000, 1600, 2500, 4000, 6000, 10000 }, levels.Select(x => x.MinPoints));
            var denied = await Assert.ThrowsAsync<BusinessException>(() => loyalty.UpdateMembershipLevelsAsync(owner.User.AccountId, new(levels), default));
            Assert.Equal(403, denied.StatusCode);
            var membership = await db.UserMemberships.SingleAsync(x => x.UserId == owner.User.Id);
            membership.AvailablePoints = 99; membership.LifetimeEarnedPoints = 99;
            await db.SaveChangesAsync();
            Assert.Equal(1, (await memberService.MembershipAsync(owner.User.Id, default)).LevelInfo!.Level);
            await memberService.CheckInAsync(owner.User.Id, default);
            Assert.Equal(2, (await memberService.MembershipAsync(owner.User.Id, default)).LevelInfo!.Level);
            membership.AvailablePoints = 99; await db.SaveChangesAsync();
            Assert.Equal(2, (await memberService.MembershipAsync(owner.User.Id, default)).LevelInfo!.Level);
            membership.AvailablePoints = 0; membership.LifetimeEarnedPoints = 50000; await db.SaveChangesAsync();
            Assert.Equal(10, (await memberService.MembershipAsync(owner.User.Id, default)).LevelInfo!.Level);
            db.AccountRoles.Add(new AccountRole { AccountId = owner.User.AccountId, Role = "Admin" }); await db.SaveChangesAsync();
            var changedLevels = levels.Select(x => x.Level == 1 ? x with { Benefits = new[] { "Ưu đãi riêng" } } : x.Level == 2 ? x with { MinPoints = 200 } : x).ToArray();
            await loyalty.UpdateMembershipLevelsAsync(owner.User.AccountId, new(changedLevels), default);
            membership.AvailablePoints = 150; membership.LifetimeEarnedPoints = 150; await db.SaveChangesAsync();
            var updated = await memberService.MembershipAsync(owner.User.Id, default);
            Assert.Equal(1, updated.LevelInfo!.Level); Assert.Equal(50, updated.LevelInfo.PointsToNextLevel); Assert.Single(updated.LevelInfo.Benefits);
            Assert.Equal(0.02m, (await memberService.AffiliateAsync(owner.User.Id, default)).RankInfo!.CommissionRate);

            var rewardsConfig = new ConfigurationBuilder().AddConfiguration(config).AddInMemoryCollection(new Dictionary<string, string?> { ["Membership:SpendPerPoint"] = "10000" }).Build();
            var finance = new FinanceService(store, rewardsConfig);
            foreach (var sample in new (decimal Subtotal, decimal Discount, long Points)[] { (100000m, 0m, 10L), (95000m, 0m, 9L), (100000m, 5000m, 9L), (9999m, 0m, 0L) })
            {
                var purchase = new Order { UserId = owner.User.Id, OrderCode = "POINT-" + Guid.NewGuid().ToString("N"), IdempotencyKey = Guid.NewGuid().ToString("N"), Status = "COMPLETED", PaymentStatus = "PAID", Subtotal = sample.Subtotal, DiscountAmount = sample.Discount, ShippingFee = 50000, TotalAmount = sample.Subtotal - sample.Discount + 50000, CreatedAt = DateTimeOffset.UtcNow };
                db.Orders.Add(purchase); await db.SaveChangesAsync();
                var beforeAvailable = membership.AvailablePoints; var beforeEarned = membership.LifetimeEarnedPoints;
                await using (var tx = await store.BeginTransactionAsync(default))
                {
                    await finance.AccrueOrderAsync(purchase.Id, default);
                    await finance.AccrueOrderAsync(purchase.Id, default);
                    await tx.CommitAsync(default);
                }
                Assert.Equal(beforeAvailable + sample.Points, membership.AvailablePoints);
                Assert.Equal(beforeEarned + sample.Points, membership.LifetimeEarnedPoints);
                Assert.Single(await db.PointTransactions.Where(x => x.Type == "ORDER_EARN" && x.ReferenceId == purchase.Id).ToListAsync());
            }
            // Future authorized game rewards use the same ledger and increase both balances.
            await using (var tx = await store.BeginTransactionAsync(default))
            {
                RewardLedger.Earn(store, membership, 400, "MINIGAME", "Game", Guid.NewGuid(), "Test reward");
                await LoyaltyQueries.RefreshMembershipAsync(store, membership, default);
                await store.SaveChangesAsync(); await tx.CommitAsync(default);
            }
            membership.AvailablePoints = 0; await db.SaveChangesAsync();
            var afterSpending = await memberService.MembershipAsync(owner.User.Id, default);
            Assert.Equal(578, afterSpending.LifetimeEarnedPoints); Assert.Equal(0, afterSpending.AvailablePoints); Assert.Equal(3, afterSpending.LevelInfo!.Level);
            RewardLedger.Reverse(store, membership, 400, Guid.NewGuid());
            await LoyaltyQueries.RefreshMembershipAsync(store, membership, default); await db.SaveChangesAsync();
            Assert.Equal(1, (await memberService.MembershipAsync(owner.User.Id, default)).LevelInfo!.Level);

            var referred = new List<Guid>();
            for (var index = 0; index < 10; index++)
            {
                var buyer = await auth.RegisterAsync(new RegisterRequest($"buyer{index}@example.com", "StrongPassword123!", "Buyer", ReferralCode: owner.User.ReferralCode), default);
                referred.Add(buyer.User.Id);
            }
            Assert.Equal(0, await LoyaltyQueries.SuccessfulReferralsAsync(store, owner.User.Id, default));
            var orders = referred.Select((userId, index) => new Order { UserId = userId, OrderCode = $"COUNT-{index}", IdempotencyKey = "count", Status = "COMPLETED", PaymentStatus = "PAID", CreatedAt = DateTimeOffset.UtcNow }).ToList();
            db.Orders.AddRange(orders); await db.SaveChangesAsync();
            var silver = (await memberService.AffiliateAsync(owner.User.Id, default)).RankInfo!;
            Assert.Equal(10, silver.SuccessfulReferrals); Assert.Equal("Bạc", silver.Name); Assert.Equal(0.03m, silver.CommissionRate);
            db.Orders.Add(new Order { UserId = referred[0], OrderCode = "COUNT-REPEAT", IdempotencyKey = "repeat", Status = "COMPLETED", PaymentStatus = "PAID", CreatedAt = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync();
            Assert.Equal(10, await LoyaltyQueries.SuccessfulReferralsAsync(store, owner.User.Id, default));
            orders[9].PaymentStatus = "REFUNDED"; orders[9].Status = "REFUNDED"; await db.SaveChangesAsync();
            Assert.Equal("Đồng", (await memberService.AffiliateAsync(owner.User.Id, default)).RankInfo!.Name);
            orders[9].Status = "COMPLETED"; orders[9].PaymentStatus = "UNPAID"; await db.SaveChangesAsync();
            Assert.Equal(9, await LoyaltyQueries.SuccessfulReferralsAsync(store, owner.User.Id, default));
            var ranks = await loyalty.AffiliateRanksAsync(default);
            var changedRanks = ranks.Select(x => x.Rank == 1 ? x with { CommissionRate = 0.025m } : x).ToArray();
            await loyalty.UpdateAffiliateRanksAsync(owner.User.AccountId, new(changedRanks), default);
            Assert.Equal(0.025m, (await memberService.AffiliateAsync(owner.User.Id, default)).RankInfo!.CommissionRate);
            Assert.Equal(1, (await memberService.MembershipAsync(owner.User.Id, default)).LevelInfo!.Level);
            Assert.Equal(2, await db.AuditLogs.CountAsync(x => x.Action == "UPDATE_MEMBERSHIP_LEVELS" || x.Action == "UPDATE_AFFILIATE_RANKS"));

            membership.AvailablePoints = 10000; await LoyaltyQueries.RefreshMembershipAsync(store, membership, default); await db.SaveChangesAsync();
            await db.GetService<IMigrator>().MigrateAsync("20261104093000_AllowPhoneOnlyRegistration");
            await using var rolledBack = new NpgsqlConnection(connection); await rolledBack.OpenAsync();
            await using var count = new NpgsqlCommand("SELECT COUNT(*) FROM \"MembershipTiers\"", rolledBack);
            Assert.Equal(1L, (long)(await count.ExecuteScalarAsync())!);
            await db.Database.MigrateAsync();
        }
        finally
        {
            NpgsqlConnection.ClearAllPools();
            await using var drop = new NpgsqlCommand($"DROP DATABASE \"{databaseName}\" WITH (FORCE)", admin);
            await drop.ExecuteNonQueryAsync();
        }
    }
}
