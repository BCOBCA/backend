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

public sealed class CollectedCheckoutVoucherFlowTests
{
    [Fact]
    public async Task OwnedVouchersChooseBestPairApplyOnceAndRestoreBothOnCancellation()
    {
        EnvironmentBootstrap.Load();
        var databaseName = "bcobca_collected_voucher_test_" + Guid.NewGuid().ToString("N");
        await using var admin = new NpgsqlConnection(Environment.GetEnvironmentVariable("ConnectionStrings__Default")); await admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE DATABASE \"{databaseName}\"", admin)) await create.ExecuteNonQueryAsync();
        try
        {
            var connection = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("ConnectionStrings__Default")) { Database = databaseName }.ConnectionString;
            await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection).Options);
            await db.Database.MigrateAsync();
            var config = new ConfigurationBuilder().AddEnvironmentVariables().AddInMemoryCollection(new Dictionary<string, string?> { ["Checkout:ShippingFee"] = "30000" }).Build();
            var store = new CommerceStore(db);
            var auth = new AuthService(store, config, new RegisterValidator(), new LoginValidator(), new ProfileValidator());
            var owner = await auth.RegisterAsync(new RegisterRequest("voucher-owner@example.com", "StrongPassword123!", "Owner"), default);
            var buyer = await auth.RegisterAsync(new RegisterRequest("voucher-buyer@example.com", "StrongPassword123!", "Buyer"), default);
            var category = new Category { Name = "Vouchers", Slug = "vouchers", IsActive = true };
            var brand = new Brand { Name = "Vouchers", Slug = "vouchers", OwnerAccountId = owner.User.AccountId, Status = 1, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };
            var product = new Product { StoreId = TestStores.Own(db, brand), Name = "Voucher test", Slug = "voucher-test", SKU = "VC-P", BrandId = brand.Id, CategoryId = category.Id, BasePrice = 500000, Status = 1, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };
            var variant = new ProductVariant { ProductId = product.Id, SKU = "VC-V", VariantName = "Default", Price = 500000, StockQuantity = 10, IsActive = true };
            var now = DateTimeOffset.UtcNow;
            Voucher NewVoucher(string code, string type, decimal amount) => new() { Code = code, Name = code, DiscountType = type, DiscountValue = amount, MinOrderAmount = 500000, PerUserLimit = 1, Status = "ACTIVE", StartsAt = now.AddDays(-1), ExpiresAt = now.AddDays(1) };
            var fixedVoucher = NewVoucher("COLLECT50", "FIXED", 50000);
            var percent = NewVoucher("COLLECTPERCENT", "PERCENT", 50); percent.MaxDiscount = 20000;
            var shipping = NewVoucher("COLLECTSHIP", "SHIPPING", 20000);
            var unowned = NewVoucher("NOTCOLLECTED", "FIXED", 100000);
            var expired = NewVoucher("COLLECTEXPIRED", "FIXED", 200000); expired.ExpiresAt = now.AddSeconds(-1);
            db.Categories.Add(category); db.Brands.Add(brand); db.Products.Add(product); db.ProductVariants.Add(variant);
            db.Vouchers.AddRange(fixedVoucher, percent, shipping, unowned, expired);
            foreach (var v in new[] { fixedVoucher, percent, shipping, expired }) db.UserVouchers.Add(new UserVoucher { UserId = buyer.User.Id, VoucherId = v.Id, Status = "AVAILABLE", ClaimedAt = now });
            await db.SaveChangesAsync();
            var members = new MemberService(store);
            var orders = new OrderService(store, config, new FinanceService(store, config));
            await orders.AddAsync(buyer.User.Id, new(product.Id, variant.Id, 1), default);
            var preview = await orders.PreviewAsync(buyer.User.Id, new(VoucherCodes: [], AutoSelectVouchers: true), default);
            Assert.Equal(new[] { fixedVoucher.Code, shipping.Code }, preview.AppliedVoucherCodes);
            Assert.Equal(4, preview.Vouchers!.Count); Assert.DoesNotContain(preview.Vouchers, x => x.Code == unowned.Code);
            Assert.False(preview.Vouchers.Single(x => x.Id == expired.Id).IsEligible);
            var legacy = await orders.PreviewAsync(buyer.User.Id, new(unowned.Code), default);
            Assert.Equal(100000, legacy.DiscountAmount); // Older clients retain public-code compatibility.
            Assert.Equal(50000, preview.DiscountAmount); Assert.Equal(20000, preview.ShippingDiscountAmount); Assert.Equal(460000, preview.TotalAmount);
            var none = await orders.PreviewAsync(buyer.User.Id, new(VoucherCodes: []), default);
            Assert.Equal(530000, none.TotalAmount); Assert.Empty(none.AppliedVoucherCodes!);
            await Assert.ThrowsAsync<BusinessException>(() => orders.PreviewAsync(buyer.User.Id, new(VoucherCodes: [unowned.Code]), default));
            await Assert.ThrowsAsync<BusinessException>(() => orders.PreviewAsync(buyer.User.Id, new(VoucherCodes: [fixedVoucher.Code, percent.Code]), default));
            var address = await members.SaveAddressAsync(buyer.User.Id, null, new("Buyer", "0901234567", "79", "001", "Test address", true), default);
            var input = new CheckoutRequest(address.Id, ExpectedTotal: preview.TotalAmount, ExpectedCartHash: preview.CartHash, VoucherCodes: preview.AppliedVoucherCodes);
            var order = await orders.CheckoutAsync(buyer.User.Id, "collected-pair", input, default);
            Assert.Equal(order.Id, (await orders.CheckoutAsync(buyer.User.Id, "collected-pair", input, default)).Id);
            Assert.Equal(460000, order.TotalAmount); Assert.Equal(450000, order.Items.Single().TotalPrice);
            Assert.Equal(new[] { 20000m, 50000m }, await db.VoucherUsages.Where(x => x.OrderId == order.Id).OrderBy(x => x.DiscountAmount).Select(x => x.DiscountAmount).ToArrayAsync());
            Assert.Equal(2, await db.OrderVoucherApplications.CountAsync(x => x.OrderId == order.Id));
            Assert.Equal(460000, (await db.Payments.SingleAsync(x => x.OrderId == order.Id)).Amount);
            Assert.All(await db.UserVouchers.Where(x => x.VoucherId == fixedVoucher.Id || x.VoucherId == shipping.Id).ToListAsync(), x => Assert.Equal("USED", x.Status));
            await orders.CancelAsync(buyer.User.Id, order.Id, default);
            Assert.All(await db.UserVouchers.Where(x => x.VoucherId == fixedVoucher.Id || x.VoucherId == shipping.Id).ToListAsync(), x => Assert.Equal("AVAILABLE", x.Status));
            Assert.Equal(0, await db.VoucherUsages.CountAsync(x => x.OrderId == order.Id && x.Status == "APPLIED"));
        }
        finally
        {
            NpgsqlConnection.ClearAllPools(); await using var drop = new NpgsqlCommand($"DROP DATABASE \"{databaseName}\" WITH (FORCE)", admin); await drop.ExecuteNonQueryAsync();
        }
    }
}
