using BCOBCA.DAL.Context;
using BCOBCA.DAL.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;

namespace BCOBCA.Tests;

public sealed class ProductDescriptionMigrationTests
{
    [Fact]
    public async Task RemovesShortDescriptionAndPreservesOnlyDescriptionWhenItWasBlank()
    {
        await SellerVerificationTests.WithDatabase(async options =>
        {
            await using var db = new ApplicationDbContext(options);
            Assert.False(db.Database.HasPendingModelChanges());
            var owner = new Account { Email = "description-migration@example.test", Status = 1 };
            var shop = new Store { OwnerAccountId = owner.Id, Name = "Shop", Slug = "description-migration", StoreType = 1, Status = 1 };
            var brand = new Brand { OwnerAccountId = owner.Id, Name = "Brand", Slug = "description-migration", Status = 1 };
            var category = new Category { Name = "Category", Slug = "description-migration", IsActive = true };
            var blank = new Product { BrandId = brand.Id, StoreId = shop.Id, CategoryId = category.Id, Name = "Blank", Slug = "description-blank", SKU = "DESC-BLANK", Description = "  " };
            var detailed = new Product { BrandId = brand.Id, StoreId = shop.Id, CategoryId = category.Id, Name = "Detailed", Slug = "description-detailed", SKU = "DESC-DETAIL", Description = "Keep detailed description" };
            db.AddRange(owner, shop, brand, category, blank, detailed); await db.SaveChangesAsync();
            var migrator = db.GetService<IMigrator>();
            await migrator.MigrateAsync("20261113091512_SellerReturnBranch");
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Products\" SET \"ShortDescription\" = {"Preserve short description"} WHERE \"Id\" = {blank.Id}");
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Products\" SET \"ShortDescription\" = {"Do not overwrite detail"} WHERE \"Id\" = {detailed.Id}");
            await migrator.MigrateAsync();
            db.ChangeTracker.Clear();
            Assert.Equal("Preserve short description", (await db.Products.SingleAsync(p => p.Id == blank.Id)).Description);
            Assert.Equal("Keep detailed description", (await db.Products.SingleAsync(p => p.Id == detailed.Id)).Description);
            var columns = await db.Database.SqlQueryRaw<int>("SELECT count(*)::int AS \"Value\" FROM information_schema.columns WHERE table_name = 'Products' AND column_name = 'ShortDescription'").SingleAsync();
            Assert.Equal(0, columns);
        });
    }
}
