using System.Globalization;
using System.Net.Mail;
using BCOBCA.BLL.DTOs;
using Microsoft.Extensions.Configuration;

namespace BCOBCA.BLL.Common;

public static class AffiliatePolicyConfiguration
{
    public static AffiliatePolicyDto Read(IConfiguration config)
    {
        string? Text(string key, int maxLength)
        {
            var value = config["Affiliate:Policy:" + key]?.Trim();
            return string.IsNullOrEmpty(value) || value.Length > maxLength ? null : value;
        }
        DateOnly? Date(string key) => DateOnly.TryParseExact(Text(key, 10), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? date : null;
        int? Number(string key, int min, int max) => int.TryParse(config[key], NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) && value >= min && value <= max ? value : null;

        var version = Text("Version", 80);
        var updated = Date("UpdatedOn"); var effective = Date("EffectiveOn"); var approved = Date("ApprovedOn");
        var isApproved = bool.TryParse(config["Affiliate:Policy:Approved"], out var flag) && flag
            && version is not null && updated is not null && approved is not null && effective is not null
            && updated <= approved && approved <= effective;
        var email = Text("ComplaintEmail", 254);
        if (email is not null && (!MailAddress.TryCreate(email, out var address) || address.Address != email)) email = null;

        // Publish an allowlist only. Unimplemented payout/priority settings cannot be made promises by metadata.
        return new(isApproved ? "APPROVED" : "DRAFT", version, updated, effective, approved,
            Number("Affiliate:AttributionDays", 1, 90), Number("Affiliate:ApprovalHoldDays", 0, int.MaxValue),
            null, null, null, null, email, null, Text("OperatorName", 200), Text("OperatorAddress", 500));
    }
}
