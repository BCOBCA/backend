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

public sealed class FavoriteCatalogFlowTests
{
    [Fact]
    public async Task FavoritesUseCatalogVariantPricesAndStockAndRemainUserScoped()
    {
        EnvironmentBootstrap.Load();
        var supplied = Environment.GetEnvironmentVariable("ConnectionStrings__Default");
        var scratch = new NpgsqlConnectionStringBuilder(supplied) { Database = "bcobca_favorite_test_" + Guid.NewGuid().ToString("N") };
        await using var admin = new NpgsqlConnection(supplied);
        await admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE DATABASE \"{scratch.Database}\" TEMPLATE template0 LOCALE_PROVIDER icu ICU_LOCALE 'vi-VN'", admin)) await create.ExecuteNonQueryAsync();
        try
        {
            await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(scratch.ConnectionString).Options);
            await db.Database.MigrateAsync();
            var account = new Account { Email = "favorites@example.test", Status = 1 };
            var user = new User { AccountId = account.Id, ReferralCode = "FAVORITE-QA", DateOfBirth = new DateOnly(2000, 1, 1) };
            var brand = new Brand { OwnerAccountId = account.Id, Name = "Brand", Slug = "favorite-brand", Status = 1 };
            var category = new Category { Name = "Category", Slug = "favorite-category", IsActive = true };
            var product = new Product { BrandId = brand.Id, CategoryId = category.Id, Name = "Favorite", Slug = "favorite", SKU = "FAVORITE", BasePrice = 500, Status = 1 };
            var available = new ProductVariant { ProductId = product.Id, SKU = "AVAILABLE", VariantName = "Available", Price = 1000, SalePrice = 800, StockQuantity = 5, ReservedQuantity = 2, IsActive = true };
            db.AddRange(account, user, brand, category, product, available,
                new ProductVariant { ProductId = product.Id, SKU = "RESERVED", Price = 100, StockQuantity = 2, ReservedQuantity = 2, IsActive = true },
                new ProductImage { ProductId = product.Id, ImageUrl = "https://example.test/product.jpg", SortOrder = 1 },
                new ProductFavorite { ProductId = product.Id, UserId = user.Id });
            await db.SaveChangesAsync();
            var store = new CommerceStore(db);
            var member = new MemberService(store);
            var favorite = Assert.Single((await member.FavoritesAsync(user.Id, new CatalogQuery(), default)).Items);
            var catalog = Assert.Single((await new CatalogService(store).ProductsAsync(new CatalogQuery(), default, user.Id)).Items);
            Assert.Equal(available.Id, favorite.VariantId);
            Assert.Equal(3, favorite.AvailableStock);
            Assert.Equal(catalog.Price, favorite.Price);
            Assert.Equal(catalog.SalePrice, favorite.SalePrice);
            Assert.Equal(catalog.ImageUrl, favorite.ImageUrl);
            Assert.True(favorite.IsFavorite);
            Assert.Empty((await member.FavoritesAsync(Guid.NewGuid(), new CatalogQuery(), default)).Items);
        }
        finally
        {
            NpgsqlConnection.ClearAllPools();
            await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{scratch.Database}\" WITH (FORCE)", admin);
            await drop.ExecuteNonQueryAsync();
        }
    }
}
