using System.Reflection;
using BCOBCA.API.Controllers;
using Microsoft.AspNetCore.Authorization;
using Xunit;
namespace BCOBCA.Tests;
public sealed class VoucherAuthorizationTests
{
    [Theory]
    [InlineData("AddVoucher")]
    [InlineData("PutVoucher")]
    public void SellerCannotBypassLifecycleThroughLegacyAdminWrites(string method)
    {
        Assert.Contains(typeof(AdminCatalogController).GetMethod(method)!.GetCustomAttributes<AuthorizeAttribute>(), a => a.Roles == "Admin,SuperAdmin");
    }
    [Fact]
    public void SupervisionRequiresAdminRoleAtControllerBoundary()
        => Assert.Contains(typeof(AdminVouchersController).GetCustomAttributes<AuthorizeAttribute>(), a => a.Roles == "Admin,SuperAdmin");
}
