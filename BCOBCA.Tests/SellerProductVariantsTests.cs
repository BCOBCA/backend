using System.Text.Json;
using BCOBCA.BLL.Common;
using BCOBCA.BLL.DTOs;
using BCOBCA.BLL.Services;
using BCOBCA.BLL.Validators;
using BCOBCA.DAL.Context;
using BCOBCA.DAL.Entities;
using BCOBCA.DAL.Repositories;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BCOBCA.Tests;

public sealed class SellerProductVariantsTests
{
    private static SellerCreateProductInput Input() => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Áo thun", 100000, 0,
        ["https://images.example.test/a.png"], 200, 20, 15, 5);

    [Fact]
    public async Task RejectsSixthImageDuplicateVersionsAndOverflowButAcceptsIndependentPrices()
    {
        var validator = new SellerCreateProductValidator();
        var input = Input() with { SpecificationText = "Chất liệu: cotton\nBảo quản: giặt nhẹ", Variants = [
            new("Trắng / M", 120000, 3), new("Trắng / L", 150000, 4, 135000), new("Đen / XL", 180000, 0)
        ] };
        await validator.ValidateAndThrowAsync(input);
        Assert.False((await validator.ValidateAsync(input with { ImageUrls = Enumerable.Range(1, 6).Select(n => $"https://images.example.test/{n}.png").ToArray() })).IsValid);
        Assert.False((await validator.ValidateAsync(input with { Variants = [new(" Trắng / M ", 100000, 1), new("trắng / m", 200000, 2)] })).IsValid);
        Assert.False((await validator.ValidateAsync(input with { Variants = [new("M", 100000, int.MaxValue), new("L", 100000, 1)] })).IsValid);
        Assert.False((await validator.ValidateAsync(input with { Variants = [new("M", 0, 1)] })).IsValid);
        Assert.False((await validator.ValidateAsync(input with { Variants = [new("M", 100000, 1, 100001)] })).IsValid);
        Assert.False((await validator.ValidateAsync(input with { SpecificationText = new string('x', 20001) })).IsValid);
    }

    [Fact]
    public async Task StoresFreeTextAndEachPriceStockWithoutForcingOldCategoryFields()
    {
        await SellerVerificationTests.WithDatabase(async options =>
        {
            await using var db = new ApplicationDbContext(options);
            var owner = new Account { Email = "variant-owner@example.test", Role = "Seller", Status = 1 };
            var shop = new Store { OwnerAccountId = owner.Id, Name = "Shop", Slug = "flexible-products", StoreType = 1, Status = 1 };
            var brand = new Brand { OwnerAccountId = owner.Id, Name = "Brand", Slug = "flexible-brand", Status = 1 };
            var category = new Category { Name = "Danh mục", Slug = "flexible-category", IsActive = true,
                AttributeDefinitions = """[{"key":"chipset","label":"Chip","group":"Cấu hình","required":true},{"key":"size","label":"Size","group":"Phân loại","scope":"variant","required":true}]""" };
            db.AddRange(owner, shop, brand, category); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
            var store = new CommerceStore(db); var service = new SellerService(store, catalog: new AdminCatalogService(store));
            var input = Input() with { BrandId = brand.Id, CategoryId = category.Id, SpecificationText = "Chất liệu: cotton\nGiặt ở nhiệt độ thấp.",
                Variants = [new("Trắng / M", 120000, 3), new("Trắng / L", 150000, 4, 135000), new("Đen / XL", 80000, 0)] };
            var created = await service.CreateProductAsync(owner.Id, shop.Id, input, default);
            Assert.Equal(7, created.Stock); Assert.Equal(120000, created.Price); Assert.Equal(3, created.VariantCount);
            var variants = await db.ProductVariants.AsNoTracking().OrderBy(v => v.VariantName).ToListAsync();
            Assert.Equal(3, variants.Count);
            Assert.Equal(135000, variants.Single(v => v.VariantName == "Trắng / L").SalePrice);
            Assert.Equal(3, variants.Single(v => v.VariantName == "Trắng / M").StockQuantity);
            Assert.Equal(7, await db.Set<InventoryMovement>().SumAsync(m => m.Quantity));
            var product = await db.Products.AsNoTracking().SingleAsync();
            var values = ProductSpecificationRules.ParseValues(product.Specifications);
            Assert.Equal(input.SpecificationText, values["free_text"].GetString());
            var definitions = await ProductSpecificationRules.ResolveDefinitionsAsync(store, category.Id, default);
            var groups = ProductSpecificationRules.BuildGroups(values, definitions);
            Assert.Equal(input.SpecificationText, Assert.Single(Assert.Single(groups).Items).Value.GetString());
            var retry = await service.CreateProductAsync(owner.Id, shop.Id, input, default);
            Assert.Equal(created.Id, retry.Id); Assert.Equal(3, await db.ProductVariants.CountAsync());
            Assert.Equal(2, await db.Set<InventoryMovement>().CountAsync());
            await Assert.ThrowsAsync<BusinessException>(() => service.CreateProductAsync(owner.Id, shop.Id, Input() with { BrandId = brand.Id, CategoryId = category.Id }, default));
        });
    }

    [Fact]
    public void FreeTextIsBoundedPlainTextAndLegacySchemaStillValidates()
    {
        var definitions = new[] { new CategoryAttributeDefinitionDto("chip", "Chip", "Cấu hình", Required: true) };
        var free = ProductSpecificationRules.ParseValues("""{"free_text":"<script>plain text</script>\nRAM: 8GB"}""");
        ProductSpecificationRules.ValidateValues(free, definitions, "product");
        Assert.Throws<BusinessException>(() => ProductSpecificationRules.ValidateValues(new Dictionary<string, JsonElement>(), definitions, "product"));
        Assert.Throws<BusinessException>(() => ProductSpecificationRules.ValidateValues(ProductSpecificationRules.ParseValues("""{"free_text":42}"""), definitions, "product"));
        Assert.Equal("<script>plain text</script>\nRAM: 8GB", Assert.Single(Assert.Single(ProductSpecificationRules.BuildGroups(free, definitions)).Items).Value.GetString());
    }
}
