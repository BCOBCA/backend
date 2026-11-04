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

public sealed class ShippingVoucherFlowTests
{
    [Fact]
    public async Task ShippingDiscountsAreCappedAndDoNotReduceMerchandisePointsCommissionOrRefundAccuracy()
    {
        EnvironmentBootstrap.Load();
        var databaseName = "bcobca_shipping_test_" + Guid.NewGuid().ToString("N");
        await using var admin = new NpgsqlConnection(Environment.GetEnvironmentVariable("ConnectionStrings__Default")); await admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE DATABASE \"{databaseName}\"", admin)) await create.ExecuteNonQueryAsync();
        try
        {
            var connection = new NpgsqlConnectionStringBuilder(admin.ConnectionString) { Database = databaseName }.ConnectionString;
            await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection).Options);
            await db.Database.MigrateAsync(); Assert.False(db.Database.HasPendingModelChanges());
            var config = new ConfigurationBuilder().AddEnvironmentVariables().AddInMemoryCollection(new Dictionary<string, string?> { ["Checkout:ShippingFee"] = "20000", ["Membership:SpendPerPoint"] = "10000", ["Settlement:PlatformFeeRate"] = "0.1" }).Build();
            var store = new CommerceStore(db);
            var auth = new AuthService(store, config, new RegisterValidator(), new LoginValidator(), new ProfileValidator());
            var owner = await auth.RegisterAsync(new RegisterRequest("ship-owner@example.com", "StrongPassword123!", "Owner"), default);
            var buyer = await auth.RegisterAsync(new RegisterRequest("ship-buyer@example.com", "StrongPassword123!", "Buyer", ReferralCode: owner.User.ReferralCode), default);
            db.AccountRoles.Add(new AccountRole { AccountId = owner.User.AccountId, Role = "Admin" });
            var member = await db.UserMemberships.SingleAsync(x => x.UserId == buyer.User.Id); member.AvailablePoints = 2000; member.LifetimeEarnedPoints = 2000;
            var category = new Category { Name = "Shipping test", Slug = "shipping-test", IsActive = true };
            var brand = new Brand { Name = "Shipping test", Slug = "shipping-test", OwnerAccountId = owner.User.AccountId, Status = 1, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };
            var product = new Product { Name = "Shipping test", Slug = "shipping-test", SKU = "SHIP-P", BrandId = brand.Id, CategoryId = category.Id, BasePrice = 500000, Status = 1, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };
            var variant = new ProductVariant { ProductId = product.Id, SKU = "SHIP-V", VariantName = "Default", Price = 500000, StockQuantity = 5, IsActive = true };
            db.Categories.Add(category); db.Brands.Add(brand); db.Products.Add(product); db.ProductVariants.Add(variant); await db.SaveChangesAsync();
            var members = new MemberService(store); var finance = new FinanceService(store, config); var orders = new OrderService(store, config, finance);
            var free = await db.Vouchers.SingleAsync(x => x.Code == "BCASHIP30K");
            var paid = await db.Vouchers.SingleAsync(x => x.Code == "BCADOISHIP30K");
            Assert.Equal("SHIPPING", free.DiscountType); Assert.Equal("MEMBER_OFFER", free.Status); Assert.Equal(1000, paid.PointCost);
            await orders.AddAsync(buyer.User.Id, new(product.Id, variant.Id, 1), default);
            await Assert.ThrowsAsync<BusinessException>(() => orders.PreviewAsync(buyer.User.Id, new(paid.Code), default));
            foreach (var shippingFee in new[] { 20000m, 50000m })
            {
                config["Checkout:ShippingFee"] = shippingFee.ToString(System.Globalization.CultureInfo.InvariantCulture);
                var preview = await orders.PreviewAsync(buyer.User.Id, new(free.Code), default);
                Assert.Equal(0, preview.DiscountAmount); Assert.Equal(Math.Min(shippingFee, 30000), preview.ShippingDiscountAmount);
                Assert.Equal(500000 + shippingFee - Math.Min(shippingFee, 30000), preview.TotalAmount);
            }
            config["Checkout:ShippingFee"] = "0";
            await Assert.ThrowsAsync<BusinessException>(() => orders.PreviewAsync(buyer.User.Id, new(free.Code), default));
            config["Checkout:ShippingFee"] = "20000";
            variant.Price = 299999; await db.SaveChangesAsync();
            await Assert.ThrowsAsync<BusinessException>(() => orders.PreviewAsync(buyer.User.Id, new(free.Code), default));
            variant.Price = 500000; await db.SaveChangesAsync();
            await members.ClaimAsync(buyer.User.Id, free.Id, default);
            var address = await members.SaveAddressAsync(buyer.User.Id, null, new("Buyer", "0901234567", "79", "001", "Shipping test address", true), default);
            var input = new CheckoutRequest(address.Id, VoucherCode: free.Code, ExpectedTotal: 500000);
            var order = await orders.CheckoutAsync(buyer.User.Id, "free-shipping", input, default);
            Assert.Equal(order.Id, (await orders.CheckoutAsync(buyer.User.Id, "free-shipping", input, default)).Id);
            Assert.Equal(20000, order.ShippingFee); Assert.Equal(20000, order.ShippingDiscountAmount); Assert.Equal(500000, order.TotalAmount); Assert.Equal(0, order.DiscountAmount);
            Assert.Equal(500000, order.Items.Single().TotalPrice);
            Assert.Equal(20000, (await db.VoucherUsages.SingleAsync(x => x.OrderId == order.Id)).DiscountAmount);
            Assert.Equal(20000, (await db.OrderVoucherApplications.SingleAsync(x => x.OrderId == order.Id)).DiscountAmount);
            foreach (var status in new[] { "CONFIRMED", "READY", "SHIPPING" }) await orders.TransitionAsync(owner.User.AccountId, order.Id, new(status), default);
            await orders.TransitionAsync(owner.User.AccountId, order.Id, new("COMPLETED", true), default);
            Assert.Equal(2050, member.AvailablePoints); Assert.Equal(2050, member.LifetimeEarnedPoints);
            var commission = await db.AffiliateCommissions.SingleAsync(); Assert.Equal(500000, commission.BaseAmount); Assert.Equal(10000, commission.CommissionAmount);
            await members.ClaimAsync(buyer.User.Id, paid.Id, default); await members.ClaimAsync(buyer.User.Id, paid.Id, default);
            Assert.Equal(1050, member.AvailablePoints); Assert.Equal(2050, member.LifetimeEarnedPoints);
            await orders.AddAsync(buyer.User.Id, new(product.Id, variant.Id, 1), default);
            config["Checkout:ShippingFee"] = "50000";
            var paidOrder = await orders.CheckoutAsync(buyer.User.Id, "paid-shipping", new(address.Id, VoucherCode: paid.Code, ExpectedTotal: 520000), default);
            Assert.Equal(30000, paidOrder.ShippingDiscountAmount);
            await orders.CancelAsync(buyer.User.Id, paidOrder.Id, default);
            Assert.Equal("AVAILABLE", (await db.UserVouchers.SingleAsync(x => x.VoucherId == paid.Id && x.UserId == buyer.User.Id)).Status);
            Assert.Equal(1050, member.AvailablePoints);
            var refunds = new RefundSettlementService(store, config);
            var refundId = await refunds.RequestRefundAsync(buyer.User.Id, order.Id, "shipping-refund", new("Return"), default);
            Assert.Equal(500000, (await db.Refunds.SingleAsync(x => x.Id == refundId)).Amount);
            await refunds.SetRefundStatusAsync(owner.User.AccountId, refundId, new("APPROVED"), default);
            await refunds.SetRefundStatusAsync(owner.User.AccountId, refundId, new("COMPLETED", "SHIP-REFUND", true), default);
            Assert.Equal(2000, member.LifetimeEarnedPoints); Assert.Equal(1000, member.AvailablePoints); Assert.Equal("REVERSED", commission.Status);
            var adminCatalog = new AdminCatalogService(store);
            var created = await adminCatalog.SaveVoucherAsync(owner.User.AccountId, null, new("CUSTOMSHIP", "Custom shipping", "Shipping", "SHIPPING", 30000, 300000, 1, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(1), []), default);
            Assert.Equal("MEMBER_OFFER", (await db.Vouchers.SingleAsync(x => x.Id == created)).Status);
        }
        finally
        {
            NpgsqlConnection.ClearAllPools(); await using var drop = new NpgsqlCommand($"DROP DATABASE \"{databaseName}\" WITH (FORCE)", admin); await drop.ExecuteNonQueryAsync();
        }
    }
}
