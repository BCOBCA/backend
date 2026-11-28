using BCOBCA.API.Controllers;
using BCOBCA.BLL.Common;
using BCOBCA.BLL.DTOs;
using BCOBCA.BLL.Services;
using Microsoft.AspNetCore.Authorization;
using Xunit;

namespace BCOBCA.Tests;

public sealed class AdminProductsTests
{
    private static AdminProductQuery Valid() => new() { From = "2026-01-01", To = "2026-01-31" };

    [Fact]
    public void EndpointRequiresAdminRoles() => Assert.Contains(typeof(AdminProductsController)
        .GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>(), a => a.Roles == "Admin,SuperAdmin");

    [Fact]
    public void DateRangeUsesInclusiveVietnamDatesAndExclusiveEnd()
    {
        var (start, end) = AdminStoreService.ValidateProductQuery(Valid());
        Assert.Equal(new DateTimeOffset(2025, 12, 31, 17, 0, 0, TimeSpan.Zero), start);
        Assert.Equal(new DateTimeOffset(2026, 1, 31, 17, 0, 0, TimeSpan.Zero), end);
    }

    [Theory]
    [InlineData(-1, 100)]
    [InlineData(200, 100)]
    public void InvalidPriceRangesAreRejected(int min, int max)
    {
        var query = Valid(); query.MinPrice = min; query.MaxPrice = max;
        Assert.Throws<BusinessException>(() => AdminStoreService.ValidateProductQuery(query));
    }

    [Fact]
    public void FractionalPricesAreRejected()
    {
        var query = Valid(); query.MinPrice = 1.5m;
        Assert.Throws<BusinessException>(() => AdminStoreService.ValidateProductQuery(query));
    }

    [Fact]
    public void InvalidPagingStatusStockSortAndDatesAreRejected()
    {
        Action<AdminProductQuery>[] invalid = [q => q.Page = 0, q => q.PageSize = 100, q => q.Status = "blocked",
            q => q.Stock = "unknown", q => q.Sort = "unknown", q => q.To = "2025-12-01", q => q.To = "2026-05-01",
            q => q.Search = new string('x', 201)];
        foreach (var patch in invalid) { var query = Valid(); patch(query); Assert.Throws<BusinessException>(() => AdminStoreService.ValidateProductQuery(query)); }
    }
}
