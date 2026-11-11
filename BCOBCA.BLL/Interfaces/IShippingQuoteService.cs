using BCOBCA.DAL.Entities;

namespace BCOBCA.BLL.Interfaces;

public sealed record ShippingRate(decimal Fee, DateTimeOffset? ExpectedDeliveryAt = null);
public interface IShippingQuoteService
{
    Task<IReadOnlyDictionary<Guid, ShippingRate>> QuoteAsync(UserAddress? address, IReadOnlyList<OrderItem> lines, CancellationToken ct);
}
