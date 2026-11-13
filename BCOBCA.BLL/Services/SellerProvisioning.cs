using BCOBCA.BLL.Common;
using BCOBCA.DAL.Entities;
using BCOBCA.DAL.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BCOBCA.BLL.Services;

internal static class SellerProvisioning
{
    // The caller owns the transaction, including account creation and role assignment.
    internal static async Task<Store> EnsureAsync(ICommerceStore store, Account account, User user, CancellationToken ct)
    {
        var shops = await store.Set<Store>().IgnoreQueryFilters()
            .Where(s => s.OwnerAccountId == account.Id && s.StoreType == 1).OrderBy(s => s.CreatedAt).ToListAsync(ct);
        var shop = shops.FirstOrDefault(s => s.Status == 1 && s.DeletedAt == null);
        if (shop is null && shops.Count != 0)
            throw new BusinessException("Cửa hàng đã ngừng hoạt động hoặc bị khóa. Vui lòng liên hệ quản trị viên.", 409);
        var now = DateTimeOffset.UtcNow;
        if (shop is null)
        {
            shop = new Store { OwnerAccountId = account.Id, StoreType = 1, Status = 1,
                Name = "Cửa hàng " + user.FullName, Slug = "seller-" + account.Id.ToString("N"),
                Email = account.Email, Phone = account.Phone, CreatedAt = now, UpdatedAt = now };
            store.Set<Store>().Add(shop);
            store.Set<StoreSettings>().Add(new StoreSettings { StoreId = shop.Id, AllowMessages = true, UpdatedAt = now });
        }
        if (!await store.Set<AccountRole>().AnyAsync(r => r.AccountId == account.Id && r.Role == "Seller", ct))
            store.Set<AccountRole>().Add(new AccountRole { AccountId = account.Id, Role = "Seller" });
        if (account.Role == "Customer") { account.Role = "Seller"; account.UpdatedAt = now; }
        return shop;
    }
}
