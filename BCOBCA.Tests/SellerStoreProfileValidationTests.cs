using BCOBCA.BLL.DTOs;
using BCOBCA.BLL.Validators;
using Xunit;

namespace BCOBCA.Tests;

public sealed class SellerStoreProfileValidationTests
{
    internal static SellerStoreProfileInput Input(DateTimeOffset version) => new("Cửa hàng An", "Trà và quà tặng",
        "https://example.test/logo.png", "https://example.test/cover.png", "shop@example.test", "0901234567",
        "https://example.test", "Toàn quốc", [], new("Chi nhánh chính", "0901234567", "12 Nguyễn Trãi, Hà Nội"),
        new(true, true, false, true), version);

    [Fact]
    public void AcceptsCompleteProfileAndRejectsUnsafeOrOversizedInput()
    {
        var validator = new SellerStoreProfileValidator();
        var valid = Input(DateTimeOffset.UtcNow);
        Assert.True(validator.Validate(valid).IsValid);
        foreach (var invalid in new[] {
            valid with { Name = " " }, valid with { Description = new string('x', 5001) },
            valid with { Email = "invalid" }, valid with { Phone = "abc" },
            valid with { LogoUrl = "javascript:alert(1)" }, valid with { CoverUrl = "http://example.test/a.png" },
            valid with { Website = "https://secret@example.test" }, valid with { MainBranch = new("Main", "", "") },
            valid with { UpdatedAt = default }, valid with { CategoryIds = new Guid[101] }
        }) Assert.False(validator.Validate(invalid).IsValid);
    }
}
