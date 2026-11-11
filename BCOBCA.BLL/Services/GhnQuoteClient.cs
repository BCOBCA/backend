using System.Net.Http.Json;
using System.Text.Json;
using BCOBCA.BLL.Common;
using BCOBCA.BLL.Interfaces;
using BCOBCA.DAL.Entities;
using Microsoft.Extensions.Configuration;

namespace BCOBCA.BLL.Services;

public sealed record GhnPackage(int ShopId, int Weight, int Length, int Width, int Height, decimal MerchandiseTotal, IReadOnlyList<object> Items);

public sealed class GhnQuoteClient(HttpClient client, IConfiguration config)
{
    public const string PreviewUrl = "https://dev-online-gateway.ghn.vn/shiip/public-api/v2/shipping-order/preview";

    public async Task<(string Province, string Ward)> AddressNamesAsync(UserAddress address, CancellationToken ct)
    {
        if (!int.TryParse(address.ProvinceCode, out var province) || province is < 1 or > 99 || !int.TryParse(address.WardCode, out var ward) || ward < 1) throw new BusinessException("Vui lòng cập nhật tỉnh/thành và phường/xã của địa chỉ nhận", 409);
        using var response = await client.GetAsync($"https://provinces.open-api.vn/api/v2/p/{province}?depth=2", ct);
        if (!response.IsSuccessStatusCode) throw new BusinessException("Chưa xác minh được địa chỉ nhận hàng. Vui lòng thử lại", 503);
        var data = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        if (data.ValueKind != JsonValueKind.Object || !data.TryGetProperty("code", out var code) || code.ValueKind != JsonValueKind.Number || !code.TryGetInt32(out var supplied) || supplied != province || !data.TryGetProperty("name", out var name) || name.ValueKind != JsonValueKind.String || !data.TryGetProperty("wards", out var wards) || wards.ValueKind != JsonValueKind.Array || wards.GetArrayLength() > 5000) throw new BusinessException("Địa chỉ nhận hàng chưa được xác minh", 409);
        var match = wards.EnumerateArray().FirstOrDefault(x => x.ValueKind == JsonValueKind.Object && x.TryGetProperty("code", out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var id) && id == ward);
        if (match.ValueKind != JsonValueKind.Object || !match.TryGetProperty("name", out var wardName) || wardName.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(name.GetString()) || string.IsNullOrWhiteSpace(wardName.GetString())) throw new BusinessException("Vui lòng cập nhật phường/xã của địa chỉ nhận", 409);
        return (name.GetString()!, wardName.GetString()!);
    }

    public async Task<ShippingRate> QuoteAsync(UserAddress address, string province, string ward, GhnPackage package, CancellationToken ct)
    {
        var token = config["Shipping:Ghn:TestToken"];
        if (string.IsNullOrWhiteSpace(token)) throw new BusinessException("Chưa thể tính phí giao hàng. Vui lòng thử lại sau", 503);
        if (package.ShopId <= 0 || package.Weight is < 1 or > 50000 || package.Length is < 1 or > 200 || package.Width is < 1 or > 200 || package.Height is < 1 or > 200 || package.MerchandiseTotal is < 0 or > 50000000) throw new BusinessException("Kiện hàng chưa đủ thông tin để tính phí giao hàng", 409);
        using var request = new HttpRequestMessage(HttpMethod.Post, PreviewUrl);
        request.Headers.Add("Token", token);
        request.Headers.Add("ShopId", package.ShopId.ToString(System.Globalization.CultureInfo.InvariantCulture));
        request.Content = JsonContent.Create(new { payment_type_id = 2, required_note = "KHONGCHOXEMHANG", to_name = address.RecipientName, to_phone = address.Phone,
            to_address = address.AddressLine, to_province_name = province, to_ward_name = ward, is_new_to_address = true,
            weight = package.Weight, length = package.Length, width = package.Width, height = package.Height,
            service_type_id = package.Weight < 20000 ? 2 : 5, cod_amount = decimal.Round(package.MerchandiseTotal, 0, MidpointRounding.AwayFromZero), items = package.Items });
        try
        {
            using var response = await client.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode) throw new BusinessException("Chưa lấy được phí giao hàng cho tuyến này. Vui lòng kiểm tra địa chỉ hoặc thử lại", 503);
            var data = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
            if (data.ValueKind != JsonValueKind.Object || !data.TryGetProperty("code", out var code) || code.ValueKind != JsonValueKind.Number || !code.TryGetInt32(out var status) || status != 200 || !data.TryGetProperty("data", out var body) || body.ValueKind != JsonValueKind.Object || !body.TryGetProperty("total_fee", out var total) || total.ValueKind != JsonValueKind.Number || !total.TryGetInt32(out var fee) || fee < 0) throw new BusinessException("Phí giao hàng chưa được xác nhận. Vui lòng thử lại", 503);
            if (!body.TryGetProperty("order_code", out var orderCode) || orderCode.ValueKind != JsonValueKind.String || orderCode.GetString() != "") throw new BusinessException("Báo giá giao hàng chưa được xác nhận", 503);
            DateTimeOffset? eta = body.TryGetProperty("expected_delivery_time", out var delivery) && delivery.ValueKind == JsonValueKind.String && delivery.TryGetDateTimeOffset(out var date) ? date : null;
            return new(fee, eta);
        }
        catch (HttpRequestException) { throw new BusinessException("Không kết nối được dịch vụ giao hàng. Vui lòng thử lại", 503); }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested) { throw new BusinessException("Dịch vụ giao hàng phản hồi chậm. Vui lòng thử lại", 503); }
        catch (JsonException) { throw new BusinessException("Phí giao hàng chưa được xác nhận. Vui lòng thử lại", 503); }
    }
}
