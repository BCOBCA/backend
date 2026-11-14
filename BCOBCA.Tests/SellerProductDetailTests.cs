using BCOBCA.BLL.Common;
using BCOBCA.BLL.DTOs;
using BCOBCA.BLL.Services;
using BCOBCA.DAL.Context;
using BCOBCA.DAL.Entities;
using BCOBCA.DAL.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BCOBCA.Tests;

public sealed class SellerProductDetailTests
{
    [Fact]
    public async Task OwnerReadsHiddenProductDetailWithoutExposingForeignOrDeletedProducts()
    {
        await SellerVerificationTests.WithDatabase(async options =>
        {
            await using var db = new ApplicationDbContext(options);
            var owner = new Account { Email = "detail-owner@example.test", Role = "Seller", Status = 1 };
            var other = new Account { Email = "detail-other@example.test", Role = "Seller", Status = 1 };
            var shop = new Store { OwnerAccountId = owner.Id, Name = "Shop", Slug = "detail-shop", StoreType = 1, Status = 1 };
            var foreign = new Store { OwnerAccountId = other.Id, Name = "Other", Slug = "detail-other", StoreType = 1, Status = 1 };
            var brand = new Brand { OwnerAccountId = owner.Id, Name = "Brand", Slug = "detail-brand", Status = 1 };
            var category = new Category { Name = "Category", Slug = "detail-category", IsActive = true };
            db.AddRange(owner, other, shop, foreign, brand, category); await db.SaveChangesAsync();
            var commerce = new CommerceStore(db); var seller = new SellerService(commerce, catalog: new AdminCatalogService(commerce));
            var created = await seller.CreateProductAsync(owner.Id, shop.Id, new SellerCreateProductInput(Guid.NewGuid(), brand.Id,
                category.Id, "Product", 10000, 0, ["https://images.example.test/front.png", "https://images.example.test/back.png"],
                200, 20, 15, 5, Description: "Full description", IsActive: false, SpecificationText: "Material: cotton\nCare: wash gently",
                Variants: [new("White / M", 10000, 5), new("White / L", 15000, 0, 12000)]), default);
            var row = await db.ProductVariants.SingleAsync(v => v.ProductId == created.Id && v.VariantName == "White / M");
            row.ReservedQuantity = 2; await db.SaveChangesAsync();
            var detail = await seller.ProductAsync(owner.Id, shop.Id, created.Id, default);
            Assert.False(detail.Product.Active); Assert.Equal(3, detail.Product.Stock);
            Assert.Equal("Full description", detail.Description); Assert.Equal(2, detail.ImageUrls.Count);
            Assert.EndsWith("front.png", detail.ImageUrls[0]); Assert.StartsWith("SP-", detail.SKU);
            Assert.Equal(3, detail.Variants.Single(v => v.Name == "White / M").Available);
            Assert.Equal(12000, detail.Variants.Single(v => v.Name == "White / L").SalePrice);
            Assert.Equal(200, detail.ShippingWeightGrams); Assert.Single(detail.Specifications);
            Assert.Equal(404, (await Assert.ThrowsAsync<BusinessException>(() => seller.ProductAsync(other.Id, shop.Id, created.Id, default))).StatusCode);
            await Assert.ThrowsAsync<BusinessException>(() => seller.ProductAsync(other.Id, foreign.Id, created.Id, default));
            var entity = await db.Products.SingleAsync(p => p.Id == created.Id); entity.IsDeleted = true; await db.SaveChangesAsync();
            await Assert.ThrowsAsync<BusinessException>(() => seller.ProductAsync(owner.Id, shop.Id, created.Id, default));
        });
    }
}
