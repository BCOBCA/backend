using BCOBCA.BLL.Interfaces;
using BCOBCA.BLL.Services;
using BCOBCA.DAL.Entities;
using BCOBCA.DAL.Interfaces;
using Microsoft.Extensions.Configuration;

namespace BCOBCA.Tests;

internal static class TestOrders
{
    public static OrderService Create(ICommerceStore store, IConfiguration config, IFinanceService finance) => new(store, config, finance, new TestShipping(config));

    private sealed class TestShipping(IConfiguration config) : IShippingQuoteService
    {
        public Task<ShippingQuoteResult> QuoteAsync(UserAddress? address, IReadOnlyList<OrderItem> lines, CancellationToken ct)
        {
            var fee = config.GetValue<decimal>("Checkout:ShippingFee");
            if (fee < 0) throw new InvalidOperationException("Test shipping fee must not be negative.");
            IReadOnlyDictionary<Guid, ShippingRate> rates = lines.Select(x => x.StoreId!.Value).Distinct().ToDictionary(id => id, _ => new ShippingRate(fee));
            return Task.FromResult(new ShippingQuoteResult(rates, $"{address?.AddressLine ?? "Test address"}, Phường kiểm thử, Thành phố Hồ Chí Minh"));
        }
    }
}
