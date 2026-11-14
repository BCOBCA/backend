using BCOBCA.BLL.Common;
using BCOBCA.BLL.DTOs;
using BCOBCA.BLL.Services;
using BCOBCA.DAL.Context;
using BCOBCA.DAL.Entities;
using BCOBCA.DAL.Repositories;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Xunit;

namespace BCOBCA.Tests;

public sealed class SellerProductCreateTests
{
    [Fact]
    public async Task OwnerCreatesStoreScopedProductWithImagesStockAndSafeRetry()
    {
        await SellerVerificationTests.WithDatabase(async options =>
        {
            await using var db = new ApplicationDbContext(options);
            var owner = new Account { Email = "create-owner@example.test", Role = "Seller", Status = 1 };
            var other = new Account { Email = "create-other@example.test", Role = "Seller", Status = 1 };
            var shop = new Store { OwnerAccountId = owner.Id, Name = "Shop", Slug = "product-create", StoreType = 1, Status = 1 };
            var foreign = new Store { OwnerAccountId = other.Id, Name = "Other", Slug = "product-other", StoreType = 1, Status = 1 };
            var brand = new Brand { OwnerAccountId = other.Id, Name = "Brand", Slug = "create-brand", Status = 1 };
            var category = new Category { Name = "Danh mục", Slug = "create-category", IsActive = true };
            category.AttributeDefinitions = """[{"key":"origin","label":"Xuất xứ","group":"Chung","type":"text","required":true,"scope":"product"},{"key":"size","label":"Kích thước","group":"Phân loại","type":"select","required":true,"scope":"variant","options":["S","M"]}]""";
            db.AddRange(owner, other, shop, foreign, brand, category); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
            var store = new CommerceStore(db);
            var service = new SellerService(store, catalog: new AdminCatalogService(store));
            var choices = await service.ProductOptionsAsync(owner.Id, shop.Id, default);
            Assert.Contains(choices.Brands, b => b.Id == brand.Id); Assert.Contains(choices.Categories, c => c.Id == category.Id);
            var input = new SellerCreateProductInput(Guid.NewGuid(), brand.Id, category.Id, " Trà sen Tây Hồ ", 150000, 12,
                ["https://images.example.test/tea.png"], 300, 20, 15, 10, "Mô tả thật", "Trà sen", 120000, false,
                JsonSerializer.Deserialize<Dictionary<string, JsonElement>>("""{"origin":"Việt Nam"}"""),
                JsonSerializer.Deserialize<Dictionary<string, JsonElement>>("""{"size":"M"}"""));
            await Assert.ThrowsAsync<BusinessException>(() => service.CreateProductAsync(other.Id, shop.Id, input, default));
            await Assert.ThrowsAsync<BusinessException>(() => service.ProductOptionsAsync(owner.Id, foreign.Id, default));
            var created = await service.CreateProductAsync(owner.Id, shop.Id, input, default);
            Assert.Equal("Trà sen Tây Hồ", created.Name); Assert.Equal(120000, created.Price); Assert.Equal(12, created.Stock);
            Assert.False(created.Active); Assert.Equal(input.ImageUrls[0], created.ImageUrl);
            var product = await db.Products.AsNoTracking().SingleAsync();
            Assert.Equal(shop.Id, product.StoreId); Assert.Equal(brand.Id, product.BrandId); Assert.Equal(300, product.ShippingWeightGrams);
            var variant = await db.ProductVariants.AsNoTracking().SingleAsync();
            Assert.Equal(product.Id, variant.ProductId); Assert.Equal(0, variant.ReservedQuantity); Assert.Equal(12, variant.StockQuantity);
            Assert.Contains("origin", product.Specifications); Assert.Contains("size", variant.Attributes);
            Assert.Equal(12, (await db.Set<InventoryMovement>().SingleAsync()).Quantity);
            Assert.Equal(product.Id, (await db.Set<AuditLog>().SingleAsync(a => a.EntityType == "Product")).EntityId);
            var retry = await service.CreateProductAsync(owner.Id, shop.Id, input, default);
            Assert.Equal(created.Id, retry.Id); Assert.Equal(1, await db.Products.CountAsync()); Assert.Equal(1, await db.Set<InventoryMovement>().CountAsync());
            var listed = await service.ProductsAsync(owner.Id, shop.Id, new(), default);
            Assert.Equal(created.Id, Assert.Single(listed.Items).Id);
            Assert.Empty((await service.ProductsAsync(other.Id, foreign.Id, new(), default)).Items);
            await Assert.ThrowsAsync<BusinessException>(() => service.CreateProductAsync(owner.Id, shop.Id, input with { RequestId = Guid.NewGuid(), Specifications = null }, default));
            Assert.Equal(1, await db.Products.CountAsync());
        });
    }

    [Fact]
    public async Task RejectsInvalidPricesImagesShippingCatalogAndInactiveOwnersWithoutWrites()
    {
        await SellerVerificationTests.WithDatabase(async options =>
        {
            await using var db = new ApplicationDbContext(options);
            var owner = new Account { Email = "invalid-create@example.test", Role = "Seller", Status = 1 };
            var shop = new Store { OwnerAccountId = owner.Id, Name = "Shop", Slug = "invalid-create", StoreType = 1, Status = 1 };
            var brand = new Brand { OwnerAccountId = owner.Id, Name = "Brand", Slug = "invalid-brand", Status = 1 };
            var parent = new Category { Name = "Hidden", Slug = "hidden-parent", IsActive = false };
            var category = new Category { Name = "Category", Slug = "invalid-category", IsActive = true };
            var hiddenChild = new Category { Name = "Child", Slug = "hidden-child", ParentId = parent.Id, IsActive = true };
            db.AddRange(owner, shop, brand, parent, category, hiddenChild); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
            var store = new CommerceStore(db); var service = new SellerService(store, catalog: new AdminCatalogService(store));
            var input = new SellerCreateProductInput(Guid.NewGuid(), brand.Id, category.Id, "Product", 100, 0, ["https://images.example.test/item.png"], 100, 10, 10, 10);
            foreach (var invalid in new[] { input with { Name = " " }, input with { Price = 0 }, input with { Stock = -1 },
                input with { SalePrice = 101 }, input with { ImageUrls = [] }, input with { ImageUrls = ["javascript:bad"] },
                input with { ShippingWeightGrams = 0 }, input with { ShippingLengthCm = 201 }, input with { CategoryId = hiddenChild.Id } })
            {
                var failure = await Record.ExceptionAsync(() => service.CreateProductAsync(owner.Id, shop.Id, invalid, default));
                Assert.True(failure is ValidationException or BusinessException, failure?.GetType().Name ?? "Validation did not reject input");
            }
            var persistedBrand = await db.Brands.SingleAsync(); persistedBrand.Status = 0; await db.SaveChangesAsync(); db.ChangeTracker.Clear();
            await Assert.ThrowsAsync<BusinessException>(() => service.CreateProductAsync(owner.Id, shop.Id, input, default));
            var account = await db.Accounts.SingleAsync(); account.Status = 0; await db.SaveChangesAsync(); db.ChangeTracker.Clear();
            await Assert.ThrowsAsync<BusinessException>(() => service.CreateProductAsync(owner.Id, shop.Id, input, default));
            Assert.Empty(await db.Products.ToListAsync()); Assert.Empty(await db.Set<InventoryMovement>().ToListAsync());
        });
    }
}
