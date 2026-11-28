using BCOBCA.BLL.Common;
using BCOBCA.BLL.DTOs;
using BCOBCA.BLL.Services;
using BCOBCA.DAL.Context;
using BCOBCA.DAL.Entities;
using BCOBCA.DAL.Repositories;
using Xunit;

namespace BCOBCA.Tests;

public sealed class AdminVoucherScopeTests
{
    [Fact]
    public async Task AdminScopeFiltersRowsAndStatisticsBeforePagingAndSearchesDescription()
    {
        await SellerVerificationTests.WithDatabase(async options =>
        {
            await using var db = new ApplicationDbContext(options);
            var admin = new Account { Email = "voucher-scope-admin@example.test", Role = "Admin", Status = 1 };
            var owner = new Account { Email = "voucher-scope-owner@example.test", Status = 1 };
            var shop = new Store { OwnerAccountId = owner.Id, Name = "Scope Shop", Slug = "voucher-scope", StoreType = 1, Status = 1 };
            var now = DateTimeOffset.UtcNow;
            var platform = new Voucher { Code = "SCOPE_PLATFORM", Description = "Ưu đãi toàn sàn", Status = "ACTIVE", DiscountType = "FIXED", DiscountValue = 10000, PerUserLimit = 1, StartsAt = now.AddDays(-1), ExpiresAt = now.AddDays(2) };
            var seller = new Voucher { StoreId = shop.Id, Code = "SCOPE_SHOP", Description = "Ưu đãi của shop", Status = "ACTIVE", DiscountType = "FIXED", DiscountValue = 20000, PerUserLimit = 1, StartsAt = now.AddDays(1), ExpiresAt = now.AddDays(3) };
            db.AddRange(admin, owner, shop, platform, seller); await db.SaveChangesAsync();
            var service = new VoucherManagementService(new CommerceStore(db));
            var platformPage = await service.ListAsync(admin.Id, null, new VoucherManagementQuery(Scope: "PLATFORM"), true, default);
            Assert.Equal(platform.Id, Assert.Single(platformPage.Vouchers.Items).Id);
            Assert.Equal(1, platformPage.Statistics.Active); Assert.Equal(0, platformPage.Statistics.Upcoming);
            var shopPage = await service.ListAsync(admin.Id, null, new VoucherManagementQuery(Scope: "SHOP"), true, default);
            Assert.Equal(seller.Id, Assert.Single(shopPage.Vouchers.Items).Id);
            Assert.Equal(0, shopPage.Statistics.Active); Assert.Equal(1, shopPage.Statistics.Upcoming);
            Assert.Single((await service.ListAsync(admin.Id, null, new VoucherManagementQuery(Search: "toàn sàn", Scope: "PLATFORM"), true, default)).Vouchers.Items);
            Assert.Equal(2, (await service.ListAsync(admin.Id, null, new(), true, default)).Vouchers.TotalItems);
            Assert.Single((await service.ListAsync(owner.Id, shop.Id, new(), false, default)).Vouchers.Items);
            await Assert.ThrowsAsync<BusinessException>(() => service.ListAsync(owner.Id, shop.Id, new(Scope: "PLATFORM"), false, default));
            await Assert.ThrowsAsync<BusinessException>(() => service.ListAsync(admin.Id, null, new(Scope: "INVALID"), true, default));
        });
    }
}
