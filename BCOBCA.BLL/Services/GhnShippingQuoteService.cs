using BCOBCA.BLL.Common;
using BCOBCA.BLL.Interfaces;
using BCOBCA.DAL.Entities;
using BCOBCA.DAL.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BCOBCA.BLL.Services;

public sealed class GhnShippingQuoteService(ICommerceStore store, GhnQuoteClient client) : IShippingQuoteService
{
    public async Task<ShippingQuoteResult> QuoteAsync(UserAddress? address, IReadOnlyList<OrderItem> lines, CancellationToken ct)
    {
        if (address is null) throw new BusinessException("Vui lòng chọn địa chỉ nhận hàng", 409);
        var storeIds = lines.Select(x => x.StoreId).Distinct().ToArray();
        var shops = await store.Set<Store>().AsNoTracking().Where(x => storeIds.Contains(x.Id) && x.StoreType == 1 && x.Status == 1).ToDictionaryAsync(x => x.Id, ct);
        var warehouseBranches = await store.Set<StoreBranch>().AsNoTracking().Where(b => storeIds.Contains(b.StoreId) && (b.IsPickup || b.IsReturn)).ToListAsync(ct);
        var pickups = warehouseBranches.Where(b => b.IsPickup).ToDictionary(b => b.StoreId);
        var returns = warehouseBranches.Where(b => b.IsReturn).ToDictionary(b => b.StoreId);
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
            GhnPickup? pickup = null;
            if (pickups.TryGetValue(group.Key, out var branch))
            {
                if (!branch.IsActive || string.IsNullOrWhiteSpace(branch.Address) || string.IsNullOrWhiteSpace(branch.Phone) || string.IsNullOrWhiteSpace(branch.ProvinceName) || string.IsNullOrWhiteSpace(branch.WardName)) throw new BusinessException("Kho lấy hàng của cửa hàng chưa đủ thông tin. Vui lòng liên hệ cửa hàng.", 409);
                pickup = new(branch.Name, branch.Phone, branch.Address, branch.ProvinceName, branch.WardName);
            }
            GhnPickup? returnAddress = null;
            if (returns.TryGetValue(group.Key, out var returnBranch))
            {
                if (!returnBranch.IsActive || string.IsNullOrWhiteSpace(returnBranch.Address) || string.IsNullOrWhiteSpace(returnBranch.Phone) || string.IsNullOrWhiteSpace(returnBranch.ProvinceName) || string.IsNullOrWhiteSpace(returnBranch.WardName)) throw new BusinessException("Địa chỉ trả hàng của cửa hàng chưa đủ thông tin. Vui lòng liên hệ cửa hàng.", 409);
                returnAddress = new(returnBranch.Name, returnBranch.Phone, returnBranch.Address, returnBranch.ProvinceName, returnBranch.WardName);
            }
            return (StoreId: group.Key, Package: new GhnPackage(shop.GhnTestShopId.Value, (int)weight, items.Max(x => x.length), items.Max(x => x.width), (int)height, group.Sum(x => x.TotalPrice), items.Cast<object>().ToArray(), pickup, returnAddress));
        }).ToArray();
        var rates = await Task.WhenAll(packages.Select(async x => (x.StoreId, Rate: await client.QuoteAsync(address, names.Province, names.Ward, x.Package, ct))));
        return new(rates.ToDictionary(x => x.StoreId, x => x.Rate), $"{address.AddressLine.Trim()}, {names.Ward}, {names.Province}");
    }
}
