using BCOBCA.BLL.Common;
using BCOBCA.BLL.DTOs;
using BCOBCA.BLL.Validators;
using BCOBCA.DAL.Entities;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace BCOBCA.BLL.Services;

public sealed partial class SellerService
{
    private async Task CheckProfileAccess(Guid accountId, Guid storeId, CancellationToken ct)
    {
        if (!await store.Set<Account>().AnyAsync(a => a.Id == accountId && a.Status == 1, ct))
            throw new BusinessException("Tài khoản không hoạt động", 403);
        await CheckAccess(accountId, storeId, ct);
    }

    public async Task<SellerStoreProfileDto> ProfileAsync(Guid accountId, Guid storeId, CancellationToken ct)
    {
        await CheckProfileAccess(accountId, storeId, ct);
        var shop = await Owned(accountId).SingleAsync(s => s.Id == storeId, ct);
        var settings = await store.Set<StoreSettings>().AsNoTracking().SingleOrDefaultAsync(s => s.StoreId == storeId, ct);
        var activeCategoryIds = CategoryTree.Active(await store.Set<Category>().AsNoTracking().ToListAsync(ct)).Select(c => c.Id).ToArray();
        var categories = await (from link in store.Set<StoreCategory>() join category in store.Set<Category>() on link.CategoryId equals category.Id
            where link.StoreId == storeId && activeCategoryIds.Contains(category.Id) orderby category.Name select new DealerCategoryDto(category.Id, category.Name)).ToListAsync(ct);
        var branches = await store.Set<StoreBranch>().AsNoTracking().Where(b => b.StoreId == storeId)
            .OrderByDescending(b => b.IsMain).ThenBy(b => b.Name).ThenBy(b => b.Id)
            .Select(b => new SellerBranchProfileDto(b.Id, b.Name, b.Phone, b.Address, b.ProvinceCode, b.WardCode, b.IsMain, b.IsActive, b.Latitude, b.Longitude)).ToListAsync(ct);
        var verification = await store.Set<StoreVerification>().AsNoTracking().Where(v => v.StoreId == storeId)
            .OrderByDescending(v => v.CreatedAt).ThenByDescending(v => v.Id)
            .Select(v => new SellerVerificationDto(v.LegalName, v.TaxCode, v.RepresentativeName, v.Status, v.RejectionReason, v.UpdatedAt)).FirstOrDefaultAsync(ct);
        return new(shop.Id, shop.Name, shop.Slug, shop.Description, shop.LogoUrl, shop.CoverUrl, shop.Email, shop.Phone,
            shop.Website, shop.Provinces, shop.Status, shop.IsVerified, shop.VerifiedAt, shop.CreatedAt, shop.UpdatedAt,
            await store.Set<StoreFollower>().CountAsync(f => f.StoreId == storeId, ct), categories, branches,
            new(settings?.AllowMessages ?? false, settings?.ShowPhone ?? false, settings?.ShowEmail ?? false, settings?.ShowBranches ?? false), verification);
    }

    public async Task<SellerStoreProfileDto> UpdateProfileAsync(Guid accountId, Guid storeId, SellerStoreProfileInput input, CancellationToken ct)
    {
        input = input with { Name = input.Name?.Trim() ?? "", Description = input.Description?.Trim() ?? "",
            LogoUrl = input.LogoUrl?.Trim() ?? "", CoverUrl = input.CoverUrl?.Trim() ?? "",
            Email = input.Email?.Trim().ToLowerInvariant() ?? "", Phone = input.Phone?.Trim() ?? "", Website = input.Website?.Trim() ?? "",
            Provinces = input.Provinces?.Trim() ?? "", MainBranch = input.MainBranch is null ? null! : input.MainBranch with {
                Name = input.MainBranch.Name?.Trim() ?? "", Phone = input.MainBranch.Phone?.Trim() ?? "", Address = input.MainBranch.Address?.Trim() ?? "" } };
        await new SellerStoreProfileValidator().ValidateAndThrowAsync(input, ct);
        await using var tx = await store.BeginTransactionAsync(ct);
        await CheckProfileAccess(accountId, storeId, ct);
        // Read the persisted timestamp: PostgreSQL stores microseconds while a tracked entity may retain .NET ticks.
        var version = await store.Set<Store>().Where(s => s.Id == storeId).Select(s => s.UpdatedAt).SingleAsync(ct);
        if (version != input.UpdatedAt) throw new BusinessException("Thông tin cửa hàng vừa thay đổi. Vui lòng tải lại trước khi lưu.", 409);
        var shop = await store.Set<Store>().SingleAsync(s => s.Id == storeId, ct);
        var provinces = input.Provinces == "" ? null : DealerProvinces.Normalize(input.Provinces);
        var ids = input.CategoryIds.Distinct().ToArray();
        var activeCategories = CategoryTree.Active(await store.Set<Category>().AsNoTracking().ToListAsync(ct)).Select(c => c.Id).ToHashSet();
        if (ids.Any(id => !activeCategories.Contains(id))) throw new BusinessException("Ngành hàng không hợp lệ hoặc đã ngừng hoạt động.");
        var main = await store.Set<StoreBranch>().SingleOrDefaultAsync(b => b.StoreId == storeId && b.IsMain, ct);
        if (main is not null && input.MainBranch.Address == "") throw new BusinessException("Vui lòng nhập địa chỉ chi nhánh chính.");
        var now = DateTimeOffset.UtcNow;
        shop.Name = input.Name; shop.Description = input.Description; shop.LogoUrl = input.LogoUrl; shop.CoverUrl = input.CoverUrl;
        shop.Email = input.Email; shop.Phone = input.Phone; shop.Website = input.Website; shop.Provinces = provinces; shop.UpdatedAt = now;
        var settings = await store.Set<StoreSettings>().SingleOrDefaultAsync(s => s.StoreId == storeId, ct);
        if (settings is null) { settings = new StoreSettings { StoreId = storeId }; store.Set<StoreSettings>().Add(settings); }
        settings.AllowMessages = input.Settings.AllowMessages; settings.ShowPhone = input.Settings.ShowPhone;
        settings.ShowEmail = input.Settings.ShowEmail; settings.ShowBranches = input.Settings.ShowBranches; settings.UpdatedAt = now;
        var links = await store.Set<StoreCategory>().Where(c => c.StoreId == storeId).ToListAsync(ct);
        store.Set<StoreCategory>().RemoveRange(links.Where(link => !ids.Contains(link.CategoryId)));
        foreach (var id in ids.Where(id => links.All(link => link.CategoryId != id))) store.Set<StoreCategory>().Add(new StoreCategory { StoreId = storeId, CategoryId = id });
        if (input.MainBranch.Address != "")
        {
            if (main is null) { main = new StoreBranch { StoreId = storeId, IsMain = true, CreatedAt = now }; store.Set<StoreBranch>().Add(main); }
            main.Name = input.MainBranch.Name == "" ? "Chi nhánh chính" : input.MainBranch.Name;
            main.Phone = input.MainBranch.Phone; main.Address = input.MainBranch.Address; main.UpdatedAt = now;
        }
        await store.SaveChangesAsync(ct);
        var result = await ProfileAsync(accountId, storeId, ct);
        await tx.CommitAsync(ct);
        return result;
    }
}
