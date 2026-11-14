using BCOBCA.BLL.Common;
using BCOBCA.BLL.DTOs;
using BCOBCA.BLL.Services;
using BCOBCA.DAL.Context;
using BCOBCA.DAL.Entities;
using BCOBCA.DAL.Repositories;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BCOBCA.Tests;

public sealed class SellerOtherBrandTests
{
    [Fact]
    public async Task OtherBrandIsTransactionalUnverifiedStoreScopedAndReusable()
    {
        await SellerVerificationTests.WithDatabase(async options =>
        {
            await using var db = new ApplicationDbContext(options);
            var owner = new Account { Email = "other-brand@example.test", Role = "Seller", Status = 1 };
            var stranger = new Account { Email = "other-brand-stranger@example.test", Role = "Seller", Status = 1 };
            var shop = new Store { OwnerAccountId = owner.Id, Name = "Shop", Slug = "other-brand-shop", StoreType = 1, Status = 1 };
            var second = new Store { OwnerAccountId = owner.Id, Name = "Second", Slug = "other-brand-second", StoreType = 1, Status = 1 };
            var category = new Category { Name = "Category", Slug = "other-brand-category", IsActive = true,
                AttributeDefinitions = """[{"key":"required","label":"Required","group":"Details","required":true}]""" };
            db.AddRange(owner, stranger, shop, second, category); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
            var commerce = new CommerceStore(db);
            var service = new SellerService(commerce, catalog: new AdminCatalogService(commerce));
            var choices = await service.ProductOptionsAsync(owner.Id, shop.Id, default);
            Assert.True(choices.SupportsOtherBrand); Assert.Empty(choices.Brands);
            var input = new SellerCreateProductInput(Guid.NewGuid(), Guid.Empty, category.Id, "Generic product", 10000, 2,
                ["https://images.example.test/other.png"], 100, 10, 10, 10, UseOtherBrand: true);
            await Assert.ThrowsAsync<ValidationException>(() => service.CreateProductAsync(owner.Id, shop.Id, input with { UseOtherBrand = false }, default));
            await Assert.ThrowsAsync<ValidationException>(() => service.CreateProductAsync(owner.Id, shop.Id, input with { BrandId = Guid.NewGuid() }, default));
            await Assert.ThrowsAsync<BusinessException>(() => service.CreateProductAsync(stranger.Id, shop.Id, input, default));
            // Category validation fails after fallback resolution, and must roll back the brand as well.
            await Assert.ThrowsAsync<BusinessException>(() => service.CreateProductAsync(owner.Id, shop.Id, input, default));
            Assert.Empty(await db.Brands.AsNoTracking().ToListAsync()); Assert.Empty(await db.Products.AsNoTracking().ToListAsync());
            db.ChangeTracker.Clear();
            input = input with { SpecificationText = "" };
            var created = await service.CreateProductAsync(owner.Id, shop.Id, input, default);
            var brand = await db.Brands.AsNoTracking().SingleAsync();
            Assert.False(brand.IsVerified); Assert.Equal(owner.Id, brand.OwnerAccountId);
            await Assert.ThrowsAsync<BusinessException>(() => service.CreateProductAsync(owner.Id, shop.Id,
                input with { RequestId = Guid.NewGuid(), UseOtherBrand = false, BrandId = brand.Id }, default));
            Assert.Equal(brand.Id, (await db.Products.AsNoTracking().SingleAsync()).BrandId);
            Assert.Equal(created.Id, (await service.CreateProductAsync(owner.Id, shop.Id, input, default)).Id);
            await service.CreateProductAsync(owner.Id, shop.Id, input with { RequestId = Guid.NewGuid() }, default);
            Assert.Equal(1, await db.Brands.CountAsync());
            Assert.Empty((await service.ProductOptionsAsync(owner.Id, shop.Id, default)).Brands);
            await service.CreateProductAsync(owner.Id, second.Id, input with { RequestId = Guid.NewGuid() }, default);
            Assert.Equal(2, await db.Brands.CountAsync());
            var catalog = new CatalogService(commerce);
            Assert.Empty((await catalog.BrandsAsync(new CatalogQuery(), default)).Items);
            Assert.Equal(brand.Id, (await catalog.BrandAsync(brand.Id, default)).Id);
            Assert.Equal(3, (await catalog.ProductsAsync(new CatalogQuery(), default)).TotalItems);
        });
    }
}
