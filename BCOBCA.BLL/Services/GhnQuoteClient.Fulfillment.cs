using System.Net.Http.Json;
using System.Text.Json;
using BCOBCA.BLL.Common;
using Microsoft.Extensions.Configuration;

namespace BCOBCA.BLL.Services;

public sealed record GhnOrderState(string OrderCode, int ShopId, string Status, bool CodCollected, decimal CodAmount);
public sealed partial class GhnQuoteClient
{
    private const string StagingOrders = "https://dev-online-gateway.ghn.vn/shiip/public-api/v2/shipping-order/";
    private async Task<JsonElement> SendOrderAsync(HttpMethod method, string operation, int shopId, string? payload, CancellationToken ct)
    {
        var token = config["Shipping:Ghn:TestToken"];
        if (string.IsNullOrWhiteSpace(token) || shopId <= 0) throw new BusinessException("Chưa cấu hình vận chuyển GHN test", 503);
        using var request = new HttpRequestMessage(method, StagingOrders + operation);
        request.Headers.Add("Token", token); request.Headers.Add("ShopId", shopId.ToString(System.Globalization.CultureInfo.InvariantCulture));
        if (payload is not null) request.Content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json");
        try
        {
            using var response = await client.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode) throw new BusinessException("GHN chưa xử lý được vận đơn. Hệ thống sẽ thử lại.", 503);
            var envelope = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
            if (!envelope.TryGetProperty("code", out var code) || code.GetInt32() != 200 || !envelope.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object)
                throw new BusinessException("Phản hồi vận đơn GHN không hợp lệ", 503);
            return data;
        }
        catch (HttpRequestException) { throw new BusinessException("Không kết nối được GHN. Hệ thống sẽ thử lại.", 503); }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested) { throw new BusinessException("GHN phản hồi chậm. Hệ thống sẽ thử lại.", 503); }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException)
        { throw new BusinessException("Phản hồi vận đơn GHN không hợp lệ", 503); }
    }

    public async Task<string> CreateShipmentAsync(int shopId, string payload, CancellationToken ct)
    {
        // Existing snapshots can contain decimal scale from PostgreSQL money columns.
        // GHN's create endpoint decodes COD into an integer and rejects 61900.00.
        try
        {
            var body = System.Text.Json.Nodes.JsonNode.Parse(payload)?.AsObject();
            if (body?["cod_amount"] is not System.Text.Json.Nodes.JsonValue value || !value.TryGetValue<decimal>(out var amount)
                || amount is < 0 or > 50000000 || decimal.Truncate(amount) != amount)
                throw new BusinessException("Tổng tiền thu COD cần là số nguyên đồng trong hạn mức GHN", 409);
            body["cod_amount"] = (int)amount;
            payload = body.ToJsonString();
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException)
        { throw new BusinessException("Dữ liệu tạo vận đơn GHN không hợp lệ", 409); }
        var data = await SendOrderAsync(HttpMethod.Post, "create", shopId, payload, ct);
        if (!data.TryGetProperty("order_code", out var code) || code.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(code.GetString()) || code.GetString()!.Length > 100)
            throw new BusinessException("GHN chưa cấp mã vận đơn hợp lệ", 503);
        return code.GetString()!;
    }

    public async Task<GhnOrderState> ShipmentStateAsync(int shopId, string code, CancellationToken ct)
    {
        var data = await SendOrderAsync(HttpMethod.Get, "detail?order_code=" + Uri.EscapeDataString(code), shopId, null, ct);
        try
        {
            var result = new GhnOrderState(data.GetProperty("order_code").GetString()!, data.GetProperty("shop_id").GetInt32(),
                data.GetProperty("status").GetString()!, data.TryGetProperty("is_cod_collected", out var collected) && collected.ValueKind == JsonValueKind.True,
                data.GetProperty("cod_amount").GetDecimal());
            if (result.OrderCode != code || result.ShopId != shopId) throw new BusinessException("Vận đơn GHN không khớp cửa hàng", 409);
            return result;
        }
        catch (Exception ex) when (ex is KeyNotFoundException or InvalidOperationException or FormatException)
        { throw new BusinessException("Phản hồi vận đơn GHN không hợp lệ", 503); }
    }
}
