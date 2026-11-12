using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using BCOBCA.BLL.DTOs;
using Microsoft.Extensions.Configuration;

namespace BCOBCA.BLL.Common;

internal static class BankDataProtection
{
    private static byte[] Key(IConfiguration config)
    {
        try { var key = Convert.FromBase64String(config["Security:BankDataKey"] ?? ""); if (key.Length != 32) throw new FormatException(); return key; }
        catch (FormatException) { throw new BusinessException("Chưa cấu hình khóa mã hóa thông tin ngân hàng", 503); }
    }

    public static BankDetailsDto Validate(BankDetailsDto details)
    {
        var name = details.BankName?.Trim() ?? ""; var number = details.AccountNumber?.Trim() ?? ""; var holder = details.AccountHolder?.Trim() ?? "";
        if (name.Length is < 2 or > 150 || holder.Length is < 2 or > 150 || !Regex.IsMatch(number, "^[0-9]{6,34}$")) throw new BusinessException("Thông tin ngân hàng không hợp lệ");
        return new(name, number, holder);
    }

    public static string Encrypt(IConfiguration config, BankDetailsDto details)
    {
        var nonce = RandomNumberGenerator.GetBytes(12); var plain = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(details)); var encrypted = new byte[plain.Length]; var tag = new byte[16];
        using var aes = new AesGcm(Key(config), 16); aes.Encrypt(nonce, plain, encrypted, tag);
        return "v1:" + Convert.ToBase64String(nonce.Concat(tag).Concat(encrypted).ToArray());
    }

    public static BankDetailsDto Decrypt(IConfiguration config, string value)
    {
        if (!value.StartsWith("v1:", StringComparison.Ordinal)) throw new BusinessException("Không thể đọc thông tin ngân hàng", 409);
        var data = Convert.FromBase64String(value[3..]); if (data.Length <= 28) throw new BusinessException("Không thể đọc thông tin ngân hàng", 409);
        var plain = new byte[data.Length - 28]; using var aes = new AesGcm(Key(config), 16); aes.Decrypt(data.AsSpan(0, 12), data.AsSpan(28), data.AsSpan(12, 16), plain);
        return JsonSerializer.Deserialize<BankDetailsDto>(plain) ?? throw new InvalidOperationException("Invalid bank payload.");
    }
}
