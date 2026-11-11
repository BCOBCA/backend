using BCOBCA.DAL.Entities;

namespace BCOBCA.BLL.Interfaces;

public sealed record ShippingRate(decimal Fee, DateTimeOffset? ExpectedDeliveryAt = null);
public sealed record ShippingQuoteResult(IReadOnlyDictionary<Guid, ShippingRate> Rates, string ShippingAddress);
public interface IShippingQuoteService
{
    Task<ShippingQuoteResult> QuoteAsync(UserAddress? address, IReadOnlyList<OrderItem> lines, CancellationToken ct);
}
