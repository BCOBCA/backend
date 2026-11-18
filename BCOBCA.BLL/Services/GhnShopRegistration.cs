using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BCOBCA.BLL.Common;

namespace BCOBCA.BLL.Services;

public sealed partial class GhnQuoteClient
{
    private const string SandboxApi = "https://dev-online-gateway.ghn.vn/shiip/public-api/";
    private const string RegistrationUnavailable = "Chưa thể kích hoạt giao hàng GHN. Vui lòng lưu lại kho lấy hàng để thử lại.";

    public async Task<int> GetOrRegisterShopAsync(Guid storeId, GhnPickup pickup, CancellationToken ct)
    {
        var token = config["Shipping:Ghn:TestToken"];
        if (string.IsNullOrWhiteSpace(token)) throw new BusinessException(RegistrationUnavailable, 503);
        if (storeId == Guid.Empty || new[] { pickup.Name, pickup.Phone, pickup.Address, pickup.Province, pickup.Ward }.Any(string.IsNullOrWhiteSpace))
            throw new BusinessException("Vui lòng điền đủ thông tin kho lấy hàng trước khi kích hoạt GHN.", 422);
        // The remote identity survives a lost response or a failed local transaction.
        var phone = ShopPhone(pickup.Phone);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { phone, pickup.Address, pickup.Province, pickup.Ward }))));
        var name = $"BCA-{storeId:N}-{hash[..12]}";
        try
        {
            var shops = await RegisteredShopsAsync(token, ct);
            var bootstrap = shops.FirstOrDefault(s => ShopInt(s, "status") == 1 && ShopInt(s, "_id") > 0);
            var bootstrapId = ShopInt(bootstrap, "_id");
            if (bootstrapId <= 0) throw new BusinessException(RegistrationUnavailable, 503);
            var provinces = await ShopRequestAsync("v3/master-data/province/all?offset=0&limit=200", token, bootstrapId, null, ct);
            var province = MatchLocation(provinces, pickup.Province);
            var provinceId = ShopInt(province, "_id");
            var wards = await ShopRequestAsync($"v3/master-data/ward/all-by-province-id?province_id={provinceId}&offset=0&limit=200", token, bootstrapId, null, ct);
            var ward = MatchLocation(wards, pickup.Ward);
            var wardId = ShopInt(ward, "_id");
            if (ShopInt(ward, "parent_id") != provinceId) throw new BusinessException(RegistrationUnavailable, 503);
            var matches = shops.Where(s => ShopString(s, "name") == name).ToArray();
            if (matches.Length > 1) throw new BusinessException(RegistrationUnavailable, 503);
            if (matches.Length == 1) return VerifiedShop(matches[0], pickup.Address, phone, provinceId, wardId);

            var created = await ShopRequestAsync("v2/shop/register", token, bootstrapId,
                new { name, phone, address = pickup.Address, address_v2 = pickup.Address, is_new_address = true, province_id_v2 = provinceId, ward_id_v2 = wardId }, ct);
            var id = ShopInt(created, "shop_id");
            if (id <= 0) throw new BusinessException(RegistrationUnavailable, 503);
            // Read back the provider's routing before binding; a successful HTTP response alone is insufficient.
            var registered = (await RegisteredShopsAsync(token, ct)).Where(s => ShopInt(s, "_id") == id && ShopString(s, "name") == name).ToArray();
            if (registered.Length != 1) throw new BusinessException(RegistrationUnavailable, 503);
            return VerifiedShop(registered[0], pickup.Address, phone, provinceId, wardId);
        }
        catch (HttpRequestException) { throw new BusinessException(RegistrationUnavailable, 503); }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested) { throw new BusinessException(RegistrationUnavailable, 503); }
        catch (JsonException) { throw new BusinessException(RegistrationUnavailable, 503); }
    }

    private async Task<List<JsonElement>> RegisteredShopsAsync(string token, CancellationToken ct)
    {
        var shops = new List<JsonElement>();
        var offset = 0;
        for (var page = 0; page < 100; page++)
        {
            var data = await ShopRequestAsync($"v2/shop/all?offset={offset}&limit=100", token, null, null, ct);
            if (data.ValueKind != JsonValueKind.Object || !data.TryGetProperty("shops", out var items) || items.ValueKind != JsonValueKind.Array || items.GetArrayLength() > 100)
                throw new BusinessException(RegistrationUnavailable, 503);
            shops.AddRange(items.EnumerateArray());
            if (items.GetArrayLength() < 100) return shops;
            var next = ShopInt(data, "last_offset");
            if (next <= offset) throw new BusinessException(RegistrationUnavailable, 503);
            offset = next;
        }
        // Never register after an incomplete lookup: an existing shop may be on the next page.
        throw new BusinessException(RegistrationUnavailable, 503);
    }

    private async Task<JsonElement> ShopRequestAsync(string path, string token, int? shopId, object? body, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(body is null ? HttpMethod.Get : HttpMethod.Post, SandboxApi + path);
        request.Headers.Add("Token", token);
        if (shopId is { } id) request.Headers.Add("ShopId", id.ToString(CultureInfo.InvariantCulture));
        if (body is not null) request.Content = JsonContent.Create(body);
        using var response = await client.SendAsync(request, ct);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        if (!response.IsSuccessStatusCode || ShopInt(json, "code") != 200)
        {
            if (response.StatusCode == HttpStatusCode.BadRequest)
            {
                if (ShopString(json, "code_message") == "PHONE_INVALID")
                    throw new BusinessException("Số điện thoại kho lấy hàng không hợp lệ với GHN. Vui lòng cập nhật rồi thử lại.", 422);
                if (ShopString(json, "code_message") == "WARD_IS_INVALID")
                    throw new BusinessException("GHN chưa chấp nhận địa chỉ kho lấy hàng. Vui lòng kiểm tra tỉnh/thành và phường/xã.", 422);
            }
            throw new BusinessException(RegistrationUnavailable, 503);
        }
        if (!json.TryGetProperty("data", out var data) || data.ValueKind is not (JsonValueKind.Object or JsonValueKind.Array))
            throw new BusinessException(RegistrationUnavailable, 503);
        return data;
    }

    private static JsonElement MatchLocation(JsonElement data, string name)
    {
        if (data.ValueKind != JsonValueKind.Array || data.GetArrayLength() > 200) throw new BusinessException(RegistrationUnavailable, 503);
        var normalized = LocationName(name);
        var matches = data.EnumerateArray().Where(item => ShopInt(item, "status") == 1 && ShopInt(item, "_id") > 0 &&
            (LocationName(ShopString(item, "name")) == normalized ||
             (item.TryGetProperty("extension_names", out var aliases) && aliases.ValueKind == JsonValueKind.Array &&
              aliases.EnumerateArray().Any(a => a.ValueKind == JsonValueKind.String && LocationName(a.GetString()!) == normalized)))).ToArray();
        if (matches.Length != 1) throw new BusinessException("Không đối chiếu được địa chỉ kho lấy hàng với GHN. Vui lòng kiểm tra tỉnh/thành và phường/xã.", 422);
        return matches[0];
    }

    private static int VerifiedShop(JsonElement shop, string address, string phone, int provinceId, int wardId)
    {
        var registeredAddress = ShopString(shop, "address_v2");
        if (string.IsNullOrWhiteSpace(registeredAddress)) registeredAddress = ShopString(shop, "address");
        if (ShopInt(shop, "_id") <= 0 || ShopInt(shop, "status") != 1 || registeredAddress.Trim() != address.Trim() ||
            ShopPhone(ShopString(shop, "phone")) != phone || ShopInt(shop, "province_id_v2") != provinceId || ShopInt(shop, "ward_id_v2") != wardId ||
            !shop.TryGetProperty("is_new_address", out var flag) || flag.ValueKind != JsonValueKind.True)
            throw new BusinessException(RegistrationUnavailable, 503);
        return ShopInt(shop, "_id");
    }

    private static int ShopInt(JsonElement item, string key) => item.ValueKind == JsonValueKind.Object && item.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var id) ? id : 0;
    private static string ShopString(JsonElement item, string key) => item.ValueKind == JsonValueKind.Object && item.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString()! : "";
    private static string ShopPhone(string phone)
    {
        var digits = phone.Trim().TrimStart('+');
        return digits.Length == 11 && digits.StartsWith("84", StringComparison.Ordinal) ? "0" + digits[2..] : digits;
    }
    private static string LocationName(string name) => string.Concat(name.Normalize(NormalizationForm.FormD)
        .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark && char.IsLetterOrDigit(c))).ToLowerInvariant().Replace('đ', 'd');
}
