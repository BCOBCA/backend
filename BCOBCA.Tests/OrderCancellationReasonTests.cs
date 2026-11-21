using BCOBCA.BLL.Common;
using Xunit;

namespace BCOBCA.Tests;

public sealed class OrderCancellationReasonTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Lý do không có trong danh sách")]
    public void BuyerMustChooseAnAllowedReason(string? reason)
        => Assert.Throws<BusinessException>(() => OrderCancellationReasons.Buyer(reason));

    [Fact]
    public void BuyerReasonIsTrimmed()
        => Assert.Equal("Không còn nhu cầu mua", OrderCancellationReasons.Buyer("  Không còn nhu cầu mua  "));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void SellerCannotCancelWithoutAReason(string? reason)
        => Assert.Throws<BusinessException>(() => OrderCancellationReasons.Seller(reason));

    [Fact]
    public void SellerMayGiveATrimmedCustomReason()
        => Assert.Equal("Sản phẩm bị lỗi khi đóng gói", OrderCancellationReasons.Seller(" Sản phẩm bị lỗi khi đóng gói "));

    [Fact]
    public void SellerReasonHasALengthLimit()
        => Assert.Throws<BusinessException>(() => OrderCancellationReasons.Seller(new string('a', 501)));
}
