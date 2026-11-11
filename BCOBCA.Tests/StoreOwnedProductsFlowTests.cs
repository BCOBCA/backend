using System.Text.Json;
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

public sealed class StoreOwnedProductsFlowTests
{
    [Fact]
    public async Task SharedListingsSplitWithoutDuplicatingInventoryAndRemainStoreScopedThroughCheckout()
    {
        EnvironmentBootstrap.Load();
        var supplied = Environment.GetEnvironmentVariable("ConnectionStrings__Default") ?? throw new InvalidOperationException("Configure isolated PostgreSQL.");
        var target = new NpgsqlConnectionStringBuilder(supplied) { Database = "bcobca_ownership_test_" + Guid.NewGuid().ToString("N") };
        await using var adminConnection = new NpgsqlConnection(supplied);
        await adminConnection.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE DATABASE \"{target.Database}\" TEMPLATE template0 LOCALE_PROVIDER icu ICU_LOCALE 'vi-VN'", adminConnection)) await create.ExecuteNonQueryAsync();
        try
        {
            await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(target.ConnectionString).Options);
            await db.GetService<IMigrator>().MigrateAsync("20261109032814_AddProductSpecifications");
            var ownerA = new Account { Email = "store-a@example.test", Status = 1 };
            var ownerB = new Account { Email = "store-b@example.test", Status = 1 };
            var buyerAccount = new Account { Email = "store-buyer@example.test", Status = 1 };
            var buyer = new User { AccountId = buyerAccount.Id, ReferralCode = "STORE-BUYER", DateOfBirth = new DateOnly(2000, 1, 1) };
            var storeA = new Store { OwnerAccountId = ownerA.Id, StoreType = 1, Status = 1, Name = "Store A", Slug = "a-store" };
            var storeB = new Store { OwnerAccountId = ownerB.Id, StoreType = 1, Status = 1, Name = "Store B", Slug = "b-store" };
            var brand = new Brand { OwnerAccountId = ownerA.Id, Status = 1, Name = "Shared manufacturer", Slug = "shared-manufacturer" };
            var category = new Category { Name = "Phones", Slug = "owner-phones", IsActive = true };
            var cart = new Cart { UserId = buyer.Id };
            db.AddRange(ownerA, ownerB, buyerAccount, buyer, brand, category, cart);
            await db.SaveChangesAsync();
            await InsertLegacy("Stores", storeA); await InsertLegacy("Stores", storeB);
            db.Stores.AttachRange(storeA, storeB);
            var original = new Product { BrandId = brand.Id, CategoryId = category.Id, Name = "Same model", Slug = "same-model", SKU = "SAME-MODEL", BasePrice = 1000, Status = 1, Specifications = "{\"chip\":\"Example chip\"}", AffiliateBudget = 100 };
            var originalVariant = new ProductVariant { ProductId = original.Id, SKU = "SAME-MODEL-64", VariantName = "64 GB", Attributes = "{\"storage\":\"64 GB\"}", Price = 1000, StockQuantity = 10, ReservedQuantity = 2, IsActive = true };
            var orphan = new Product { BrandId = brand.Id, CategoryId = category.Id, Name = "Unassigned historical record", Slug = "unassigned", SKU = "UNASSIGNED", Status = 1 };
            async Task InsertLegacy(string table, object value)
            {
                var payload = JsonSerializer.SerializeToNode(value)!;
                if (value is Product p) payload["Specifications"] = System.Text.Json.Nodes.JsonNode.Parse(p.Specifications);
                if (value is ProductVariant v) payload["Attributes"] = System.Text.Json.Nodes.JsonNode.Parse(v.Attributes);
                var parameter = new NpgsqlParameter("payload", NpgsqlTypes.NpgsqlDbType.Jsonb) { Value = payload.ToJsonString() };
                var sql = table switch
                {
                    "Stores" => "INSERT INTO \"Stores\" SELECT (jsonb_populate_record(NULL::\"Stores\", @payload)).*",
                    "Products" => "INSERT INTO \"Products\" SELECT (jsonb_populate_record(NULL::\"Products\", @payload)).*",
                    "ProductVariants" => "INSERT INTO \"ProductVariants\" SELECT (jsonb_populate_record(NULL::\"ProductVariants\", @payload)).*",
                    "ProductImages" => "INSERT INTO \"ProductImages\" SELECT (jsonb_populate_record(NULL::\"ProductImages\", @payload)).*",
                    "Orders" => "INSERT INTO \"Orders\" SELECT (jsonb_populate_record(NULL::\"Orders\", @payload)).*",
                    "OrderItems" => "INSERT INTO \"OrderItems\" SELECT (jsonb_populate_record(NULL::\"OrderItems\", @payload)).*",
                    "SellerOrders" => "INSERT INTO \"SellerOrders\" SELECT (jsonb_populate_record(NULL::\"SellerOrders\", @payload)).*",
                    "SellerSettlements" => "INSERT INTO \"SellerSettlements\" SELECT (jsonb_populate_record(NULL::\"SellerSettlements\", @payload)).*",
                    _ => throw new InvalidOperationException("Unexpected fixture table")
                };
                await db.Database.ExecuteSqlRawAsync(sql, parameter);
            }
            await InsertLegacy("Products", original);
            await InsertLegacy("Products", orphan);
            await InsertLegacy("ProductVariants", originalVariant);
            await InsertLegacy("ProductImages", new ProductImage { ProductId = original.Id, ImageUrl = "https://example.test/model.jpg", IsPrimary = true });
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"StoreProducts\" (\"StoreId\",\"ProductId\") VALUES ({storeA.Id},{original.Id}),({storeB.Id},{original.Id})");
            var historicalOrder = new Order { UserId = buyer.Id, OrderCode = "BEFORE-SPLIT", IdempotencyKey = "before-split", Status = "COMPLETED", PaymentStatus = "PAID", Subtotal = 1000, TotalAmount = 1000 };
            var historicalItem = new OrderItem { OrderId = historicalOrder.Id, ProductId = original.Id, VariantId = originalVariant.Id, BrandId = brand.Id, ProductName = original.Name, SKU = originalVariant.SKU, Quantity = 1, UnitPrice = 1000, TotalPrice = 1000 };
            var historicalSeller = new SellerOrder { OrderId = historicalOrder.Id, BrandId = brand.Id, Status = "COMPLETED", Subtotal = 1000, TotalAmount = 1000 };
            await InsertLegacy("Orders", historicalOrder);
            await InsertLegacy("OrderItems", historicalItem);
            await InsertLegacy("SellerOrders", historicalSeller);
            await InsertLegacy("SellerSettlements", new SellerSettlement { BrandId = brand.Id, SellerOrderId = historicalSeller.Id, GrossAmount = 1000, NetAmount = 1000, Status = "PENDING" });
            db.AddRange(new ProductFavorite { ProductId = original.Id, UserId = buyer.Id }, new ProductReview { ProductId = original.Id, OrderItemId = historicalItem.Id, UserId = buyer.Id, Rating = 5, Content = "Original store purchase", Status = 1 });
            var oldCartItem = new CartItem { CartId = cart.Id, ProductId = original.Id, VariantId = originalVariant.Id, Quantity = 1, IsSelected = false };
            db.Add(oldCartItem);
            await db.SaveChangesAsync();
            await db.Database.MigrateAsync();
            Assert.False(db.Database.HasPendingModelChanges());
            var listings = await db.Products.OrderBy(p => p.StoreId).ToListAsync();
            Assert.Equal(2, listings.Count);
            Assert.True((await db.Products.IgnoreQueryFilters().SingleAsync(p => p.Id == orphan.Id)).IsDeleted);
            var retained = listings.Single(p => p.Id == original.Id);
            var clone = listings.Single(p => p.Id != original.Id);
            Assert.Equal(storeA.Id, retained.StoreId);
            Assert.Equal(storeB.Id, clone.StoreId);
            Assert.Equal(original.SKU, retained.SKU);
            Assert.NotEqual(retained.SKU, clone.SKU);
            Assert.Equal(brand.Id, clone.BrandId);
            Assert.Equal(retained.Specifications, clone.Specifications);
            Assert.Equal(0m, clone.AffiliateBudget);
            var clonedVariant = await db.ProductVariants.SingleAsync(v => v.ProductId == clone.Id);
            Assert.Equal(0, clonedVariant.StockQuantity);
            Assert.Equal(0, clonedVariant.ReservedQuantity);
            Assert.NotEqual(originalVariant.Id, clonedVariant.Id);
            Assert.NotEqual(originalVariant.SKU, clonedVariant.SKU);
            Assert.Equal(10, await db.ProductVariants.SumAsync(v => v.StockQuantity));
            Assert.Equal(2, await db.ProductVariants.SumAsync(v => v.ReservedQuantity));
            Assert.Equal(original.Id, (await db.CartItems.SingleAsync()).ProductId);
            Assert.Equal(2, await db.ProductImages.CountAsync());
            Assert.Equal(original.Id, (await db.OrderItems.SingleAsync()).ProductId);
            Assert.Null((await db.OrderItems.SingleAsync()).StoreId);
            Assert.Null((await db.Set<SellerOrder>().SingleAsync()).StoreId);
            Assert.Null((await db.Set<SellerSettlement>().SingleAsync()).StoreId);
            Assert.Equal(original.Id, (await db.Set<ProductReview>().SingleAsync()).ProductId);
            Assert.Equal(original.Id, (await db.Set<ProductFavorite>().SingleAsync()).ProductId);
            var repository = new CommerceStore(db);
            var catalog = new CatalogService(repository);
            Assert.Equal(storeA.Id, Assert.Single((await catalog.ProductAsync(retained.Id, default)).Sellers!).Id);
            Assert.Equal(storeB.Id, Assert.Single((await catalog.ProductAsync(clone.Id, default)).Sellers!).Id);
            Assert.Equal(clone.Id, Assert.Single((await catalog.ProductsAsync(new CatalogQuery(StoreId: storeB.Id), default)).Items).Id);
            var admin = new AdminCatalogService(repository);
            var update = new ProductInput(brand.Id, category.Id, clone.Name, clone.Slug, clone.SKU, 700,
                [new VariantInput(clonedVariant.Id, clonedVariant.SKU, clonedVariant.VariantName, 700, 3, Attributes: clonedVariant.Attributes)], [], StoreId: storeB.Id);
            Assert.Equal(403, (await Assert.ThrowsAsync<BusinessException>(() => admin.SaveProductAsync(ownerA.Id, clone.Id, update, default))).StatusCode);
            await admin.SaveProductAsync(ownerB.Id, clone.Id, update, default);
            Assert.Equal(409, (await Assert.ThrowsAsync<BusinessException>(() => admin.SaveProductAsync(ownerB.Id, clone.Id, update with { StoreId = storeA.Id }, default))).StatusCode);
            await Assert.ThrowsAsync<BusinessException>(() => admin.ArchiveProductAsync(ownerA.Id, clone.Id, default));
            await Assert.ThrowsAsync<BusinessException>(() => admin.SaveProductAsync(ownerB.Id, null, update with { StoreId = null }, default));
            Assert.Equal(1000, (await db.ProductVariants.AsNoTracking().SingleAsync(v => v.Id == originalVariant.Id)).Price);
            var config = new ConfigurationBuilder().AddEnvironmentVariables().AddInMemoryCollection(new Dictionary<string,string?> { ["Settlement:PlatformFeeRate"] = "0.1", ["Settlement:HoldDays"] = "0", ["Checkout:ShippingFee"] = "0" }).Build();
            var finance = new FinanceService(repository, config);
            var orders = TestOrders.Create(repository, config, finance);
            await Assert.ThrowsAsync<BusinessException>(() => orders.AddAsync(buyer.Id, new CartItemRequest(retained.Id, clonedVariant.Id, 1), default));
            await orders.AddAsync(buyer.Id, new CartItemRequest(retained.Id, originalVariant.Id, 1), default);
            await orders.AddAsync(buyer.Id, new CartItemRequest(clone.Id, clonedVariant.Id, 1), default);
            await orders.UpdateAsync(buyer.Id, oldCartItem.Id, new UpdateCartRequest(1, true), default);
            var cartView = await orders.CartAsync(buyer.Id, default);
            Assert.Equal(2, cartView.Items.Select(i => i.StoreId).Distinct().Count());
            var address = await new MemberService(repository).SaveAddressAsync(buyer.Id, null, new AddressRequest("Buyer", "0901234567", "79", "001", "Ownership test address", true), default);
            var order = await orders.CheckoutAsync(buyer.Id, "store-isolation", new CheckoutRequest(address.Id), default);
            var checkoutIds = order.CheckoutOrders!.Select(o => o.Id).ToArray();
            Assert.Equal(2, checkoutIds.Length);
            Assert.Single(order.Items.Select(i => i.StoreId).Distinct());
            Assert.Equal(2, await db.Set<SellerOrder>().CountAsync(s => checkoutIds.Contains(s.OrderId)));
            foreach (var persistedOrder in await db.Orders.Where(o => checkoutIds.Contains(o.Id)).ToListAsync())
            {
                persistedOrder.Status = "COMPLETED"; persistedOrder.PaymentStatus = "PAID";
            }
            // Finance requires its caller's transaction and a membership; no unrelated ledger rules are mocked.
            db.Add(new UserMembership { UserId = buyer.Id, MemberCode = "STORE-BUYER-MEMBER", TierId = await db.Set<MembershipTier>().OrderBy(t => t.MinPoints).Select(t => t.Id).FirstAsync() });
            await db.SaveChangesAsync();
            await using (var tx = await db.Database.BeginTransactionAsync())
            {
                foreach (var childId in checkoutIds) await finance.AccrueOrderAsync(childId, default);
                await tx.CommitAsync();
            }
            var refunds = new RefundSettlementService(repository, config);
            var aSettlements = (await refunds.SettlementsAsync(ownerA.Id, false, new CatalogQuery(), default)).Items;
            var bSettlements = (await refunds.SettlementsAsync(ownerB.Id, false, new CatalogQuery(), default)).Items;
            Assert.Equal(storeA.Id, Assert.Single(aSettlements, s => checkoutIds.Contains(s.OrderId)).StoreId);
            Assert.Equal(storeB.Id, Assert.Single(bSettlements).StoreId);
            var settlementB = await db.Set<SellerSettlement>().SingleAsync(s => s.StoreId == storeB.Id);
            settlementB.Status = "PAID"; settlementB.PaidAmount = settlementB.NetAmount;
            var sellerBOrderId = await db.SellerOrders.Where(s => s.Id == settlementB.SellerOrderId).Select(s => s.OrderId).SingleAsync();
            var completedRefund = new Refund { PaymentId = await db.Set<Payment>().Where(p => p.OrderId == sellerBOrderId).Select(p => p.Id).SingleAsync(), Amount = settlementB.NetAmount, Status = "COMPLETED", Reason = "Store B recovery fixture" };
            var debtB = new SellerSettlementAdjustment { SellerSettlementId = settlementB.Id, RefundId = completedRefund.Id, Amount = settlementB.NetAmount, RemainingAmount = settlementB.NetAmount, Status = "OPEN" };
            db.AddRange(completedRefund, debtB, new AccountRole { AccountId = ownerA.Id, Role = "Admin" });
            await db.SaveChangesAsync();
            var settlementA = Assert.Single(aSettlements, s => checkoutIds.Contains(s.OrderId));
            await refunds.PaySettlementAsync(ownerA.Id, settlementA.Id, new SettlementPaymentRequest("STORE-A-PAYOUT"), default);
            Assert.Equal(settlementA.NetAmount, (await db.Set<SellerSettlement>().SingleAsync(s => s.Id == settlementA.Id)).PaidAmount);
            Assert.Equal(settlementB.NetAmount, (await db.Set<SellerSettlementAdjustment>().SingleAsync()).RemainingAmount);
            await Assert.ThrowsAsync<PostgresException>(() => db.GetService<IMigrator>().MigrateAsync("20261109032814_AddProductSpecifications"));
            Assert.Equal(2, await db.Set<SellerOrder>().CountAsync(s => checkoutIds.Contains(s.OrderId) && s.StoreId != null));
            storeB.Status = 0;
            await db.SaveChangesAsync();
            await Assert.ThrowsAsync<BusinessException>(() => catalog.ProductAsync(clone.Id, default));
            await Assert.ThrowsAsync<BusinessException>(() => orders.AddAsync(buyer.Id, new CartItemRequest(clone.Id, clonedVariant.Id, 1), default));
        }
        finally
        {
            NpgsqlConnection.ClearAllPools();
            await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{target.Database}\" WITH (FORCE)", adminConnection);
            await drop.ExecuteNonQueryAsync();
        }
    }
}
