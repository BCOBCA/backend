using BCOBCA.BLL.Common;
using BCOBCA.BLL.Interfaces;
using BCOBCA.DAL.Entities;
using BCOBCA.DAL.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BCOBCA.BLL.Services;

public sealed class GhnShippingQuoteService(ICommerceStore store, GhnQuoteClient client) : IShippingQuoteService
{
    public async Task<IReadOnlyDictionary<Guid, ShippingRate>> QuoteAsync(UserAddress? address, IReadOnlyList<OrderItem> lines, CancellationToken ct)
    {
        if (address is null) throw new BusinessException("Vui lòng chọn địa chỉ nhận hàng", 409);
        var storeIds = lines.Select(x => x.StoreId).Distinct().ToArray();
        var shops = await store.Set<Store>().AsNoTracking().Where(x => storeIds.Contains(x.Id) && x.StoreType == 1 && x.Status == 1).ToDictionaryAsync(x => x.Id, ct);
        var productIds = lines.Select(x => x.ProductId).ToArray();
        var products = await store.Set<Product>().AsNoTracking().Where(x => productIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);
        var names = await client.AddressNamesAsync(address, ct);
        var packages = lines.GroupBy(x => x.StoreId ?? throw new BusinessException("Sản phẩm chưa có cửa hàng", 409)).Select(group => {
            if (!shops.TryGetValue(group.Key, out var shop) || shop.GhnTestShopId is not > 0) throw new BusinessException("Cửa hàng chưa sẵn sàng giao hàng. Vui lòng thử lại sau", 409);
            var items = group.Select(line => {
                if (!products.TryGetValue(line.ProductId, out var product) || product.ShippingWeightGrams is not > 0 || product.ShippingLengthCm is not > 0 || product.ShippingWidthCm is not > 0 || product.ShippingHeightCm is not > 0) throw new BusinessException("Cửa hàng chưa cập nhật thông tin vận chuyển của sản phẩm", 409);
                return new { name = product.Name, quantity = line.Quantity, weight = product.ShippingWeightGrams.Value, length = product.ShippingLengthCm.Value, width = product.ShippingWidthCm.Value, height = product.ShippingHeightCm.Value };
            }).ToArray();
            var weight = items.Sum(x => (long)x.weight * x.quantity); var height = items.Sum(x => (long)x.height * x.quantity);
            if (weight > 50000 || height > 200) throw new BusinessException("Kiện hàng vượt kích thước giao hàng được hỗ trợ", 409);
            return (StoreId: group.Key, Package: new GhnPackage(shop.GhnTestShopId.Value, (int)weight, items.Max(x => x.length), items.Max(x => x.width), (int)height, group.Sum(x => x.TotalPrice), items.Cast<object>().ToArray()));
        }).ToArray();
        var rates = await Task.WhenAll(packages.Select(async x => (x.StoreId, Rate: await client.QuoteAsync(address, names.Province, names.Ward, x.Package, ct))));
        return rates.ToDictionary(x => x.StoreId, x => x.Rate);
    }
}
