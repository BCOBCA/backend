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
        var version = await store.Set<Store>().Where(s => s.Id == storeId).Select(s => s.UpdatedAt).SingleAsync(ct);
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
        input = input with { Name = input.Name?.Trim() ?? "", Phone = input.Phone?.Trim() ?? "", Address = input.Address?.Trim() ?? "", ProvinceCode = input.ProvinceCode?.Trim() ?? "", WardCode = input.WardCode?.Trim() ?? "" };
        await new SellerBranchValidator().ValidateAndThrowAsync(input, ct);
        await using var tx = await store.BeginTransactionAsync(ct);
        var shop = await BranchStoreAsync(accountId, storeId, input.UpdatedAt, ct);
        var branch = branchId.HasValue
            ? await store.Set<StoreBranch>().SingleOrDefaultAsync(b => b.Id == branchId && b.StoreId == storeId && b.IsActive, ct)
                ?? throw new BusinessException("Không tìm thấy chi nhánh đang hoạt động của cửa hàng.", 404)
            : new StoreBranch { StoreId = storeId, IsMain = !await store.Set<StoreBranch>().AnyAsync(b => b.StoreId == storeId && b.IsMain, ct) };
        // Verify before mutating a tracked branch, so a rejected address leaves it unchanged.
        var names = await BranchLocationAsync(new StoreBranch { ProvinceCode = input.ProvinceCode, WardCode = input.WardCode }, ct);
        if (branch.Address != input.Address || branch.ProvinceCode != input.ProvinceCode || branch.WardCode != input.WardCode)
        {
            branch.Latitude = null; branch.Longitude = null;
        }
        branch.Name = input.Name; branch.Phone = input.Phone; branch.Address = input.Address;
        branch.ProvinceCode = input.ProvinceCode; branch.WardCode = input.WardCode;
        branch.ProvinceName = names.Province; branch.WardName = names.Ward;
        branch.UpdatedAt = shop.UpdatedAt = DateTimeOffset.UtcNow;
        if (!branchId.HasValue) store.Set<StoreBranch>().Add(branch);
        await store.SaveChangesAsync(ct);
        var result = await ProfileAsync(accountId, storeId, ct);
        await tx.CommitAsync(ct);
        return result;
    }

    public Task<SellerStoreProfileDto> SelectPickupBranchAsync(Guid accountId, Guid storeId, SellerPickupBranchInput input, CancellationToken ct)
        => SelectWarehouseBranchAsync(accountId, storeId, input, false, ct);

    public Task<SellerStoreProfileDto> SelectReturnBranchAsync(Guid accountId, Guid storeId, SellerPickupBranchInput input, CancellationToken ct)
        => SelectWarehouseBranchAsync(accountId, storeId, input, true, ct);

    private async Task<SellerStoreProfileDto> SelectWarehouseBranchAsync(Guid accountId, Guid storeId, SellerPickupBranchInput input, bool returns, CancellationToken ct)
    {
        if (input.BranchId == Guid.Empty || input.UpdatedAt == default) throw new BusinessException("Vui lòng chọn chi nhánh làm kho lấy hàng.");
        await using var tx = await store.BeginTransactionAsync(ct);
        var shop = await BranchStoreAsync(accountId, storeId, input.UpdatedAt, ct);
        var branch = await store.Set<StoreBranch>().SingleOrDefaultAsync(b => b.Id == input.BranchId && b.StoreId == storeId && b.IsActive, ct)
            ?? throw new BusinessException("Không tìm thấy chi nhánh đang hoạt động của cửa hàng.", 404);
        await new SellerBranchValidator().ValidateAndThrowAsync(new(branch.Name, branch.Phone, branch.Address, branch.ProvinceCode, branch.WardCode, input.UpdatedAt), ct);
        var names = await BranchLocationAsync(branch, ct);
        var previous = await store.Set<StoreBranch>().Where(b => b.StoreId == storeId && (returns ? b.IsReturn : b.IsPickup) && b.Id != branch.Id).ToListAsync(ct);
        foreach (var value in previous) { if (returns) value.IsReturn = false; else value.IsPickup = false; value.UpdatedAt = DateTimeOffset.UtcNow; }
        // Flush the previous flag before setting a new one; the unique index is immediate.
        if (previous.Count > 0) await store.SaveChangesAsync(ct);
        if (returns) branch.IsReturn = true; else branch.IsPickup = true;
        branch.ProvinceName = names.Province; branch.WardName = names.Ward;
        branch.UpdatedAt = shop.UpdatedAt = DateTimeOffset.UtcNow;
        await store.SaveChangesAsync(ct);
        var result = await ProfileAsync(accountId, storeId, ct);
        await tx.CommitAsync(ct);
        return result;
    }
}
