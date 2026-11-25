using BCOBCA.BLL.DTOs;
using BCOBCA.BLL.Services;
using BCOBCA.DAL.Context;
using BCOBCA.DAL.Entities;
using BCOBCA.DAL.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BCOBCA.Tests;

public sealed class VoucherProgramNameTests
{
    [Fact]
    public async Task RemovesNameColumnAndClearsPercentageCapWhenSwitchingToFixed()
    {
        await SellerVerificationTests.WithDatabase(async options =>
        {
            await using var db = new ApplicationDbContext(options);
            Assert.False(db.Database.HasPendingModelChanges());
            var columns = await db.Database.SqlQueryRaw<int>("SELECT count(*)::int AS \"Value\" FROM information_schema.columns WHERE table_name = 'Vouchers' AND column_name = 'Name'").SingleAsync();
            Assert.Equal(0, columns);
            var owner = new Account { Email = "voucher-form@example.test", Status = 1 };
            var shop = new Store { OwnerAccountId = owner.Id, Name = "Shop", Slug = "voucher-form", StoreType = 1, Status = 1 };
            db.AddRange(owner, shop); await db.SaveChangesAsync();
            var service = new VoucherManagementService(new CommerceStore(db));
            var now = DateTimeOffset.UtcNow;
            var input = new SellerVoucherInput("PERCENT10", null, "PERCENT", 10, 20000, 0, 10, 1, now.AddDays(1), now.AddDays(7), []);
            var percent = await service.SaveAsync(owner.Id, shop.Id, null, input, default);
            Assert.Equal(20000m, percent.MaxDiscount);
            var fixedVoucher = await service.SaveAsync(owner.Id, shop.Id, percent.Id, input with { DiscountType = "FIXED", DiscountValue = 50000 }, default);
            Assert.Null(fixedVoucher.MaxDiscount);
            db.ChangeTracker.Clear();
            Assert.Null((await db.Vouchers.SingleAsync(v => v.Id == fixedVoucher.Id)).MaxDiscount);
            Assert.Equal(fixedVoucher.Code, fixedVoucher.Name);
        });
    }
}
