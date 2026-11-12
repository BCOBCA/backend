using System.Globalization;
using BCOBCA.BLL.Common;
using Xunit;

namespace BCOBCA.Tests;

public sealed class PlatformFeeTests
{
    [Theory]
    [InlineData("0")]
    [InlineData("0.0001")]
    [InlineData("0.05")]
    [InlineData("1")]
    public void AcceptsFractionRatesWithUpToFourDecimalPlaces(string value)
        => PlatformFeeRules.Validate(decimal.Parse(value, CultureInfo.InvariantCulture));

    [Theory]
    [InlineData("-0.01")]
    [InlineData("1.0001")]
    [InlineData("0.00001")]
    public void RejectsInvalidOrOverpreciseRates(string value)
        => Assert.Throws<BusinessException>(() => PlatformFeeRules.Validate(decimal.Parse(value, CultureInfo.InvariantCulture)));

    [Fact]
    public void MissingRateIsRejectedInsteadOfSilentlySettingZero()
        => Assert.Throws<BusinessException>(() => PlatformFeeRules.Validate(null));
}
