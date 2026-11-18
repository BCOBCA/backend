using System.Text.RegularExpressions;

namespace BCOBCA.BLL.Common;

public static class ShippingContact
{
    public static string NormalizePhone(string? value)
    {
        var phone = Regex.Replace(value?.Trim() ?? "", @"[\s().-]", "");
        if (phone.StartsWith("+84", StringComparison.Ordinal)) return "0" + phone[3..];
        if (phone.Length is 11 or 12 && phone.StartsWith("84", StringComparison.Ordinal)) return "0" + phone[2..];
        return phone;
    }
}
