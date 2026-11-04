using BCOBCA.BLL.Common;
using BCOBCA.BLL.DTOs;
using BCOBCA.BLL.Services;
using BCOBCA.BLL.Validators;
using BCOBCA.DAL.Context;
using BCOBCA.DAL.Entities;
using BCOBCA.DAL.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Xunit;

namespace BCOBCA.Tests;

public sealed class VoucherRedemptionFlowTests
{
    [Fact]
    public async Task RedeemingOffersPreservesLifetimeAndEnforcesOwnershipLevelLimitsAndIdempotency()
    {
        EnvironmentBootstrap.Load();
        var databaseName = "bcobca_vouchers_test_" + Guid.NewGuid().ToString("N");
        await using var admin = new NpgsqlConnection(Environment.GetEnvironmentVariable("ConnectionStrings__Default"));
        await admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE DATABASE \"{databaseName}\"", admin)) await create.ExecuteNonQueryAsync();
        try
        {
            var connection = new NpgsqlConnectionStringBuilder(admin.ConnectionString) { Database = databaseName }.ConnectionString;
            var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection).Options;
            await using var db = new ApplicationDbContext(options); await db.Database.MigrateAsync();
            Assert.False(db.Database.HasPendingModelChanges());
            var config = new ConfigurationBuilder().AddEnvironmentVariables().Build();
            var store = new CommerceStore(db);
            var auth = new AuthService(store, config, new RegisterValidator(), new LoginValidator(), new ProfileValidator());
            var buyer = await auth.RegisterAsync(new RegisterRequest("vouchers@example.com", "StrongPassword123!", "Voucher buyer"), default);
            var poor = await auth.RegisterAsync(new RegisterRequest("poor@example.com", "StrongPassword123!", "Low balance"), default);
            var members = new MemberService(store);
            var redeem = await db.Vouchers.SingleAsync(x => x.Code == "BCADOI30K");
            var levelOffer = await db.Vouchers.SingleAsync(x => x.Code == "BCALV350K");
            var buyerPoints = await db.UserMemberships.SingleAsync(x => x.UserId == buyer.User.Id);
            buyerPoints.AvailablePoints = 1000; buyerPoints.LifetimeEarnedPoints = 1600; await db.SaveChangesAsync();
            var catalog = await members.VouchersAsync(buyer.User.Id, new CatalogQuery { PageSize = 100 }, default);
            Assert.Equal(500, catalog.Items.Single(x => x.Id == redeem.Id).PointCost);
            Assert.Equal(3, catalog.Items.Single(x => x.Id == levelOffer.Id).MinLevel);
            var publicCatalog = await members.VouchersAsync(null, new CatalogQuery { PageSize = 100 }, default);
            Assert.Equal(6, publicCatalog.Items.Count); Assert.All(publicCatalog.Items, x => Assert.False(x.Claimed));
            Assert.Equal("MEMBER_OFFER", redeem.Status);
            Assert.Equal("MEMBER_OFFER", levelOffer.Status);
            await Assert.ThrowsAsync<BusinessException>(() => members.ClaimAsync(poor.User.Id, redeem.Id, default));
            await Assert.ThrowsAsync<BusinessException>(() => members.ClaimAsync(poor.User.Id, levelOffer.Id, default));
            Assert.Empty(await db.UserVouchers.ToListAsync());

            var category = new Category { Name = "Test", Slug = "voucher-test", IsActive = true };
            var brand = new Brand { Name = "Test", Slug = "voucher-test", OwnerAccountId = buyer.User.AccountId, Status = 1, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };
            var product = new Product { Name = "Voucher product", Slug = "voucher-product", SKU = "VOUCHER-P", BrandId = brand.Id, CategoryId = category.Id, BasePrice = 500000, Status = 1, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };
            var variant = new ProductVariant { ProductId = product.Id, SKU = "VOUCHER-V", VariantName = "Default", Price = 500000, StockQuantity = 10, IsActive = true };
            db.Categories.Add(category); db.Brands.Add(brand); db.Products.Add(product); db.ProductVariants.Add(variant); await db.SaveChangesAsync();
            var orders = new OrderService(store, config, new FinanceService(store, config));
            await orders.AddAsync(buyer.User.Id, new(product.Id, variant.Id, 1), default);
            await Assert.ThrowsAsync<BusinessException>(() => orders.PreviewAsync(buyer.User.Id, new(redeem.Code), default));

            async Task TryClaim(Guid userId, Guid voucherId)
            {
                await using var concurrent = new ApplicationDbContext(options);
                try { await new MemberService(new CommerceStore(concurrent)).ClaimAsync(userId, voucherId, default); }
                catch (Exception ex) when (DatabaseConflicts.IsConcurrency(ex)) { }
            }
            await Task.WhenAll(TryClaim(buyer.User.Id, redeem.Id), TryClaim(buyer.User.Id, redeem.Id));
            db.ChangeTracker.Clear();
            buyerPoints = await db.UserMemberships.SingleAsync(x => x.UserId == buyer.User.Id);
            Assert.Equal(500, buyerPoints.AvailablePoints); Assert.Equal(1600, buyerPoints.LifetimeEarnedPoints);
            Assert.Equal(6, (await members.MembershipAsync(buyer.User.Id, default)).LevelInfo!.Level);
            await members.ClaimAsync(buyer.User.Id, redeem.Id, default);
            Assert.Equal(500, buyerPoints.AvailablePoints);
            Assert.False((await members.VouchersAsync(null, new(), default)).Items.Single(x => x.Id == redeem.Id).Claimed);
            var claim = await db.UserVouchers.SingleAsync(x => x.UserId == buyer.User.Id && x.VoucherId == redeem.Id);
            var debit = await db.PointTransactions.SingleAsync(x => x.MembershipId == buyerPoints.Id && x.Type == "VOUCHER_REDEEM");
            Assert.Equal(-500, debit.Points); Assert.Equal(claim.Id, debit.ReferenceId);
            Assert.Equal(30000, (await orders.PreviewAsync(buyer.User.Id, new(redeem.Code), default)).DiscountAmount);
            var address = await members.SaveAddressAsync(buyer.User.Id, null, new("Buyer", "0901234567", "79", "001", "Test address", true), default);
            var order = await orders.CheckoutAsync(buyer.User.Id, "redeem-checkout", new(address.Id, VoucherCode: redeem.Code), default);
            await orders.CancelAsync(buyer.User.Id, order.Id, default);
            Assert.Equal("AVAILABLE", claim.Status); Assert.Equal(500, buyerPoints.AvailablePoints);
            await members.ClaimAsync(buyer.User.Id, redeem.Id, default); Assert.Equal(500, buyerPoints.AvailablePoints);
            await orders.AddAsync(poor.User.Id, new(product.Id, variant.Id, 1), default);
            await Assert.ThrowsAsync<BusinessException>(() => orders.PreviewAsync(poor.User.Id, new(levelOffer.Code), default));

            var poorPoints = await db.UserMemberships.SingleAsync(x => x.UserId == poor.User.Id);
            poorPoints.AvailablePoints = 1000; poorPoints.LifetimeEarnedPoints = 1600; poorPoints.PointDebt = 1; await db.SaveChangesAsync();
            await Assert.ThrowsAsync<BusinessException>(() => members.ClaimAsync(poor.User.Id, redeem.Id, default));
            poorPoints.PointDebt = 0; await db.SaveChangesAsync();
            var limited = new Voucher { Code = "LAST-REWARD", Name = "One reward", DiscountType = "FIXED", DiscountValue = 10000, PointCost = 200, MinLevel = 1, PerUserLimit = 1, UsageLimit = 1, StartsAt = DateTimeOffset.UtcNow.AddMinutes(-1), ExpiresAt = DateTimeOffset.UtcNow.AddDays(1), Status = "ACTIVE" };
            db.Vouchers.Add(limited); await db.SaveChangesAsync();
            async Task RaceLimited(Guid userId)
            {
                try { await TryClaim(userId, limited.Id); } catch (BusinessException ex) when (ex.StatusCode == 409) { }
            }
            await Task.WhenAll(RaceLimited(buyer.User.Id), RaceLimited(poor.User.Id));
            Assert.Single(await db.UserVouchers.Where(x => x.VoucherId == limited.Id).ToListAsync());
            Assert.Equal(-200, await db.PointTransactions.Where(x => x.Type == "VOUCHER_REDEEM" && x.ReferenceId != claim.Id).SumAsync(x => x.Points));
            var loser = await db.UserVouchers.AnyAsync(x => x.VoucherId == limited.Id && x.UserId == buyer.User.Id) ? poor.User.Id : buyer.User.Id;
            await Assert.ThrowsAsync<BusinessException>(() => members.ClaimAsync(loser, limited.Id, default));
            db.AccountRoles.Add(new AccountRole { AccountId = buyer.User.AccountId, Role = "Admin" }); await db.SaveChangesAsync();
            var adminCatalog = new AdminCatalogService(store);
            var input = new VoucherInput(redeem.Code, redeem.Name, redeem.Description, redeem.DiscountType, redeem.DiscountValue, redeem.MinOrderAmount, 1, redeem.StartsAt, redeem.ExpiresAt, [], UsageLimit: redeem.UsageLimit, PointCost: 100, MinLevel: 1);
            await Assert.ThrowsAsync<BusinessException>(() => adminCatalog.SaveVoucherAsync(buyer.User.AccountId, redeem.Id, input, default));
            await Assert.ThrowsAsync<BusinessException>(() => adminCatalog.SaveVoucherAsync(buyer.User.AccountId, null, input with { Code = "INVALID", PointCost = -1 }, default));
            limited.Status = "INACTIVE"; await db.SaveChangesAsync();
            await Assert.ThrowsAsync<BusinessException>(() => members.ClaimAsync(poor.User.Id, limited.Id, default));
        }
        finally
        {
            NpgsqlConnection.ClearAllPools();
            await using var drop = new NpgsqlCommand($"DROP DATABASE \"{databaseName}\" WITH (FORCE)", admin); await drop.ExecuteNonQueryAsync();
        }
    }
}
