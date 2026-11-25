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

public sealed class SellerVoucherFlowTests
{
    [Fact]
    public async Task SellersPublishWithoutApprovalScopeProductsAndRespectOwnershipLimitsDisableAndCancellation()
    {
        EnvironmentBootstrap.Load();
        var original = Environment.GetEnvironmentVariable("ConnectionStrings__Default");
        var database = "bcobca_seller_voucher_" + Guid.NewGuid().ToString("N");
        await using var admin = new NpgsqlConnection(original); await admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE DATABASE \"{database}\"", admin)) await create.ExecuteNonQueryAsync();
        try
        {
            var connection = new NpgsqlConnectionStringBuilder(original) { Database = database }.ConnectionString;
            var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection).Options;
            await using var db = new ApplicationDbContext(options); await db.Database.MigrateAsync();
            Assert.False(db.Database.HasPendingModelChanges());
            var repo = new CommerceStore(db); var config = new ConfigurationBuilder().AddEnvironmentVariables().Build();
            var auth = new AuthService(repo, config, new RegisterValidator(), new LoginValidator(), new ProfileValidator());
            var owner = await auth.RegisterAsync(new("sv-owner@example.com", "StrongPassword123!", "Owner"), default);
            var other = await auth.RegisterAsync(new("sv-other@example.com", "StrongPassword123!", "Other"), default);
            var buyer = await auth.RegisterAsync(new("sv-buyer@example.com", "StrongPassword123!", "Buyer"), default);
            var buyer2 = await auth.RegisterAsync(new("sv-buyer2@example.com", "StrongPassword123!", "Buyer2"), default);
            var seller = new SellerService(repo);
            var shop = await seller.OnboardAsync(owner.User.AccountId, default); var otherShop = await seller.OnboardAsync(other.User.AccountId, default);
            var brand = new Brand { Name = "Seller vouchers", Slug = "seller-vouchers", OwnerAccountId = owner.User.AccountId, Status = 1 };
            var category = new Category { Name = "Seller vouchers", Slug = "seller-vouchers", IsActive = true };
            var a = new Product { StoreId = shop.Id, BrandId = brand.Id, CategoryId = category.Id, Name = "Shop A", Slug = "sv-a", SKU = "SVA", Status = 1, BasePrice = 200000 };
            var b = new Product { StoreId = otherShop.Id, BrandId = brand.Id, CategoryId = category.Id, Name = "Shop B", Slug = "sv-b", SKU = "SVB", Status = 1, BasePrice = 300000 };
            var c = new Product { StoreId = shop.Id, BrandId = brand.Id, CategoryId = category.Id, Name = "Shop A second product", Slug = "sv-c", SKU = "SVC", Status = 1, BasePrice = 250000 };
            var av = new ProductVariant { ProductId = a.Id, SKU = "SVA-V", VariantName = "A", Price = 200000, StockQuantity = 20, IsActive = true };
            var bv = new ProductVariant { ProductId = b.Id, SKU = "SVB-V", VariantName = "B", Price = 300000, StockQuantity = 20, IsActive = true };
            var cv = new ProductVariant { ProductId = c.Id, SKU = "SVC-V", VariantName = "C", Price = 250000, StockQuantity = 20, IsActive = true };
            db.AddRange(brand, category, a, b, c, av, bv, cv); await db.SaveChangesAsync();
            var service = new VoucherManagementService(repo); var members = new MemberService(repo); var now = DateTimeOffset.UtcNow;
            var input = new SellerVoucherInput("SELLER10", "Shop tự chịu giảm giá", "PERCENT", 10, 20000, 100000, 10, 1, now.AddMinutes(-1), now.AddDays(7), [a.Id]);
            await Assert.ThrowsAsync<BusinessException>(() => service.SaveAsync(other.User.AccountId, shop.Id, null, input, default));
            await Assert.ThrowsAsync<BusinessException>(() => service.SaveAsync(owner.User.AccountId, shop.Id, null, input with { ProductIds = [b.Id] }, default));
            await Assert.ThrowsAsync<BusinessException>(() => service.SaveAsync(owner.User.AccountId, shop.Id, null, input with { MaxDiscount = null }, default));
            var active = await service.SaveAsync(owner.User.AccountId, shop.Id, null, input, default);
            Assert.Equal("ACTIVE", active.Status); Assert.Equal(a.Id, Assert.Single(active.ProductIds));
            var publicShop = await members.VouchersAsync(null, new(StoreId: shop.Id, PageSize: 100), default);
            Assert.Equal(shop.Id, Assert.Single(publicShop.Items).StoreId);
            var future = await service.SaveAsync(owner.User.AccountId, shop.Id, null, input with { Code = "FUTURE", StartsAt = now.AddDays(1) }, default);
            Assert.True(future.CanEdit);
            future = await service.SaveAsync(owner.User.AccountId, shop.Id, future.Id, input with { Code = "FUTURE", StartsAt = now.AddDays(2) }, default);
            var copy = await service.DuplicateAsync(owner.User.AccountId, shop.Id, future.Id, input with { Code = "COPY", StartsAt = now.AddDays(3), ProductIds = [] }, default);
            Assert.NotEqual(future.Id, copy.Id);
            var list = await service.ListAsync(owner.User.AccountId, shop.Id, new(Status: "ACTIVE"), false, default);
            Assert.Single(list.Vouchers.Items); Assert.Equal(1, list.Statistics.Active); Assert.Equal(2, list.Statistics.Upcoming);
            Assert.Equal(2, (await service.ListAsync(owner.User.AccountId, shop.Id, new(Status: "UPCOMING"), false, default)).Vouchers.TotalItems);
            await Assert.ThrowsAsync<BusinessException>(() => service.DetailAsync(other.User.AccountId, otherShop.Id, active.Id, 1, false, default));
            await Assert.ThrowsAsync<BusinessException>(() => service.ListAsync(other.User.AccountId, null, new(), true, default));
            var orders = TestOrders.Create(repo, config, new FinanceService(repo, config));
            var address = await members.SaveAddressAsync(buyer.User.Id, null, new("Buyer", "0901234567", "79", "001", "Test", true), default);
            await orders.AddAsync(buyer.User.Id, new(a.Id, av.Id, 1), default); await orders.AddAsync(buyer.User.Id, new(b.Id, bv.Id, 1), default);
            await Assert.ThrowsAsync<BusinessException>(() => orders.PreviewAsync(buyer.User.Id, new(active.Code), default));
            await members.ClaimAsync(buyer.User.Id, active.Id, default);
            await members.ClaimAsync(buyer.User.Id, active.Id, default);
            Assert.Equal(1, await db.UserVouchers.CountAsync(x => x.UserId == buyer.User.Id && x.VoucherId == active.Id));
            Assert.Equal(shop.Id, Assert.Single((await members.MyVouchersAsync(buyer.User.Id, new(Status: "AVAILABLE"), default)).Items).StoreId);
            await Assert.ThrowsAsync<BusinessException>(() => service.SaveAsync(owner.User.AccountId, shop.Id, active.Id, input, default));
            var checkout = await orders.CheckoutAsync(buyer.User.Id, "seller-voucher-multi", new(address.Id, VoucherCode: active.Code), default);
            var split = await db.Orders.Where(x => x.CheckoutGroupId == checkout.CheckoutGroupId).OrderBy(x => x.CreatedAt).ToListAsync();
            Assert.Equal(2, split.Count); Assert.Equal(20000m, split.Sum(x => x.DiscountAmount));
            Assert.Equal(0m, await db.OrderItems.Where(x => x.StoreId == otherShop.Id).SumAsync(x => x.DiscountAmount));
            var detail = await service.DetailAsync(owner.User.AccountId, shop.Id, active.Id, 1, false, default);
            Assert.Equal(1, detail.Voucher.UsedCount); Assert.Equal(20000m, Assert.Single(detail.Usages.Items).DiscountAmount);
            foreach (var order in split) await orders.CancelAsync(buyer.User.Id, order.Id, new(OrderCancellationReasons.BuyerChoices[0]), default);
            Assert.Equal(0, (await service.DetailAsync(owner.User.AccountId, shop.Id, active.Id, 1, false, default)).Voucher.UsedCount);
            await service.DisableAsync(owner.User.AccountId, shop.Id, active.Id, new("Kết thúc chương trình"), false, default);
            await service.DisableAsync(owner.User.AccountId, shop.Id, active.Id, new("Lặp lại"), false, default);
            Assert.Single((await service.DetailAsync(owner.User.AccountId, shop.Id, active.Id, 1, false, default)).Actions, x => x.Action == "SELLER_DISABLE_VOUCHER");
            await Assert.ThrowsAsync<BusinessException>(() => members.ClaimAsync(buyer2.User.Id, active.Id, default));
            await orders.AddAsync(buyer.User.Id, new(a.Id, av.Id, 1), default);
            await Assert.ThrowsAsync<BusinessException>(() => orders.PreviewAsync(buyer.User.Id, new(active.Code), default));
            db.AccountRoles.Add(new AccountRole { AccountId = other.User.AccountId, Role = "Admin" }); await db.SaveChangesAsync();
            await service.DisableAsync(other.User.AccountId, null, future.Id, new("Vi phạm quy định"), true, default);
            Assert.Equal("DISABLED", (await service.DetailAsync(other.User.AccountId, null, future.Id, 1, true, default)).Voucher.Status);

            var last = await service.SaveAsync(owner.User.AccountId, shop.Id, null, input with { Code = "LAST", UsageLimit = 1, ProductIds = [] }, default);
            await members.ClaimAsync(buyer.User.Id, last.Id, default); await members.ClaimAsync(buyer2.User.Id, last.Id, default);
            var address2 = await members.SaveAddressAsync(buyer2.User.Id, null, new("Buyer2", "0901234567", "79", "001", "Test", true), default);
            await orders.AddAsync(buyer.User.Id, new(a.Id, av.Id, 1), default); await orders.AddAsync(buyer2.User.Id, new(c.Id, cv.Id, 1), default);
            async Task<bool> Race(Guid user, Guid shippingAddress, string key)
            {
                await using var concurrent = new ApplicationDbContext(options); var repository = new CommerceStore(concurrent);
                try { await TestOrders.Create(repository, config, new FinanceService(repository, config)).CheckoutAsync(user, key, new(shippingAddress, VoucherCode: last.Code), default); return true; }
                catch (Exception e) when (e is BusinessException || DatabaseConflicts.IsConcurrency(e)) { return false; }
            }
            var results = await Task.WhenAll(Race(buyer.User.Id, address.Id, "last-a"), Race(buyer2.User.Id, address2.Id, "last-b"));
            Assert.Single(results, x => x);
            db.ChangeTracker.Clear();
            Assert.Equal(1, await db.VoucherUsages.CountAsync(x => x.VoucherId == last.Id && x.Status == "APPLIED"));
            Assert.Equal("EXHAUSTED", (await service.DetailAsync(owner.User.AccountId, shop.Id, last.Id, 1, false, default)).Voucher.Status);
            var reservedOrder = await db.VoucherUsages.Where(x => x.VoucherId == last.Id && x.Status == "APPLIED").Select(x => x.OrderId).SingleAsync();
            await db.InventoryReservations.Where(x => x.OrderId == reservedOrder).ExecuteUpdateAsync(set => set.SetProperty(x => x.ExpiresAt, DateTimeOffset.UtcNow.AddMinutes(-1)));
            Assert.Equal(1, await orders.ExpireOneReservationAsync(default));
            Assert.Equal(0, await db.VoucherUsages.CountAsync(x => x.VoucherId == last.Id && x.Status == "APPLIED"));
            Assert.Equal("ACTIVE", (await service.DetailAsync(owner.User.AccountId, shop.Id, last.Id, 1, false, default)).Voucher.Status);
        }
        finally
        {
            NpgsqlConnection.ClearAllPools();
            await using var drop = new NpgsqlCommand($"DROP DATABASE \"{database}\" WITH (FORCE)", admin); await drop.ExecuteNonQueryAsync();
        }
    }
}
