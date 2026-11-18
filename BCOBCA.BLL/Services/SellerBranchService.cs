using BCOBCA.BLL.Common;
using BCOBCA.BLL.DTOs;
using BCOBCA.BLL.Validators;
using BCOBCA.DAL.Entities;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace BCOBCA.BLL.Services;

public sealed partial class SellerService
{
    private async Task<Store> BranchStoreAsync(Guid accountId, Guid storeId, DateTimeOffset updatedAt, CancellationToken ct)
    {
        await CheckProfileAccess(accountId, storeId, ct);
        // Lock before calling GHN: concurrent selections must not both register an unbound store.
        var version = await store.Set<Store>().FromSqlInterpolated($"SELECT * FROM \"Stores\" WHERE \"Id\" = {storeId} FOR UPDATE")
            .Select(s => s.UpdatedAt).SingleAsync(ct);
        if (version != updatedAt) throw new BusinessException("Thông tin cửa hàng vừa thay đổi. Vui lòng tải lại trước khi lưu.", 409);
        return await store.Set<Store>().SingleAsync(s => s.Id == storeId, ct);
    }

    private async Task<(string Province, string Ward)> BranchLocationAsync(StoreBranch branch, CancellationToken ct)
    {
        if (shipping is null) throw new BusinessException("Chưa thể xác minh địa chỉ chi nhánh. Vui lòng thử lại sau.", 503);
        try
        {
            return await shipping.AddressNamesAsync(new UserAddress { ProvinceCode = branch.ProvinceCode, WardCode = branch.WardCode }, ct);
        }
        catch (BusinessException) { throw new BusinessException("Không xác minh được tỉnh/thành và phường/xã của chi nhánh. Vui lòng kiểm tra địa chỉ.", 422); }
        catch (HttpRequestException) { throw new BusinessException("Không tải được danh sách địa phương. Vui lòng thử lại.", 503); }
        catch (System.Text.Json.JsonException) { throw new BusinessException("Không xác minh được địa chỉ chi nhánh. Vui lòng thử lại.", 503); }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested) { throw new BusinessException("Xác minh địa chỉ quá thời gian. Vui lòng thử lại.", 503); }
    }

    public async Task<SellerStoreProfileDto> SaveBranchAsync(Guid accountId, Guid storeId, Guid? branchId, SellerBranchInput input, CancellationToken ct)
    {
        input = input with { Name = input.Name?.Trim() ?? "", Phone = ShippingContact.NormalizePhone(input.Phone), Address = input.Address?.Trim() ?? "", ProvinceCode = input.ProvinceCode?.Trim() ?? "", WardCode = input.WardCode?.Trim() ?? "" };
        await new SellerBranchValidator().ValidateAndThrowAsync(input, ct);
        await using var tx = await store.BeginTransactionAsync(ct);
        var shop = await BranchStoreAsync(accountId, storeId, input.UpdatedAt, ct);
        var branch = branchId.HasValue
            ? await store.Set<StoreBranch>().SingleOrDefaultAsync(b => b.Id == branchId && b.StoreId == storeId && b.IsActive, ct)
                ?? throw new BusinessException("Không tìm thấy chi nhánh đang hoạt động của cửa hàng.", 404)
            : new StoreBranch { StoreId = storeId };
        // Verify before mutating a tracked branch, so a rejected address leaves it unchanged.
        var names = await BranchLocationAsync(new StoreBranch { ProvinceCode = input.ProvinceCode, WardCode = input.WardCode }, ct);
        if (branch.IsPickup || input.SetAsPickup)
            await EnsurePickupShippingAsync(shop, new(input.Name, input.Phone, input.Address, names.Province, names.Ward), ct);
        if (input.SetAsPickup || input.SetAsReturn)
        {
            var previous = await store.Set<StoreBranch>().Where(b => b.StoreId == storeId && b.Id != branch.Id &&
                ((input.SetAsPickup && b.IsPickup) || (input.SetAsReturn && b.IsReturn))).ToListAsync(ct);
            foreach (var value in previous)
            {
                if (input.SetAsPickup) value.IsPickup = false;
                if (input.SetAsReturn) value.IsReturn = false;
                value.UpdatedAt = DateTimeOffset.UtcNow;
            }
            // Clear previous flags before setting the new ones; both saves share this transaction.
            if (previous.Count > 0) await store.SaveChangesAsync(ct);
        }
        if (branch.Address != input.Address || branch.ProvinceCode != input.ProvinceCode || branch.WardCode != input.WardCode)
        {
            branch.Latitude = null; branch.Longitude = null;
        }
        branch.Name = input.Name; branch.Phone = input.Phone; branch.Address = input.Address;
        branch.ProvinceCode = input.ProvinceCode; branch.WardCode = input.WardCode;
        branch.ProvinceName = names.Province; branch.WardName = names.Ward;
        if (input.SetAsPickup) branch.IsPickup = true;
        if (input.SetAsReturn) branch.IsReturn = true;
        branch.UpdatedAt = shop.UpdatedAt = DateTimeOffset.UtcNow;
        if (!branchId.HasValue) store.Set<StoreBranch>().Add(branch);
        await store.SaveChangesAsync(ct);
        var result = await ProfileAsync(accountId, storeId, ct);
        await tx.CommitAsync(ct);
        return result;
    }

    public Task<SellerStoreProfileDto> SelectPickupBranchAsync(Guid accountId, Guid storeId, SellerPickupBranchInput input, CancellationToken ct)
        => SelectWarehouseBranchAsync(accountId, storeId, input, false, ct);

    public async Task<SellerStoreProfileDto> SelectMainBranchAsync(Guid accountId, Guid storeId, SellerPickupBranchInput input, CancellationToken ct)
    {
        if (input.BranchId == Guid.Empty || input.UpdatedAt == default) throw new BusinessException("Vui lòng chọn chi nhánh chính.");
        await using var tx = await store.BeginTransactionAsync(ct);
        var shop = await BranchStoreAsync(accountId, storeId, input.UpdatedAt, ct);
        var branch = await store.Set<StoreBranch>().SingleOrDefaultAsync(b => b.Id == input.BranchId && b.StoreId == storeId && b.IsActive, ct)
            ?? throw new BusinessException("Không tìm thấy chi nhánh đang hoạt động của cửa hàng.", 404);
        var previous = await store.Set<StoreBranch>().Where(b => b.StoreId == storeId && b.IsMain && b.Id != branch.Id).ToListAsync(ct);
        var now = DateTimeOffset.UtcNow;
        foreach (var value in previous) { value.IsMain = false; value.UpdatedAt = now; }
        // Clear the old main before setting the new one to respect the immediate unique index.
        if (previous.Count > 0) await store.SaveChangesAsync(ct);
        branch.IsMain = true;
        branch.UpdatedAt = shop.UpdatedAt = now;
        await store.SaveChangesAsync(ct);
        var result = await ProfileAsync(accountId, storeId, ct);
        await tx.CommitAsync(ct);
        return result;
    }

    public Task<SellerStoreProfileDto> SelectReturnBranchAsync(Guid accountId, Guid storeId, SellerPickupBranchInput input, CancellationToken ct)
        => SelectWarehouseBranchAsync(accountId, storeId, input, true, ct);

    private async Task<SellerStoreProfileDto> SelectWarehouseBranchAsync(Guid accountId, Guid storeId, SellerPickupBranchInput input, bool returns, CancellationToken ct)
    {
        if (input.BranchId == Guid.Empty || input.UpdatedAt == default) throw new BusinessException("Vui lòng chọn chi nhánh làm kho lấy hàng.");
        await using var tx = await store.BeginTransactionAsync(ct);
        var shop = await BranchStoreAsync(accountId, storeId, input.UpdatedAt, ct);
        var branch = await store.Set<StoreBranch>().SingleOrDefaultAsync(b => b.Id == input.BranchId && b.StoreId == storeId && b.IsActive, ct)
            ?? throw new BusinessException("Không tìm thấy chi nhánh đang hoạt động của cửa hàng.", 404);
        var phone = ShippingContact.NormalizePhone(branch.Phone);
        await new SellerBranchValidator().ValidateAndThrowAsync(new(branch.Name, phone, branch.Address, branch.ProvinceCode, branch.WardCode, input.UpdatedAt), ct);
        var names = await BranchLocationAsync(branch, ct);
        if (!returns)
            await EnsurePickupShippingAsync(shop, new(branch.Name, phone, branch.Address, names.Province, names.Ward), ct);
        var previous = await store.Set<StoreBranch>().Where(b => b.StoreId == storeId && (returns ? b.IsReturn : b.IsPickup) && b.Id != branch.Id).ToListAsync(ct);
        foreach (var value in previous) { if (returns) value.IsReturn = false; else value.IsPickup = false; value.UpdatedAt = DateTimeOffset.UtcNow; }
        // Flush the previous flag before setting a new one; the unique index is immediate.
        if (previous.Count > 0) await store.SaveChangesAsync(ct);
        if (returns) branch.IsReturn = true; else branch.IsPickup = true;
        branch.Phone = phone;
        branch.ProvinceName = names.Province; branch.WardName = names.Ward;
        branch.UpdatedAt = shop.UpdatedAt = DateTimeOffset.UtcNow;
        await store.SaveChangesAsync(ct);
        var result = await ProfileAsync(accountId, storeId, ct);
        await tx.CommitAsync(ct);
        return result;
    }

    private async Task EnsurePickupShippingAsync(Store shop, GhnPickup pickup, CancellationToken ct)
    {
        if (shop.GhnTestShopId is > 0) return;
        if (shipping is null) throw new BusinessException("Chưa thể kích hoạt giao hàng GHN. Vui lòng lưu lại kho lấy hàng để thử lại.", 503);
        shop.GhnTestShopId = await shipping.GetOrRegisterShopAsync(shop.Id, pickup, ct);
    }
}
