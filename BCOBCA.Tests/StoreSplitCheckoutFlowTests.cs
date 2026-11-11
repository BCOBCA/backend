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

public sealed class StoreSplitCheckoutFlowTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task CheckoutSplitsByStoreKeepsVoucherScopeAndReleasesOnlyAfterBeneficiariesCancel(bool sellerTargeted, bool equalPrices)
    {
        EnvironmentBootstrap.Load();
        var databaseName = "bcobca_store_split_test_" + Guid.NewGuid().ToString("N");
        await using var admin = new NpgsqlConnection(Environment.GetEnvironmentVariable("ConnectionStrings__Default"));
        await admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE DATABASE \"{databaseName}\"", admin)) await create.ExecuteNonQueryAsync();
        try
        {
            var connection = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("ConnectionStrings__Default")) { Database = databaseName }.ConnectionString;
            await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection).Options);
            await db.Database.MigrateAsync();
            var config = new ConfigurationBuilder().AddEnvironmentVariables().AddInMemoryCollection(new Dictionary<string, string?> { ["Checkout:ShippingFee"] = "30000", ["Checkout:ReservationHours"] = "24" }).Build();
            var repository = new CommerceStore(db);
            var auth = new AuthService(repository, config, new RegisterValidator(), new LoginValidator(), new ProfileValidator());
            var owner = await auth.RegisterAsync(new("split-owner@example.com", "StrongPassword123!", "Owner"), default);
            var buyer = await auth.RegisterAsync(new("split-buyer@example.com", "StrongPassword123!", "Buyer"), default);
            var brand = new Brand { Name = "Shared brand", Slug = "shared-brand", OwnerAccountId = owner.User.AccountId, Status = 1 };
            var category = new Category { Name = "Products", Slug = "products", IsActive = true };
            db.AddRange(brand, category);
            var products = new List<Product>(); var variants = new List<ProductVariant>();
            for (var i = 1; i <= 3; i++)
            {
                var seller = new Store { Name = $"Shop {i}", Slug = $"shop-{i}", OwnerAccountId = owner.User.AccountId, StoreType = 1, Status = 1 };
                var product = new Product { StoreId = seller.Id, BrandId = brand.Id, CategoryId = category.Id, Name = $"Product {i}", Slug = $"product-{i}", SKU = $"P-{i}", BasePrice = equalPrices ? 100000 : i * 100000, Status = 1 };
                var variant = new ProductVariant { ProductId = product.Id, SKU = $"V-{i}", VariantName = "Default", Price = product.BasePrice, StockQuantity = 10, IsActive = true };
                db.AddRange(seller, product, variant); products.Add(product); variants.Add(variant);
            }
            var now = DateTimeOffset.UtcNow;
            var voucher = new Voucher { Code = "SAVE50", Name = "Save 50k", DiscountType = "FIXED", DiscountValue = 50000, MinOrderAmount = 100000, PerUserLimit = 1, UsageLimit = 1, Status = "ACTIVE", StartsAt = now.AddDays(-1), ExpiresAt = now.AddDays(1) };
            var shipping = new Voucher { Code = "SHIP20", Name = "Shipping", DiscountType = "SHIPPING", DiscountValue = 20000, PerUserLimit = 1, Status = "ACTIVE", StartsAt = now.AddDays(-1), ExpiresAt = now.AddDays(1) };
            db.AddRange(voucher, shipping, new UserVoucher { UserId = buyer.User.Id, VoucherId = voucher.Id, Status = "AVAILABLE", ClaimedAt = now }, new UserVoucher { UserId = buyer.User.Id, VoucherId = shipping.Id, Status = "AVAILABLE", ClaimedAt = now });
            if (sellerTargeted) db.Add(new VoucherTarget { VoucherId = voucher.Id, ProductId = products[0].Id });
            await db.SaveChangesAsync();
            var service = TestOrders.Create(repository, config, new FinanceService(repository, config));
            for (var i = 0; i < 3; i++) await service.AddAsync(buyer.User.Id, new(products[i].Id, variants[i].Id, 1), default);
            var address = await new MemberService(repository).SaveAddressAsync(buyer.User.Id, null, new("Buyer", "0901234567", "79", "001", "Test address", true), default);
            var preview = await service.PreviewAsync(buyer.User.Id, new(VoucherCodes: [voucher.Code, shipping.Code]), default);
            Assert.Equal(3, preview.StoreOrders!.Count);
            Assert.Equal(products.Sum(x => x.BasePrice) + 20000m, preview.TotalAmount);
            Assert.Equal(90000m, preview.ShippingFee);
            var input = new CheckoutRequest(address.Id, ExpectedTotal: preview.TotalAmount, ExpectedCartHash: preview.CartHash, VoucherCodes: preview.AppliedVoucherCodes);
            var result = await service.CheckoutAsync(buyer.User.Id, "split-checkout", input, default);
            Assert.NotNull(result.CheckoutGroupId);
            var children = result.CheckoutOrders!;
            Assert.Equal(3, children.Count);
            Assert.Equal(3, children.Select(x => x.OrderCode).Distinct().Count());
            Assert.Equal(preview.TotalAmount, children.Sum(x => x.TotalAmount));
            Assert.Equal(2, await db.VoucherUsages.CountAsync(x => x.Status == "APPLIED"));
            Assert.Equal(50000m, await db.OrderVoucherApplications.Where(x => x.VoucherId == voucher.Id).SumAsync(x => x.DiscountAmount));
            Assert.Equal(3, await db.Payments.CountAsync());
            Assert.Equal(3, await db.OrderStatusHistories.CountAsync());
            Assert.Empty((await service.CartAsync(buyer.User.Id, default)).Items);
            foreach (var child in children)
            {
                var detail = await service.OrderAsync(buyer.User.Id, child.Id, default);
                var line = Assert.Single(detail.Items);
                var allocation = preview.StoreOrders.Single(x => x.StoreId == line.StoreId);
                Assert.Equal(allocation.TotalAmount, detail.TotalAmount);
                Assert.Equal(allocation.DiscountAmount, detail.DiscountAmount);
                Assert.Equal(detail.TotalAmount, (await db.Payments.SingleAsync(x => x.OrderId == child.Id)).Amount);
                Assert.Equal(1, await db.InventoryReservations.CountAsync(x => x.OrderId == child.Id && x.Status == "HELD"));
                Assert.Equal(1, await db.SellerOrders.CountAsync(x => x.OrderId == child.Id));
                Assert.False(string.IsNullOrWhiteSpace(child.StoreName));
                if (sellerTargeted) Assert.Equal(line.ProductId == products[0].Id ? 50000m : 0m, detail.DiscountAmount);
            }
            var replay = await service.CheckoutAsync(buyer.User.Id, "split-checkout", input, default);
            Assert.Equal(children.Select(x => x.Id), replay.CheckoutOrders!.Select(x => x.Id));
            Assert.Equal(3, (await service.FindRequestAsync(buyer.User.Id, "split-checkout", default))!.CheckoutOrders!.Count);
            Assert.Null(await service.FindRequestAsync(owner.User.Id, "split-checkout", default));
            Assert.Empty((await service.OrdersAsync(owner.User.Id, new(CheckoutGroupId: result.CheckoutGroupId), default)).Items);
            Assert.Equal(3, (await service.OrdersAsync(buyer.User.Id, new(CheckoutGroupId: result.CheckoutGroupId), default)).Items.Count);
            await Assert.ThrowsAsync<BusinessException>(() => service.CheckoutAsync(buyer.User.Id, "split-checkout", input with { Note = "Different" }, default));
            var beneficiary = await db.OrderItems.SingleAsync(x => x.DiscountAmount > 0);
            await service.CancelAsync(buyer.User.Id, beneficiary.OrderId, default);
            Assert.Equal("AVAILABLE", (await db.UserVouchers.SingleAsync(x => x.VoucherId == voucher.Id)).Status);
            Assert.Equal("USED", (await db.UserVouchers.SingleAsync(x => x.VoucherId == shipping.Id)).Status);
            foreach (var child in children.Where(x => x.Id != beneficiary.OrderId))
            {
                var unchanged = await service.OrderAsync(buyer.User.Id, child.Id, default);
                Assert.Equal("PENDING", unchanged.Status);
                Assert.Equal(child.TotalAmount, unchanged.TotalAmount);
                await service.CancelAsync(buyer.User.Id, child.Id, default);
            }
            Assert.All(await db.UserVouchers.ToListAsync(), x => Assert.Equal("AVAILABLE", x.Status));
            Assert.All(await db.VoucherUsages.ToListAsync(), x => Assert.Equal("RELEASED", x.Status));
            Assert.All(await db.ProductVariants.ToListAsync(), x => Assert.Equal(0, x.ReservedQuantity));
            for (var i = 0; i < 3; i++) await service.AddAsync(buyer.User.Id, new(products[i].Id, variants[i].Id, 1), default);
            await Assert.ThrowsAsync<BusinessException>(() => service.CheckoutAsync(buyer.User.Id, "wrong-price", input with { ExpectedTotal = 1 }, default));
            db.ChangeTracker.Clear();
            Assert.Equal(3, await db.Orders.CountAsync());
            Assert.Equal(3, await db.CartItems.CountAsync());
            Assert.All(await db.ProductVariants.ToListAsync(), x => Assert.Equal(0, x.ReservedQuantity));
            if (!sellerTargeted)
            {
                var catalog = new AdminCatalogService(repository);
                var shopA = await catalog.SaveVoucherAsync(owner.User.AccountId, null, new("SHOP-A", "Shop A", "Seller coupon", "FIXED", 10000, 0, 1, now.AddDays(-1), now.AddDays(1), [], StoreId: products[0].StoreId), default);
                var shopB = await catalog.SaveVoucherAsync(owner.User.AccountId, null, new("SHOP-B", "Shop B", "Seller coupon", "FIXED", 20000, 0, 1, now.AddDays(-1), now.AddDays(1), [], StoreId: products[1].StoreId), default);
                await Assert.ThrowsAsync<BusinessException>(() => catalog.SaveVoucherAsync(buyer.User.AccountId, null, new("FOREIGN", "Foreign", "", "FIXED", 1000, 0, 1, now.AddDays(-1), now.AddDays(1), [], StoreId: products[0].StoreId), default));
                db.AddRange(new UserVoucher { UserId = buyer.User.Id, VoucherId = shopA, Status = "AVAILABLE", ClaimedAt = now }, new UserVoucher { UserId = buyer.User.Id, VoucherId = shopB, Status = "AVAILABLE", ClaimedAt = now });
                await db.SaveChangesAsync();
                var stackPreview = await service.PreviewAsync(buyer.User.Id, new(VoucherCodes: ["SHOP-A", "SHOP-B", voucher.Code, shipping.Code]), default);
                Assert.Equal(30000m, stackPreview.ShopDiscountAmount); Assert.Equal(50000m, stackPreview.PlatformDiscountAmount);
                var independent = await service.CheckoutAsync(buyer.User.Id, "independent-cod", input with { ExpectedCartHash = stackPreview.CartHash, ExpectedTotal = stackPreview.TotalAmount, VoucherCodes = stackPreview.AppliedVoucherCodes }, default);
                var independentChildren = independent.CheckoutOrders!;
                var childIds = independentChildren.Select(x => x.Id).ToArray();
                Assert.Equal(3, await db.OrderVoucherApplications.CountAsync(x => childIds.Contains(x.OrderId) && x.VoucherId != shipping.Id));
                db.Add(new AccountRole { AccountId = owner.User.AccountId, Role = "Admin" });
                await db.SaveChangesAsync();
                foreach (var status in new[] { "CONFIRMED", "READY", "SHIPPING" }) await service.TransitionAsync(owner.User.AccountId, childIds[0], new(status), default);
                await service.TransitionAsync(owner.User.AccountId, childIds[0], new("COMPLETED", true), default);
                await service.CancelAsync(buyer.User.Id, childIds[1], default);
                var surviving = await service.OrderAsync(buyer.User.Id, childIds[2], default);
                Assert.Equal("PENDING", surviving.Status);
                Assert.Equal("UNPAID", surviving.PaymentStatus);
                Assert.Equal(independentChildren.Single(x => x.Id == childIds[2]).TotalAmount, surviving.TotalAmount);
                var refunds = new RefundSettlementService(repository, config);
                var refund = await refunds.RequestRefundAsync(buyer.User.Id, childIds[0], "split-refund", new("Return this store's order"), default);
                Assert.Equal(independentChildren.Single(x => x.Id == childIds[0]).TotalAmount, (await db.Refunds.SingleAsync(x => x.Id == refund)).Amount);
                await refunds.SetRefundStatusAsync(owner.User.AccountId, refund, new("APPROVED"), default);
                await refunds.SetRefundStatusAsync(owner.User.AccountId, refund, new("COMPLETED", "SPLIT-REFUND", true), default);
                Assert.Equal("REFUNDED", (await service.OrderAsync(buyer.User.Id, childIds[0], default)).Status);
                Assert.Equal("PENDING", (await service.OrderAsync(buyer.User.Id, childIds[2], default)).Status);
                var discountedOrderId = await db.OrderVoucherApplications.Where(x => childIds.Contains(x.OrderId) && x.VoucherId == voucher.Id).Select(x => x.OrderId).SingleAsync();
                Assert.Equal(discountedOrderId == childIds[1] ? "AVAILABLE" : "USED", (await db.UserVouchers.SingleAsync(x => x.VoucherId == voucher.Id)).Status);
            }
        }
        finally
        {
            NpgsqlConnection.ClearAllPools();
            await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{databaseName}\" WITH (FORCE)", admin);
            await drop.ExecuteNonQueryAsync();
        }
    }
}
