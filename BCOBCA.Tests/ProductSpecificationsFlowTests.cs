using System.Text.Json;
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

public sealed class ProductSpecificationsFlowTests
{
    private static Dictionary<string, JsonElement> Values(string json) => JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)!;

    [Fact]
    public async Task AdminPersistsInheritedTypedSpecsAndKeepsLegacyUpdatesCompatible()
    {
        EnvironmentBootstrap.Load();
        var supplied = Environment.GetEnvironmentVariable("ConnectionStrings__Default");
        var scratch = new NpgsqlConnectionStringBuilder(supplied) { Database = "bcobca_specs_test_" + Guid.NewGuid().ToString("N") };
        await using var adminConnection = new NpgsqlConnection(supplied);
        await adminConnection.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE DATABASE \"{scratch.Database}\" TEMPLATE template0 LOCALE_PROVIDER icu ICU_LOCALE 'vi-VN'", adminConnection)) await create.ExecuteNonQueryAsync();
        try
        {
            await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(scratch.ConnectionString).Options);
            await db.Database.MigrateAsync();
            Assert.False(db.Database.HasPendingModelChanges());
            var account = new Account { Email = "specs@example.test", Status = 1 };
            var brand = new Brand { OwnerAccountId = account.Id, Name = "Brand", Slug = "specs-brand", Status = 1 };
            db.AddRange(account, brand, new AccountRole { AccountId = account.Id, Role = "SuperAdmin" });
            await db.SaveChangesAsync();
            var store = new CommerceStore(db);
            var admin = new AdminCatalogService(store);
            var catalog = new CatalogService(store);
            var root = await admin.SaveCategoryAsync(account.Id, null, new CategoryInput("Electronics", "specs-electronics", AttributeDefinitions: new[] {
                new CategoryAttributeDefinitionDto("weight", "Khối lượng", "Kích thước", "number", "g"),
                new CategoryAttributeDefinitionDto("color", "Màu sắc", "Phân loại", Scope: "variant") }), default);
            var phoneInput = new CategoryInput("Phones", "specs-phones", root, AttributeDefinitions: new[] {
                new CategoryAttributeDefinitionDto("chipset", "Chip", "Hiệu năng", Required: true),
                new CategoryAttributeDefinitionDto("battery", "Pin", "Pin", "number", "mAh"),
                new CategoryAttributeDefinitionDto("stereo", "Loa stereo", "Âm thanh", "boolean"),
                new CategoryAttributeDefinitionDto("storage", "Dung lượng", "Phân loại", Scope: "variant") });
            var phone = await admin.SaveCategoryAsync(account.Id, null, phoneInput, default);
            Assert.Equal(6, (await catalog.CategoryAttributesAsync(phone, default)).Count);
            var variant = new VariantInput(null, "PHONE-SPECS-DEFAULT", "Đen / 128 GB", 1000, 5, 800, "{\"color\":\"Đen\",\"storage\":\"128 GB\"}");
            var input = new ProductInput(brand.Id, phone, "Phone", "specs-phone", "PHONE-SPECS", 1000, new[] { variant }, new[] { "https://example.test/photo.webp" }, Specifications: Values("{\"chipset\":\"Example chip\",\"battery\":3400,\"stereo\":true,\"weight\":150}"));
            await Assert.ThrowsAsync<BusinessException>(() => admin.SaveProductAsync(Guid.NewGuid(), null, input, default));
            var id = await admin.SaveProductAsync(account.Id, null, input, default);
            var seller = new Store { OwnerAccountId = account.Id, StoreType = 1, Status = 1, Name = "Seller A", Slug = "seller-a", Phone = "0900000001" };
            var privatePhoneSeller = new Store { OwnerAccountId = account.Id, StoreType = 1, Status = 1, Name = "Seller B", Slug = "seller-b", Phone = "0900000002" };
            var hiddenSeller = new Store { OwnerAccountId = account.Id, StoreType = 1, Status = 0, Name = "Hidden seller", Slug = "seller-hidden" };
            var deletedSeller = new Store { OwnerAccountId = account.Id, StoreType = 1, Status = 1, DeletedAt = DateTimeOffset.UtcNow, Name = "Deleted seller", Slug = "seller-deleted" };
            var unrelatedSeller = new Store { OwnerAccountId = account.Id, StoreType = 1, Status = 1, Name = "Unrelated seller", Slug = "seller-unrelated" };
            db.AddRange(seller, privatePhoneSeller, hiddenSeller, deletedSeller, unrelatedSeller);
            db.AddRange(new StoreSettings { StoreId = seller.Id, ShowPhone = true }, new StoreSettings { StoreId = privatePhoneSeller.Id, ShowPhone = false });
            foreach (var linkedSeller in new[] { seller, privatePhoneSeller, hiddenSeller, deletedSeller })
                db.Add(new StoreProduct { StoreId = linkedSeller.Id, ProductId = id });
            await db.SaveChangesAsync();
            var item = await catalog.ProductAsync(id, default);
            var sellers = Assert.IsAssignableFrom<IReadOnlyList<ProductSellerDto>>(item.Sellers);
            Assert.Equal(new[] { seller.Id, privatePhoneSeller.Id }, sellers.Select(s => s.Id));
            Assert.Equal("Seller A", sellers[0].Name);
            Assert.Equal("seller-a", sellers[0].Slug);
            Assert.Equal("0900000001", sellers[0].Phone);
            Assert.Null(sellers[1].Phone);
            Assert.DoesNotContain(sellers, s => s.Name == brand.Name);
            Assert.Equal("PHONE-SPECS", item.SKU);
            Assert.NotEqual(item.Variants!.Single().SKU, item.SKU);
            Assert.Equal(4, item.Specifications!.Count);
            Assert.Equal(3400, item.Specifications.Single(g => g.Name == "Pin").Items.Single().Value.GetInt32());
            Assert.DoesNotContain(item.Variants!.Single().Attributes!, p => p.Key == "chipset");
            var saved = await db.ProductVariants.SingleAsync(v => v.ProductId == id);
            var update = input with { Name = "Updated phone", Specifications = null, Variants = new[] { variant with { Id = saved.Id } } };
            await admin.SaveProductAsync(account.Id, id, update, default);
            Assert.Equal(4, (await catalog.ProductAsync(id, default)).Specifications!.Count);
            Assert.Equal(5, (await catalog.ProductAsync(id, default)).Variants!.Single().AvailableStock);
            await admin.SaveCategoryAsync(account.Id, phone, phoneInput with { AttributeDefinitions = null }, default);
            Assert.Equal(6, (await catalog.CategoryAttributesAsync(phone, default)).Count);
            await Assert.ThrowsAsync<BusinessException>(() => admin.SaveProductAsync(account.Id, null, input with { SKU = "BAD", Slug = "bad-specs", Specifications = Values("{\"battery\":3400}") }, default));
            Assert.Equal(1, await db.Products.CountAsync());
            var clothes = await admin.SaveCategoryAsync(account.Id, null, new CategoryInput("Clothes", "specs-clothes", AttributeDefinitions: new[] {
                new CategoryAttributeDefinitionDto("material", "Chất liệu", "May mặc", Required: true),
                new CategoryAttributeDefinitionDto("color", "Màu sắc", "Phân loại", Scope: "variant"),
                new CategoryAttributeDefinitionDto("size", "Kích thước", "Phân loại", Scope: "variant") }), default);
            await Assert.ThrowsAsync<BusinessException>(() => admin.SaveProductAsync(account.Id, id, update with { CategoryId = clothes }, default));
            await admin.SaveProductAsync(account.Id, id, update with { CategoryId = clothes, Specifications = Values("{\"material\":\"Cotton\"}"), Variants = new[] { variant with { Id = saved.Id, Attributes = "{\"color\":\"Đen\",\"size\":\"M\"}" } } }, default);
            var shirt = await catalog.ProductAsync(id, default);
            Assert.Equal("Cotton", Assert.Single(Assert.Single(shirt.Specifications!).Items).Value.GetString());
            Assert.Equal("M", shirt.Variants!.Single().Attributes!["size"]);
        }
        finally
        {
            NpgsqlConnection.ClearAllPools();
            await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{scratch.Database}\" WITH (FORCE)", adminConnection);
            await drop.ExecuteNonQueryAsync();
        }
    }
}
