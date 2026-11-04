using BCOBCA.BLL.DTOs;
using BCOBCA.BLL.Validators;
using BCOBCA.DAL.Context;
using BCOBCA.DAL.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BCOBCA.Tests;

public sealed class RegistrationTests
{
    [Theory]
    [InlineData("test@example.com", "", true)]
    [InlineData("", "0901234567", true)]
    [InlineData("", "+84901234567", true)]
    [InlineData("", "", false)]
    [InlineData("invalid", "0901234567", false)]
    [InlineData("", "123", false)]
    [InlineData("", "abc123456789", false)]
    public void RegistrationRequiresAValidEmailOrPhone(string email, string phone, bool valid)
    {
        var result = new RegisterValidator().Validate(new RegisterRequest(email, "StrongPassword123", "Nguyễn An", phone));
        Assert.Equal(valid, result.IsValid);
    }

    [Fact]
    public void PhoneRegistrationStillRequiresAStrongPassword()
        => Assert.False(new RegisterValidator().Validate(new RegisterRequest("", "weak", "Nguyễn An", "0901234567")).IsValid);

    [Fact]
    public void MissingEmailDoesNotConflictBetweenPhoneAccounts()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=localhost;Database=model_only").Options);
        var emailIndex = db.Model.FindEntityType(typeof(Account))!.GetIndexes()
            .Single(index => index.Properties.SingleOrDefault()?.Name == nameof(Account.Email));
        Assert.True(emailIndex.IsUnique);
        Assert.Equal("\"Email\" <> ''", emailIndex.GetFilter());
    }
}
