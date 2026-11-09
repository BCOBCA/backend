using BCOBCA.BLL.DTOs;
using BCOBCA.DAL.Context;
using BCOBCA.DAL.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BCOBCA.Tests;

public sealed class CategorySchemaTests
{
    [Fact]
    public void CategoryKeepsImageButRemovesIconWithoutChangingBrandCategory()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql("Host=localhost;Database=unused").Options);
        var category = db.Model.FindEntityType(typeof(Category))!;
        Assert.Null(category.FindProperty("IconUrl"));
        Assert.NotNull(category.FindProperty("ImageUrl"));
        Assert.Equal("jsonb", category.FindProperty("AttributeDefinitions")!.GetColumnType());
        Assert.Equal(8, category.GetProperties().Count());
        Assert.NotNull(db.Model.FindEntityType(typeof(BrandCategory))!.FindProperty("IconUrl"));
    }

    [Fact]
    public void CategoryContractsDoNotContainIconUrl()
    {
        Assert.DoesNotContain(typeof(CategoryDto).GetProperties(), x => x.Name == "IconUrl");
        Assert.DoesNotContain(typeof(CategoryInput).GetProperties(), x => x.Name == "IconUrl");
        Assert.Contains(typeof(CategoryDto).GetProperties(), x => x.Name == "ImageUrl");
    }
}
