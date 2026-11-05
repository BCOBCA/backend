using System.Text.Json;
using BCOBCA.BLL.Common;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace BCOBCA.Tests;

public sealed class AffiliatePolicyTests
{
    private static IConfiguration Config(Dictionary<string, string?>? values = null)
        => new ConfigurationBuilder().AddInMemoryCollection(values ?? []).Build();

    [Fact]
    public void MissingPublicationDataRemainsDraftAndUnknownTermsRemainNull()
    {
        var policy = AffiliatePolicyConfiguration.Read(Config());
        Assert.Equal("DRAFT", policy.ApprovalStatus);
        Assert.Null(policy.UpdatedOn); Assert.Null(policy.EffectiveOn);
        Assert.Null(policy.AttributionDays); Assert.Null(policy.ApprovalHoldDays);
        Assert.Null(policy.WithdrawalFee); Assert.Null(policy.MinimumWithdrawal);
        Assert.Null(policy.PaymentTime); Assert.Null(policy.AttributionRule);
    }

    [Fact]
    public void ReadsOnlyExplicitValidConfigurationAndNeverLeaksSecrets()
    {
        var policy = AffiliatePolicyConfiguration.Read(Config(new() {
            ["Affiliate:AttributionDays"] = "21", ["Affiliate:ApprovalHoldDays"] = "0",
            ["Affiliate:Policy:UpdatedOn"] = "2030-01-03", ["Affiliate:Policy:EffectiveOn"] = "2030-01-05",
            ["Affiliate:Policy:ComplaintEmail"] = "support@example.test", ["Jwt:SigningKey"] = "never-public-secret",
            ["Affiliate:Policy:WithdrawalFee"] = "5000",
        }));
        Assert.Equal(21, policy.AttributionDays); Assert.Equal(0, policy.ApprovalHoldDays);
        Assert.Equal(new DateOnly(2030, 1, 3), policy.UpdatedOn);
        Assert.Equal("support@example.test", policy.ComplaintEmail);
        Assert.Null(policy.WithdrawalFee); // No fee configuration is implemented by the withdrawal engine.
        Assert.DoesNotContain("never-public-secret", JsonSerializer.Serialize(policy));
        Assert.Equal("DRAFT", policy.ApprovalStatus);
    }

    [Theory]
    [InlineData("", "2030-01-04", "2030-01-03", "2030-01-05")]
    [InlineData("v1", "", "2030-01-03", "2030-01-05")]
    [InlineData("v1", "2030-01-04", "invalid", "2030-01-05")]
    [InlineData("v1", "2030-01-04", "2030-01-03", "2030-01-02")]
    public void ApprovalFlagAloneCannotPublishIncompleteMetadata(string version, string approved, string updated, string effective)
    {
        var policy = AffiliatePolicyConfiguration.Read(Config(new() {
            ["Affiliate:Policy:Approved"] = "true", ["Affiliate:Policy:Version"] = version,
            ["Affiliate:Policy:ApprovedOn"] = approved, ["Affiliate:Policy:UpdatedOn"] = updated,
            ["Affiliate:Policy:EffectiveOn"] = effective,
        }));
        Assert.Equal("DRAFT", policy.ApprovalStatus);
    }

    [Fact]
    public void ApprovalRequiresCompleteChronologicalMetadata()
    {
        var policy = AffiliatePolicyConfiguration.Read(Config(new() {
            ["Affiliate:Policy:Approved"] = "true", ["Affiliate:Policy:Version"] = "v1",
            ["Affiliate:Policy:UpdatedOn"] = "2030-01-03", ["Affiliate:Policy:ApprovedOn"] = "2030-01-04",
            ["Affiliate:Policy:EffectiveOn"] = "2030-01-05",
        }));
        Assert.Equal("APPROVED", policy.ApprovalStatus);
        Assert.Equal("v1", policy.Version);
    }

    [Fact]
    public void InvalidOrUnsupportedValuesAreNotPublished()
    {
        var policy = AffiliatePolicyConfiguration.Read(Config(new() {
            ["Affiliate:AttributionDays"] = "91", ["Affiliate:ApprovalHoldDays"] = "-1",
            ["Affiliate:Policy:ComplaintEmail"] = "javascript:alert(1)",
            ["Affiliate:Policy:UpdatedOn"] = "2030-02-30",
        }));
        Assert.Null(policy.AttributionDays); Assert.Null(policy.ApprovalHoldDays);
        Assert.Null(policy.ComplaintEmail); Assert.Null(policy.UpdatedOn);
    }
}
