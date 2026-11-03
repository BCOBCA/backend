using BCOBCA.BLL.DTOs;
using BCOBCA.DAL.Context;
using BCOBCA.DAL.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BCOBCA.Tests;

public sealed class BannerContractTests
{
    [Fact]
    public void BannerTableContainsOnlyImageSchedulingAndIdentityColumns()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=localhost;Database=unused;Username=unused").Options);
        var entity = db.Model.FindEntityType(typeof(Banner))!;
        Assert.Equal(new[] { "EndsAt", "Id", "ImageUrl", "IsActive", "StartsAt", "TargetUrl" }, entity.GetProperties().Select(x => x.Name).Order());
    }

    [Fact]
    public void PublicAndAdminContractsDoNotExposeRemovedBannerFields()
    {
        Assert.Equal(new[] { "Id", "ImageUrl", "TargetUrl" }, typeof(BannerDto).GetProperties().Select(x => x.Name).Order());
        Assert.Equal(new[] { "EndsAt", "ImageUrl", "IsActive", "StartsAt", "TargetUrl" }, typeof(BannerInput).GetProperties().Select(x => x.Name).Order());
    }
}
