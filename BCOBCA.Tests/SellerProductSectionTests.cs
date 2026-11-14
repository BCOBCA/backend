using BCOBCA.BLL.Common;
using BCOBCA.BLL.DTOs;
using BCOBCA.BLL.Services;
using BCOBCA.DAL.Context;
using BCOBCA.DAL.Entities;
using BCOBCA.DAL.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BCOBCA.Tests;

public sealed class SellerProductSectionTests
{
    [Fact]
    public async Task UpdatesOnlySelectedSectionAndProtectsVersionsOwnershipAndReservedStock()
    {
        await SellerVerificationTests.WithDatabase(async options =>
        {
            Guid ownerId, otherId, shopId, productId, categoryId;
            await using (var seed = new ApplicationDbContext(options))
            {
                var owner = new Account { Email = "edit-product@example.test", Role = "Seller", Status = 1 };
                var other = new Account { Email = "edit-other@example.test", Role = "Seller", Status = 1 };
                var shop = new Store { OwnerAccountId = owner.Id, Name = "Shop", Slug = "edit-shop", StoreType = 1, Status = 1 };
                var brand = new Brand { OwnerAccountId = owner.Id, Name = "Brand", Slug = "edit-brand", Status = 1 };
                var category = new Category { Name = "Category", Slug = "edit-category", IsActive = true };
                seed.AddRange(owner, other, shop, brand, category); await seed.SaveChangesAsync();
                var commerce = new CommerceStore(seed); var service = new SellerService(commerce, catalog: new AdminCatalogService(commerce));
                var created = await service.CreateProductAsync(owner.Id, shop.Id, new SellerCreateProductInput(Guid.NewGuid(), brand.Id, category.Id,
                    "Original", 10000, 0, ["https://images.example.test/one.png"], 200, 20, 15, 5, Description: "Original description",
                    SpecificationText: "Material: cotton", Variants: [new("M", 10000, 5), new("L", 15000, 2)]), default);
                var reserved = await seed.ProductVariants.SingleAsync(v => v.ProductId == created.Id && v.VariantName == "M");
                reserved.ReservedQuantity = 3; await seed.SaveChangesAsync();
                (ownerId, otherId, shopId, productId, categoryId) = (owner.Id, other.Id, shop.Id, created.Id, category.Id);
            }
            async Task<SellerProductDetailDto> Read()
            {
                await using var db = new ApplicationDbContext(options);
                return await new SellerService(new CommerceStore(db)).ProductAsync(ownerId, shopId, productId, default);
            }
            async Task<SellerProductDetailDto> Update(string section, SellerProductSectionInput input, Guid? actor = null)
            {
                await using var db = new ApplicationDbContext(options);
                return await new SellerService(new CommerceStore(db)).UpdateProductSectionAsync(actor ?? ownerId, shopId, productId, section, input, default);
            }
            var original = await Read();
            await Assert.ThrowsAsync<BusinessException>(() => Update("description", new(original.UpdatedAt, Description: new string('x', 20001))));
            await Assert.ThrowsAsync<BusinessException>(() => Update("basic", new(original.UpdatedAt, Name: " ", CategoryId: categoryId)));
            await Assert.ThrowsAsync<BusinessException>(() => Update("basic", new(original.UpdatedAt, Name: "Invalid category", CategoryId: Guid.NewGuid())));
            await Assert.ThrowsAsync<BusinessException>(() => Update("images", new(original.UpdatedAt, ImageUrls: ["javascript:alert(1)"])));
            await Assert.ThrowsAsync<BusinessException>(() => Update("shipping", new(original.UpdatedAt, ShippingWeightGrams: 50001, ShippingLengthCm: 20, ShippingWidthCm: 15, ShippingHeightCm: 5)));
            await Assert.ThrowsAsync<BusinessException>(() => Update("status", new(original.UpdatedAt)));
            await Assert.ThrowsAsync<BusinessException>(() => Update("description", new(original.UpdatedAt, Description: "Forbidden"), otherId));
            var changed = await Update("description", new(original.UpdatedAt, Name: "Ignore unrelated name", Description: "Changed description"));
            Assert.Equal("Original", changed.Product.Name); Assert.Equal("Changed description", changed.Description);
            Assert.True(changed.UpdatedAt > original.UpdatedAt);
            Assert.Equal(409, (await Assert.ThrowsAsync<BusinessException>(() => Update("description", new(original.UpdatedAt, Description: "Stale")))).StatusCode);
            changed = await Update("basic", new(changed.UpdatedAt, Name: "Updated name", CategoryId: categoryId));
            Assert.Equal("Updated name", changed.Product.Name);
            var badStock = changed.Variants.Select(v => new SellerProductVariantUpdateInput(v.Id, Stock: v.Name == "M" ? 2 : v.Stock)).ToArray();
            Assert.Equal(409, (await Assert.ThrowsAsync<BusinessException>(() => Update("stock", new(changed.UpdatedAt, Variants: badStock)))).StatusCode);
            Assert.Equal(changed.UpdatedAt, (await Read()).UpdatedAt);
            var stock = changed.Variants.Select(v => new SellerProductVariantUpdateInput(v.Id, Stock: v.Stock + 1)).ToArray();
            changed = await Update("stock", new(changed.UpdatedAt, Variants: stock));
            Assert.Equal(3, changed.Variants.Single(v => v.Name == "M").Available); // 6 in stock minus 3 held.
            var prices = changed.Variants.Select(v => new SellerProductVariantUpdateInput(v.Id, v.Name, v.Price + 1000, v.Name == "M" ? 9000 : null, Stock: 0)).ToArray();
            changed = await Update("variants", new(changed.UpdatedAt, Variants: prices));
            Assert.Equal(9000, changed.Product.Price);
            Assert.Equal(6, changed.Variants.Single(v => v.Name == "M").Stock);
            await Assert.ThrowsAsync<BusinessException>(() => Update("variants", new(changed.UpdatedAt, Variants: [prices[0] with { Id = Guid.NewGuid() }, prices[1]])));
            await Assert.ThrowsAsync<BusinessException>(() => Update("variants", new(changed.UpdatedAt, Variants: [prices[0] with { Name = "Same" }, prices[1] with { Name = "same" }])));
            await Assert.ThrowsAsync<BusinessException>(() => Update("images", new(changed.UpdatedAt, ImageUrls: Enumerable.Range(1, 6).Select(n => $"https://images.example.test/{n}.png").ToArray())));
            changed = await Update("images", new(changed.UpdatedAt, ImageUrls: ["https://images.example.test/back.png", "https://images.example.test/front.png"]));
            Assert.EndsWith("back.png", changed.Product.ImageUrl);
            changed = await Update("info", new(changed.UpdatedAt, SpecificationText: "<b>Plain text</b>\nCare: wash gently"));
            Assert.Contains("<b>", changed.Specifications[0].Items[0].Value.GetString());
            changed = await Update("shipping", new(changed.UpdatedAt, ShippingWeightGrams: 350, ShippingLengthCm: 25, ShippingWidthCm: 15, ShippingHeightCm: 10));
            Assert.Equal(350, changed.ShippingWeightGrams);
            changed = await Update("status", new(changed.UpdatedAt, IsActive: false));
            Assert.False(changed.Product.Active);
            await using var check = new ApplicationDbContext(options);
            Assert.Equal(2, await check.ProductVariants.CountAsync(v => v.ProductId == productId));
            Assert.Equal(3, await check.ProductVariants.Where(v => v.ProductId == productId).SumAsync(v => v.ReservedQuantity));
            Assert.Equal(4, await check.Set<InventoryMovement>().CountAsync()); // two initial + two stock deltas only.
            Assert.Equal(9, await check.Set<AuditLog>().CountAsync(a => a.EntityType == "Product" && a.EntityId == productId));
            async Task<Exception?> Race(string value) { try { await Update("description", new(changed.UpdatedAt, Description: value)); return null; } catch (Exception ex) { return ex; } }
            var race = await Task.WhenAll(Race("Concurrent A"), Race("Concurrent B"));
            Assert.Single(race, result => result is null);
            var failed = Assert.Single(race, result => result is not null);
            Assert.True(failed is BusinessException { StatusCode: 409 } || DatabaseConflicts.IsConcurrency(failed!));
        });
    }
}
