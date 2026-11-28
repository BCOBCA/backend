using BCOBCA.BLL.Common;
using BCOBCA.BLL.DTOs;
using BCOBCA.BLL.Services;
using BCOBCA.API.Controllers;
using Microsoft.AspNetCore.Authorization;
using Xunit;

namespace BCOBCA.Tests;

public sealed class AdminSupportTests
{
    [Fact]
    public void InboxEndpointRequiresAdminRole() => Assert.Contains(typeof(AdminOperationsController).GetMethod("Support")!
        .GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>(), a => a.Roles == "Admin,SuperAdmin");

    [Fact]
    public void DatesAreOptionalAndUseVietnamBoundaries()
    {
        var empty = AdminOperationsService.ValidateSupportQuery(new());
        Assert.Null(empty.Start); Assert.Null(empty.End);
        var (start, end) = AdminOperationsService.ValidateSupportQuery(new() { From = "2026-01-01", To = "2026-01-02" });
        Assert.Equal(new DateTimeOffset(2025, 12, 31, 17, 0, 0, TimeSpan.Zero), start);
        Assert.Equal(new DateTimeOffset(2026, 1, 2, 17, 0, 0, TimeSpan.Zero), end);
    }

    [Fact]
    public void RejectsInvalidPagingFiltersAndPartialDateRanges()
    {
        Action<AdminSupportQuery>[] invalid = [q => q.Page = 0, q => q.PageSize = 200, q => q.Status = "PENDING",
            q => q.Search = new string('a', 201), q => q.Subject = new string('a', 201), q => q.From = "2026-01-01",
            q => { q.From = "2026-02-01"; q.To = "2026-01-01"; }, q => { q.From = "2026-01-01"; q.To = "2026-06-01"; }];
        foreach (var patch in invalid) { var q = new AdminSupportQuery(); patch(q); Assert.Throws<BusinessException>(() => AdminOperationsService.ValidateSupportQuery(q)); }
    }
}
