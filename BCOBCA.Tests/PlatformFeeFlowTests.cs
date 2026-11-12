using BCOBCA.BLL.Common;
using BCOBCA.BLL.DTOs;
using BCOBCA.BLL.Services;
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

public sealed class PlatformFeeFlowTests
{
    [Fact]
    public async Task AdminUpdatesDatabaseRateAndOnlyFutureAccrualUsesIt()
    {
        EnvironmentBootstrap.Load();
        var supplied = Environment.GetEnvironmentVariable("ConnectionStrings__Default") ?? throw new InvalidOperationException("Configure isolated PostgreSQL tests.");
        var scratch = new NpgsqlConnectionStringBuilder(supplied) { Database = "bcobca_platform_fee_" + Guid.NewGuid().ToString("N") };
        await using var adminConnection = new NpgsqlConnection(supplied); await adminConnection.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE DATABASE \"{scratch.Database}\" TEMPLATE template0 LOCALE_PROVIDER icu ICU_LOCALE 'vi-VN'", adminConnection)) await create.ExecuteNonQueryAsync();
        try
        {
            await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(scratch.ConnectionString).Options);
            await db.Database.MigrateAsync(); Assert.False(db.Database.HasPendingModelChanges());
            var admin = new Account { Email = "fee-admin@example.test", Status = 1 };
            var buyer = new Account { Email = "fee-buyer@example.test", Status = 1 };
            var user = new User { AccountId = buyer.Id, ReferralCode = "FEE-BUYER" };
            var role = new AccountRole { AccountId = admin.Id, Role = "Admin" };
            var brand = new Brand { OwnerAccountId = admin.Id, Name = "Fee brand", Slug = "fee-brand", Status = 1 };
            var membership = new UserMembership { UserId = user.Id, TierId = (await db.MembershipTiers.OrderBy(t => t.SortOrder).FirstAsync()).Id, MemberCode = "FEE-BUYER" };
            db.AddRange(admin, buyer, user, role, brand, membership);
            var storeId = TestStores.Own(db, brand); await db.SaveChangesAsync();
            var store = new CommerceStore(db);
            var settings = new PlatformFeeConfigurationService(store);
            var initial = await settings.GetAsync(admin.Id, default); Assert.Equal(0m, initial.PlatformFeeRate);
            Assert.Equal(403, (await Assert.ThrowsAsync<BusinessException>(() => settings.GetAsync(buyer.Id, default))).StatusCode);
            Assert.Equal(403, (await Assert.ThrowsAsync<BusinessException>(() => settings.UpdateAsync(buyer.Id, new(0.05m, initial.Revision), default))).StatusCode);
            await Assert.ThrowsAsync<BusinessException>(() => settings.UpdateAsync(admin.Id, new(null, initial.Revision), default));
            await Assert.ThrowsAsync<BusinessException>(() => settings.UpdateAsync(admin.Id, new(0.05m, null), default));
            await Assert.ThrowsAsync<BusinessException>(() => settings.UpdateAsync(admin.Id, new(1.1m, initial.Revision), default));

            // An environment value deliberately disagrees with database policy and must be ignored.
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Settlement:PlatformFeeRate"] = "0.9", ["Membership:SpendPerPoint"] = "0" }).Build();
            var finance = new FinanceService(store, config);
            async Task<SellerSettlement> Complete(decimal gross)
            {
                var order = new Order { UserId = user.Id, OrderCode = Guid.NewGuid().ToString("N"), IdempotencyKey = Guid.NewGuid().ToString("N"), Status = "COMPLETED", PaymentStatus = "PAID", Subtotal = gross, ShippingFee = 100m, TotalAmount = gross + 100m };
                var sellerOrder = new SellerOrder { OrderId = order.Id, BrandId = brand.Id, StoreId = storeId, Status = "PENDING", Subtotal = gross, TotalAmount = gross };
                db.AddRange(order, sellerOrder); await db.SaveChangesAsync();
                await using var tx = await store.BeginTransactionAsync(default);
                await finance.AccrueOrderAsync(order.Id, default); await tx.CommitAsync();
                return await db.SellerSettlements.SingleAsync(s => s.SellerOrderId == sellerOrder.Id);
            }
            var old = await Complete(100_000m); Assert.Equal(0m, old.FeeAmount);
            var current = await settings.UpdateAsync(admin.Id, new(0.05m, initial.Revision), default);
            Assert.Equal(0.05m, current.PlatformFeeRate); Assert.Equal(admin.Id, current.UpdatedByAccountId); Assert.NotEqual(initial.Revision, current.Revision);
            Assert.Equal(409, (await Assert.ThrowsAsync<BusinessException>(() => settings.UpdateAsync(admin.Id, new(0.1m, initial.Revision), default))).StatusCode);
            Assert.Equal(current.Revision, (await settings.UpdateAsync(admin.Id, new(0.05m, current.Revision), default)).Revision);
            Assert.Equal(1, await db.AuditLogs.CountAsync(a => a.Action == "UPDATE_PLATFORM_FEE"));
            var charged = await Complete(1999.99m); Assert.Equal(100m, charged.FeeAmount); Assert.Equal(1899.99m, charged.NetAmount);
            var later = await settings.UpdateAsync(admin.Id, new(0.1m, current.Revision), default);
            await using (var tx = await store.BeginTransactionAsync(default))
            {
                var oldOrderId = (await db.SellerOrders.SingleAsync(s => s.Id == charged.SellerOrderId)).OrderId;
                await finance.AccrueOrderAsync(oldOrderId, default); await tx.CommitAsync();
            }
            Assert.Equal(100m, (await db.SellerSettlements.AsNoTracking().SingleAsync(s => s.Id == charged.Id)).FeeAmount);
            Assert.Equal(0m, (await db.SellerSettlements.AsNoTracking().SingleAsync(s => s.Id == old.Id)).FeeAmount);
            Assert.Equal(1000m, (await Complete(10_000m)).FeeAmount);
            db.AccountRoles.Remove(role); await db.SaveChangesAsync();
            Assert.Equal(403, (await Assert.ThrowsAsync<BusinessException>(() => settings.UpdateAsync(admin.Id, new(0m, later.Revision), default))).StatusCode);
            Assert.Equal(0.1m, (await db.PlatformFeeConfigurations.SingleAsync()).PlatformFeeRate);
            db.AccountRoles.Add(new AccountRole { AccountId = admin.Id, Role = "SuperAdmin" }); admin.Status = 0; await db.SaveChangesAsync();
            Assert.Equal(403, (await Assert.ThrowsAsync<BusinessException>(() => settings.GetAsync(admin.Id, default))).StatusCode);
            Assert.Equal(403, (await Assert.ThrowsAsync<BusinessException>(() => settings.UpdateAsync(admin.Id, new(0m, later.Revision), default))).StatusCode);
            admin.Status = 1; await db.SaveChangesAsync();
            await using (var freshDb = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(scratch.ConnectionString).Options))
            {
                var persisted = await new PlatformFeeConfigurationService(new CommerceStore(freshDb)).GetAsync(admin.Id, default);
                Assert.Equal(0.1m, persisted.PlatformFeeRate); Assert.Equal(later.Revision, persisted.Revision);
            }
            await db.GetService<IMigrator>().MigrateAsync("20261112015152_MergeProductReviewImages");
            Assert.Equal(3, await db.SellerSettlements.CountAsync());
            await db.GetService<IMigrator>().MigrateAsync();
            Assert.Equal(0m, (await db.PlatformFeeConfigurations.AsNoTracking().SingleAsync()).PlatformFeeRate);
            Assert.Equal(100m, (await db.SellerSettlements.AsNoTracking().SingleAsync(s => s.Id == charged.Id)).FeeAmount);
        }
        finally
        {
            NpgsqlConnection.ClearAllPools();
            await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{scratch.Database}\" WITH (FORCE)", adminConnection); await drop.ExecuteNonQueryAsync();
        }
    }
}
