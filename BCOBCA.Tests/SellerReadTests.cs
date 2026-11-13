using BCOBCA.BLL.Common;
using BCOBCA.BLL.DTOs;
using BCOBCA.BLL.Services;
using BCOBCA.DAL.Context;
using BCOBCA.DAL.Entities;
using BCOBCA.DAL.Repositories;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace BCOBCA.Tests;

public sealed class SellerReadTests
{
    [Fact]
    public async Task EveryReadIsScopedToTheOwnerAndExcludesMixedStoreOrders()
    {
        EnvironmentBootstrap.Load();
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__Default") ?? throw new InvalidOperationException("Configure test PostgreSQL connection.");
        var database = "bcobca_test_seller_" + Guid.NewGuid().ToString("N");
        await using var admin = new NpgsqlConnection(connection);
        await admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE DATABASE \"{database}\"", admin)) await create.ExecuteNonQueryAsync();
        try
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(new NpgsqlConnectionStringBuilder(connection) { Database = database }.ConnectionString).Options;
            await using var db = new ApplicationDbContext(options);
            await db.Database.MigrateAsync();
            var ownerA = new Account { Email = "seller-a@example.test", PasswordHash = "test", Status = 1 };
            var ownerB = new Account { Email = "seller-b@example.test", PasswordHash = "test", Status = 1 };
            var buyerAccount = new Account { Email = "buyer@example.test", PasswordHash = "test", Status = 1 };
            var buyer = new User { AccountId = buyerAccount.Id, FullName = "Buyer", ReferralCode = "SELLER-READ" };
            var shopA = new Store { OwnerAccountId = ownerA.Id, StoreType = 1, Status = 1, Name = "Shop A", Slug = "shop-a" };
            var shopB = new Store { OwnerAccountId = ownerB.Id, StoreType = 1, Status = 1, Name = "Shop B", Slug = "shop-b" };
            var brand = new Brand { OwnerAccountId = ownerA.Id, Name = "Brand", Slug = "brand", Status = 1 };
            var category = new Category { Name = "Tea", Slug = "tea", IsActive = true };
            var productA = new Product { StoreId = shopA.Id, BrandId = brand.Id, CategoryId = category.Id, Name = "Hidden tea", Slug = "hidden-tea", SKU = "A", BasePrice = 100000, Status = 0 };
            var productB = new Product { StoreId = shopB.Id, BrandId = brand.Id, CategoryId = category.Id, Name = "Other store tea", Slug = "other-tea", SKU = "B", BasePrice = 90000, Status = 1 };
            var variant = new ProductVariant { ProductId = productA.Id, SKU = "A-1", VariantName = "Tea", Price = 100000, StockQuantity = 10, ReservedQuantity = 3, IsActive = true };
            var completed = new Order { UserId = buyer.Id, OrderCode = "SELLER-DONE", Status = "COMPLETED", PaymentStatus = "PAID", Subtotal = 200000, DiscountAmount = 20000, ShippingFee = 30000, ShippingDiscountAmount = 10000, TotalAmount = 200000, IdempotencyKey = "seller-done", CreatedAt = DateTimeOffset.UtcNow };
            var mixed = new Order { UserId = buyer.Id, OrderCode = "SELLER-MIXED", Status = "COMPLETED", PaymentStatus = "PAID", TotalAmount = 999999, IdempotencyKey = "seller-mixed", CreatedAt = DateTimeOffset.UtcNow };
            db.AddRange(ownerA, ownerB, buyerAccount, buyer, shopA, shopB, brand, category, productA, productB, variant, completed, mixed);
            db.AddRange(
                new OrderItem { OrderId = completed.Id, ProductId = productA.Id, BrandId = brand.Id, StoreId = shopA.Id, ProductName = "Historical tea", SKU = "A", Quantity = 2, UnitPrice = 100000, DiscountAmount = 20000, TotalPrice = 180000 },
                new OrderItem { OrderId = mixed.Id, ProductId = productA.Id, BrandId = brand.Id, StoreId = shopA.Id, ProductName = "A", SKU = "A", Quantity = 1, UnitPrice = 100000, TotalPrice = 100000 },
                new OrderItem { OrderId = mixed.Id, ProductId = productB.Id, BrandId = brand.Id, StoreId = shopB.Id, ProductName = "B", SKU = "B", Quantity = 1, UnitPrice = 90000, TotalPrice = 90000 });
            await db.SaveChangesAsync();
            var service = new SellerService(new CommerceStore(db));
            Assert.Equal(shopA.Id, Assert.Single(await service.StoresAsync(ownerA.Id, default)).Id);
            var products = await service.ProductsAsync(ownerA.Id, shopA.Id, new CatalogQuery(), default);
            var product = Assert.Single(products.Items);
            Assert.Equal(productA.Id, product.Id);
            Assert.False(product.Active);
            Assert.Equal(7, product.Stock);
            var orders = await service.OrdersAsync(ownerA.Id, shopA.Id, new CatalogQuery(PageSize: 1), default);
            Assert.Equal(1, orders.TotalItems);
            var detail = await service.OrderAsync(ownerA.Id, shopA.Id, completed.Id, default);
            Assert.Equal(200000m, detail.Total);
            Assert.Equal(20000m, detail.Discount);
            Assert.Equal("Historical tea", Assert.Single(detail.Items).Name);
            var summary = await service.SummaryAsync(ownerA.Id, shopA.Id, default);
            Assert.Equal(180000m, summary.Revenue);
            Assert.Equal(1, summary.TotalOrders);
            var forbidden = await Assert.ThrowsAsync<BusinessException>(() => service.ProductsAsync(ownerB.Id, shopA.Id, new CatalogQuery(), default));
            Assert.Equal(404, forbidden.StatusCode);
            await Assert.ThrowsAsync<BusinessException>(() => service.SummaryAsync(ownerB.Id, shopA.Id, default));
            await Assert.ThrowsAsync<BusinessException>(() => service.OrderAsync(ownerB.Id, shopA.Id, completed.Id, default));
            await Assert.ThrowsAsync<BusinessException>(() => service.OrderAsync(ownerA.Id, shopA.Id, mixed.Id, default));
            shopA.DeletedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();
            Assert.Empty(await service.StoresAsync(ownerA.Id, default));
            await Assert.ThrowsAsync<BusinessException>(() => service.OrdersAsync(ownerA.Id, shopA.Id, new CatalogQuery(), default));
        }
        finally
        {
            NpgsqlConnection.ClearAllPools();
            await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{database}\" WITH (FORCE)", admin);
            await drop.ExecuteNonQueryAsync();
        }
    }
}
