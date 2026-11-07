using BCOBCA.BLL.Common;
using Xunit;

namespace BCOBCA.Tests;

public sealed class DealerProvinceTests
{
    [Fact]
    public void CoverageIsNormalizedFromTheCatalog()
    {
        Assert.Equal("Toàn quốc", DealerProvinces.Normalize("Toàn quốc"));
        Assert.Equal("Hà Nội, Hồ Chí Minh, Đà Nẵng", DealerProvinces.Normalize(" Hà Nội , Hồ Chí Minh, Đà Nẵng, Hà Nội "));
        Assert.Equal("Hồ Chí Minh", DealerProvinces.Resolve("79"));
        Assert.Equal("Hà Nội", DealerProvinces.Resolve("Hà Nội"));
        Assert.Equal(34, DealerProvinces.Names.Count);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("Toàn quốc, Hà Nội")]
    [InlineData("Hà Nội,, Đà Nẵng")]
    [InlineData("Tỉnh không tồn tại")]
    [InlineData("79")]
    public void InvalidOrMixedCoverageIsRejected(string? value)
        => Assert.Throws<BusinessException>(() => DealerProvinces.Normalize(value));
}
