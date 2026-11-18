using BCOBCA.DAL.Entities;

namespace BCOBCA.BLL.Interfaces;

public sealed record GhnShippingDraft(int ShopId, string RequestJson);
public sealed record ShippingRate(decimal Fee, DateTimeOffset? ExpectedDeliveryAt = null, GhnShippingDraft? Draft = null);
public sealed record ShippingQuoteResult(IReadOnlyDictionary<Guid, ShippingRate> Rates, string ShippingAddress);
public interface IShippingQuoteService
{
    Task<ShippingQuoteResult> QuoteAsync(UserAddress? address, IReadOnlyList<OrderItem> lines, CancellationToken ct);
}
