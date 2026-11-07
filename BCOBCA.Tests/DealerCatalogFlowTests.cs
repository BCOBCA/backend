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

public sealed class DealerCatalogFlowTests
{
    [Fact]
    public async Task StorefrontScopesProductsAndUsesActiveCategoryTreesAndCartPrices()
    {
        EnvironmentBootstrap.Load();
        var supplied = Environment.GetEnvironmentVariable("ConnectionStrings__Default") ?? throw new InvalidOperationException("Configure local PostgreSQL.");
        var scratch = new NpgsqlConnectionStringBuilder(supplied) { Database = "bcobca_catalog_test_" + Guid.NewGuid().ToString("N") };
        await using var admin = new NpgsqlConnection(supplied);
        await admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE DATABASE \"{scratch.Database}\" TEMPLATE template0 LOCALE_PROVIDER icu ICU_LOCALE 'vi-VN'", admin)) await create.ExecuteNonQueryAsync();
        try
        {
            await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(scratch.ConnectionString).Options);
            await db.Database.MigrateAsync();
            var now = DateTimeOffset.UtcNow;
            var account = new Account { Email = "catalog@example.test", Phone = "0900000007", PasswordHash = "test", Role = "Customer", Status = 1, CreatedAt = now, UpdatedAt = now };
            var dealer = new Store { OwnerAccountId = account.Id, StoreType = 1, Name = "Catalog QA", Slug = "catalog-qa", Status = 1 };
            var other = new Store { OwnerAccountId = account.Id, StoreType = 1, Name = "Other QA", Slug = "other-qa", Status = 1 };
            var brand = new Brand { OwnerAccountId = account.Id, Name = "Brand QA", Slug = "brand-qa", Status = 1, CreatedAt = now, UpdatedAt = now };
            var root = new Category { Name = "Thiết bị QA", Slug = "device-qa", IsActive = true };
            var phones = new Category { Name = "Điện thoại QA", Slug = "phone-qa", ParentId = root.Id, IsActive = true };
            var tablets = new Category { Name = "Máy tính bảng QA", Slug = "tablet-qa", ParentId = root.Id, IsActive = true };
            var privateRoot = new Category { Name = "Ẩn QA", Slug = "private-qa", IsActive = false };
            var privateChild = new Category { Name = "Con ẩn QA", Slug = "private-child-qa", ParentId = privateRoot.Id, IsActive = true };
            db.AddRange(account, dealer, other, brand, root, phones, tablets, privateRoot, privateChild);
            var phone = new Product { BrandId = brand.Id, CategoryId = phones.Id, Name = "Phone", Slug = "phone", SKU = "PHONE", BasePrice = 1000, Status = 1, CreatedAt = now, UpdatedAt = now };
            var tablet = new Product { BrandId = brand.Id, CategoryId = tablets.Id, Name = "Tablet", Slug = "tablet", SKU = "TABLET", BasePrice = 2000, Status = 1, CreatedAt = now, UpdatedAt = now };
            var noVariant = new Product { BrandId = brand.Id, CategoryId = phones.Id, Name = "No variant", Slug = "no-variant", SKU = "NO-VARIANT", BasePrice = 400, SalePrice = 350, Status = 1, CreatedAt = now, UpdatedAt = now };
            var otherProduct = new Product { BrandId = brand.Id, CategoryId = phones.Id, Name = "Other product", Slug = "other-product", SKU = "OTHER", BasePrice = 100, Status = 1, CreatedAt = now, UpdatedAt = now };
            var privateProduct = new Product { BrandId = brand.Id, CategoryId = privateChild.Id, Name = "Private category", Slug = "private-product", SKU = "PRIVATE", BasePrice = 50, Status = 1, CreatedAt = now, UpdatedAt = now };
            db.AddRange(phone, tablet, noVariant, otherProduct, privateProduct);
            var phoneVariant = new ProductVariant { ProductId = phone.Id, SKU = "PHONE-V1", VariantName = "Default", Price = 1000, SalePrice = 800, StockQuantity = 5, IsActive = true };
            var tabletVariant = new ProductVariant { ProductId = tablet.Id, SKU = "TABLET-V2", VariantName = "Available", Price = 3000, SalePrice = 2500, StockQuantity = 1, IsActive = true };
            db.AddRange(phoneVariant, tabletVariant, new ProductVariant { ProductId = tablet.Id, SKU = "TABLET-V1", VariantName = "Reserved", Price = 500, StockQuantity = 3, ReservedQuantity = 3, IsActive = true });
            db.AddRange(new StoreCategory { StoreId = dealer.Id, CategoryId = root.Id }, new StoreProduct { StoreId = other.Id, ProductId = otherProduct.Id });
            foreach (var product in new[] { phone, tablet, noVariant, privateProduct }) db.Add(new StoreProduct { StoreId = dealer.Id, ProductId = product.Id });
            await db.SaveChangesAsync();
            var service = new DealerCatalogService(new CommerceStore(db));
            var page = await service.ProductsAsync(dealer.Id, new DealerProductQuery(Sort: "price-asc"), default);
            Assert.Equal(3, page.TotalItems);
            Assert.Equal(new[] { noVariant.Id, phone.Id, tablet.Id }, page.Items.Select(x => x.Id));
            Assert.Equal(tabletVariant.Id, page.Items.Last().VariantId);
            Assert.Equal(1, page.Items.Last().AvailableStock);
            Assert.All(page.Items, x => { Assert.Null(x.Rating); Assert.Equal(0, x.ReviewCount); Assert.Equal(0, x.SoldCount); });
            Assert.Equal(phone.Id, Assert.Single((await service.ProductsAsync(dealer.Id, new DealerProductQuery(MinPrice: 800, MaxPrice: 800), default)).Items).Id);
            Assert.Equal(3, (await service.ProductsAsync(dealer.Id, new DealerProductQuery(CategoryId: root.Id), default)).TotalItems);
            Assert.Equal(4, (await new CatalogService(new CommerceStore(db)).ProductsAsync(new CatalogQuery(CategoryId: root.Id), default)).TotalItems);
            Assert.Equal(2, (await service.ProductsAsync(dealer.Id, new DealerProductQuery(CategoryId: phones.Id), default)).TotalItems);
            Assert.Empty((await service.ProductsAsync(dealer.Id, new DealerProductQuery(CategoryId: Guid.NewGuid()), default)).Items);
            Assert.Equal(2, (await service.ProductsAsync(dealer.Id, new DealerProductQuery(PageSize: 2), default)).TotalPages);
            var categories = await service.CategoriesAsync(dealer.Id, default);
            Assert.Equal(2, categories.Count);
            Assert.Equal(2, categories.Single(x => x.Id == phones.Id).ProductCount);
            await Assert.ThrowsAsync<BusinessException>(() => service.ProductsAsync(dealer.Id, new DealerProductQuery(MinPrice: 100, MaxPrice: 50), default));
            await Assert.ThrowsAsync<BusinessException>(() => service.ProductsAsync(dealer.Id, new DealerProductQuery(MinPrice: -1), default));
            await Assert.ThrowsAsync<BusinessException>(() => service.ProductsAsync(dealer.Id, new DealerProductQuery(PageSize: 1000), default));
            await Assert.ThrowsAsync<BusinessException>(() => service.ProductsAsync(Guid.NewGuid(), new(), default));
            root.IsActive = false;
            await db.SaveChangesAsync();
            Assert.Empty((await service.ProductsAsync(dealer.Id, new(), default)).Items);
            Assert.Empty(await service.CategoriesAsync(dealer.Id, default));
            dealer.Status = 0;
            await db.SaveChangesAsync();
            await Assert.ThrowsAsync<BusinessException>(() => service.ProductsAsync(dealer.Id, new(), default));
        }
        finally
        {
            NpgsqlConnection.ClearAllPools();
            await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{scratch.Database}\" WITH (FORCE)", admin);
            await drop.ExecuteNonQueryAsync();
        }
    }
}
