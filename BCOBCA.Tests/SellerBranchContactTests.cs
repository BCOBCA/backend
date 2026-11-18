using BCOBCA.BLL.Common;
using BCOBCA.BLL.DTOs;
using BCOBCA.BLL.Validators;
using Xunit;

namespace BCOBCA.Tests;

public sealed class SellerBranchContactTests
{
    [Theory]
    [InlineData("0901234567", "0901234567")]
    [InlineData("+84 901 234 567", "0901234567")]
    [InlineData("84 901 234 567", "0901234567")]
    [InlineData("(028) 3822-1234", "02838221234")]
    [InlineData("+84 28 3822 1234", "02838221234")]
    public void AcceptsNormalizedVietnameseContactFormats(string input, string expected)
    {
        Assert.Equal(expected, ShippingContact.NormalizePhone(input));
        Assert.True(new SellerBranchValidator().Validate(new SellerBranchInput("Kho", expected, "12 Test", "79", "26734", DateTimeOffset.UtcNow)).IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("123456789")]
    [InlineData("+491234567890")]
    [InlineData("090123456789")]
    [InlineData("abcdef")]
    public void RejectsUnsupportedContacts(string input)
    {
        Assert.False(new SellerBranchValidator().Validate(new SellerBranchInput("Kho", ShippingContact.NormalizePhone(input), "12 Test", "79", "26734", DateTimeOffset.UtcNow)).IsValid);
    }
}
