using System.Text.RegularExpressions;
using BCOBCA.BLL.Common;
using BCOBCA.BLL.DTOs;
using BCOBCA.BLL.Interfaces;
using BCOBCA.DAL.Entities;
using BCOBCA.DAL.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BCOBCA.BLL.Services;

public sealed class VoucherManagementService(ICommerceStore store) : IVoucherManagementService
{
    public async Task<IReadOnlyList<VoucherStoreOptionDto>> StoresAsync(Guid accountId, CancellationToken ct)
    {
        await Scope(accountId, null, true, ct);
        return await store.Set<Store>().AsNoTracking().Where(x => x.DeletedAt == null && store.Set<Voucher>().Any(v => v.StoreId == x.Id))
            .OrderBy(x => x.Name).ThenBy(x => x.Id).Select(x => new VoucherStoreOptionDto(x.Id, x.Name)).ToListAsync(ct);
    }
    private async Task<IQueryable<Voucher>> Scope(Guid accountId, Guid? storeId, bool admin, CancellationToken ct)
    {
        if (admin)
        {
            if (!await store.Set<AccountRole>().AnyAsync(x => x.AccountId == accountId && (x.Role == "Admin" || x.Role == "SuperAdmin"), ct))
                throw new BusinessException("Không có quyền quản trị", 403);
        }
        else if (storeId is null || !await store.Set<Store>().AnyAsync(x => x.Id == storeId && x.OwnerAccountId == accountId && x.StoreType == 1 && x.Status == 1 && x.DeletedAt == null, ct))
            throw new BusinessException("Không tìm thấy cửa hàng thuộc tài khoản của bạn", 404);
        return store.Set<Voucher>().AsNoTracking().Where(x => storeId == null || x.StoreId == storeId);
    }

    private sealed class Row
    {
        public Voucher Voucher { get; init; } = null!;
        public string StoreName { get; init; } = "";
        public int Used { get; init; }
        public int Claimed { get; init; }
    }
    private IQueryable<Row> Rows(IQueryable<Voucher> source) => source.Select(v => new Row {
        Voucher = v, StoreName = store.Set<Store>().Where(s => s.Id == v.StoreId).Select(s => s.Name).FirstOrDefault() ?? "Nền tảng",
        Used = store.Set<VoucherUsage>().Count(u => u.VoucherId == v.Id && u.Status == "APPLIED"),
        Claimed = store.Set<UserVoucher>().Count(u => u.VoucherId == v.Id),
    });
    private async Task<ManagedVoucherDto[]> Map(IReadOnlyList<Row> rows, CancellationToken ct)
    {
        var ids = rows.Select(x => x.Voucher.Id).ToArray();
        var targets = await store.Set<VoucherTarget>().AsNoTracking().Where(x => ids.Contains(x.VoucherId) && x.ProductId != null).ToListAsync(ct);
        var now = DateTimeOffset.UtcNow;
        return rows.Select(x => {
            var v = x.Voucher;
            var status = VoucherLifecycle.Status(v.Status, v.StartsAt, v.ExpiresAt, v.UsageLimit, x.Used, now);
            return new ManagedVoucherDto(v.Id, v.StoreId, x.StoreName, v.Code, v.Name, v.Description, v.DiscountType, v.DiscountValue, v.MaxDiscount,
                v.MinOrderAmount, v.UsageLimit, v.PerUserLimit, x.Used, x.Claimed, v.StartsAt, v.ExpiresAt, status,
                targets.Where(t => t.VoucherId == v.Id).Select(t => t.ProductId!.Value).ToArray(),
                status == "UPCOMING" && x.Claimed == 0 && x.Used == 0, v.CreatedAt, v.UpdatedAt, v.DisabledReason);
        }).ToArray();
    }

    public async Task<VoucherManagementPageDto> ListAsync(Guid accountId, Guid? storeId, VoucherManagementQuery query, bool admin, CancellationToken ct)
    {
        if (query.Status is not (null or "ALL" or "ACTIVE" or "UPCOMING" or "ENDED" or "EXPIRED" or "EXHAUSTED" or "DISABLED")) throw new BusinessException("Bộ lọc voucher không hợp lệ");
        if (query.Search?.Length > 200 || query.From > query.To) throw new BusinessException("Bộ lọc tìm kiếm không hợp lệ");
        var source = Rows(await Scope(accountId, storeId, admin, ct));
        var now = DateTimeOffset.UtcNow;
        var available = source.Where(x => (x.Voucher.Status == "ACTIVE" || x.Voucher.Status == "MEMBER_OFFER") && x.Voucher.ExpiresAt > now && (x.Voucher.UsageLimit == null || x.Used < x.Voucher.UsageLimit));
        var stats = new VoucherStatisticsDto(await available.CountAsync(x => x.Voucher.StartsAt <= now, ct), await available.CountAsync(x => x.Voucher.StartsAt > now, ct), await source.SumAsync(x => (int?)x.Used, ct) ?? 0);
        if (!string.IsNullOrWhiteSpace(query.Search)) { var search = query.Search.Trim().ToLower(); source = source.Where(x => x.Voucher.Code.ToLower().Contains(search) || x.Voucher.Name.ToLower().Contains(search) || x.StoreName.ToLower().Contains(search)); }
        if (query.From is { } from) source = source.Where(x => x.Voucher.ExpiresAt >= from);
        if (query.To is { } to) source = source.Where(x => x.Voucher.StartsAt <= to);
        source = query.Status switch {
            "ACTIVE" => source.Where(x => (x.Voucher.Status == "ACTIVE" || x.Voucher.Status == "MEMBER_OFFER") && x.Voucher.StartsAt <= now && x.Voucher.ExpiresAt > now && (x.Voucher.UsageLimit == null || x.Used < x.Voucher.UsageLimit)),
            "UPCOMING" => source.Where(x => (x.Voucher.Status == "ACTIVE" || x.Voucher.Status == "MEMBER_OFFER") && x.Voucher.StartsAt > now && (x.Voucher.UsageLimit == null || x.Used < x.Voucher.UsageLimit)),
            "DISABLED" => source.Where(x => x.Voucher.Status != "ACTIVE" && x.Voucher.Status != "MEMBER_OFFER"),
            "EXPIRED" => source.Where(x => (x.Voucher.Status == "ACTIVE" || x.Voucher.Status == "MEMBER_OFFER") && x.Voucher.ExpiresAt <= now),
            "EXHAUSTED" => source.Where(x => (x.Voucher.Status == "ACTIVE" || x.Voucher.Status == "MEMBER_OFFER") && x.Voucher.ExpiresAt > now && x.Voucher.UsageLimit != null && x.Used >= x.Voucher.UsageLimit),
            "ENDED" => source.Where(x => x.Voucher.Status != "ACTIVE" && x.Voucher.Status != "MEMBER_OFFER" || x.Voucher.ExpiresAt <= now || x.Voucher.UsageLimit != null && x.Used >= x.Voucher.UsageLimit),
            _ => source,
        };
        var (page, size) = CatalogService.Paging(new(query.Page, query.PageSize));
        var total = await source.CountAsync(ct);
        var rows = await source.OrderByDescending(x => x.Voucher.CreatedAt).ThenBy(x => x.Voucher.Id).Skip((page - 1) * size).Take(size).ToListAsync(ct);
        return new(new(await Map(rows, ct), page, size, total), stats);
    }

    public async Task<VoucherManagementDetailDto> DetailAsync(Guid accountId, Guid? storeId, Guid id, int page, bool admin, CancellationToken ct)
    {
        var scope = await Scope(accountId, storeId, admin, ct);
        var row = await Rows(scope.Where(x => x.Id == id)).SingleOrDefaultAsync(ct) ?? throw new BusinessException("Không tìm thấy voucher", 404);
        var usages = store.Set<VoucherUsage>().AsNoTracking().Where(x => x.VoucherId == id);
        var (number, size) = CatalogService.Paging(new(page, 20));
        var history = await usages.OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id).Skip((number - 1) * size).Take(size).Select(x => new VoucherUsageHistoryDto(x.Id, x.OrderId, x.DiscountAmount, x.Status, x.CreatedAt)).ToListAsync(ct);
        var actions = await store.Set<AuditLog>().AsNoTracking().Where(x => x.EntityType == "Voucher" && x.EntityId == id).OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id).Take(100).Select(x => new VoucherActionDto(x.Action, x.Details, x.CreatedAt)).ToListAsync(ct);
        return new((await Map([row], ct))[0], new(history, number, size, await usages.CountAsync(ct)), actions);
    }

    private static void Validate(SellerVoucherInput x)
    {
        if (string.IsNullOrWhiteSpace(x.Code) || !Regex.IsMatch(x.Code.Trim(), "^[a-zA-Z0-9_-]{3,50}$")) throw new BusinessException("Mã voucher cần 3–50 chữ, số, dấu gạch ngang hoặc gạch dưới");
        if (string.IsNullOrWhiteSpace(x.Name) || x.Name.Trim().Length > 150 || x.Description?.Length > 10000) throw new BusinessException("Tên hoặc mô tả voucher không hợp lệ");
        if (x.DiscountType is not ("FIXED" or "PERCENT") || x.DiscountValue <= 0 || x.DiscountValue > 9999999999999999.99m || decimal.Round(x.DiscountValue, 2) != x.DiscountValue || x.MinOrderAmount < 0 || x.MinOrderAmount > 9999999999999999.99m || decimal.Round(x.MinOrderAmount, 2) != x.MinOrderAmount || x.MaxDiscount <= 0 || x.MaxDiscount > 9999999999999999.99m || x.MaxDiscount is { } max && decimal.Round(max, 2) != max)
            throw new BusinessException("Giá trị giảm hoặc đơn tối thiểu không hợp lệ");
        if (x.DiscountType == "PERCENT" && (x.DiscountValue > 100 || x.MaxDiscount is null)) throw new BusinessException("Giảm phần trăm cần mức giảm tối đa và không vượt quá 100%");
        if (x.UsageLimit < 1 || x.PerUserLimit < 1 || x.PerUserLimit > x.UsageLimit || x.ExpiresAt <= x.StartsAt || x.ExpiresAt <= DateTimeOffset.UtcNow) throw new BusinessException("Thời gian hoặc số lượt sử dụng không hợp lệ");
        if (x.ProductIds is null || x.ProductIds.Count > 100 || x.ProductIds.Distinct().Count() != x.ProductIds.Count || x.ProductIds.Contains(Guid.Empty)) throw new BusinessException("Danh sách sản phẩm không hợp lệ");
    }
    public async Task<ManagedVoucherDto> SaveAsync(Guid accountId, Guid storeId, Guid? id, SellerVoucherInput input, CancellationToken ct)
    {
        Validate(input);
        await using var tx = await store.BeginTransactionAsync(ct);
        await Scope(accountId, storeId, false, ct);
        var code = input.Code.Trim().ToUpperInvariant();
        var voucher = id is null ? new Voucher { StoreId = storeId } : await store.Set<Voucher>().SingleOrDefaultAsync(x => x.Id == id && x.StoreId == storeId, ct) ?? throw new BusinessException("Không tìm thấy voucher", 404);
        var now = DateTimeOffset.UtcNow;
        if (id is not null && (voucher.Status != "ACTIVE" || voucher.StartsAt <= now || await store.Set<UserVoucher>().AnyAsync(x => x.VoucherId == id, ct) || await store.Set<VoucherUsage>().AnyAsync(x => x.VoucherId == id, ct)))
            throw new BusinessException("Chỉ sửa voucher chưa bắt đầu và chưa có người nhận. Hãy sao chép để tạo chương trình mới", 409);
        if (await store.Set<Voucher>().AnyAsync(x => x.Code == code && x.Id != voucher.Id, ct)) throw new BusinessException("Mã voucher đã tồn tại", 409);
        var productIds = input.ProductIds!.ToArray();
        if (await store.Set<Product>().CountAsync(x => productIds.Contains(x.Id) && x.StoreId == storeId && x.Status == 1 && !x.IsDeleted, ct) != productIds.Length)
            throw new BusinessException("Chỉ được chọn sản phẩm đang bán thuộc cửa hàng của bạn", 400);
        voucher.Code = code; voucher.Name = input.Name.Trim(); voucher.Description = input.Description?.Trim() ?? "";
        voucher.DiscountType = input.DiscountType; voucher.DiscountValue = input.DiscountValue; voucher.MaxDiscount = input.MaxDiscount;
        voucher.MinOrderAmount = input.MinOrderAmount; voucher.UsageLimit = input.UsageLimit; voucher.PerUserLimit = input.PerUserLimit;
        voucher.StartsAt = input.StartsAt.ToUniversalTime(); voucher.ExpiresAt = input.ExpiresAt.ToUniversalTime(); voucher.Status = "ACTIVE"; voucher.UpdatedAt = now;
        if (id is null) store.Set<Voucher>().Add(voucher);
        store.Set<VoucherTarget>().RemoveRange(await store.Set<VoucherTarget>().Where(x => x.VoucherId == voucher.Id).ToListAsync(ct));
        foreach (var productId in productIds) store.Set<VoucherTarget>().Add(new VoucherTarget { VoucherId = voucher.Id, ProductId = productId });
        store.Set<AuditLog>().Add(new AuditLog { AccountId = accountId, Action = id is null ? "CREATE_SELLER_VOUCHER" : "UPDATE_SELLER_VOUCHER", EntityType = "Voucher", EntityId = voucher.Id, CreatedAt = now });
        await store.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return (await DetailAsync(accountId, storeId, voucher.Id, 1, false, ct)).Voucher;
    }
    public async Task<ManagedVoucherDto> DuplicateAsync(Guid accountId, Guid storeId, Guid id, SellerVoucherInput input, CancellationToken ct)
    {
        await DetailAsync(accountId, storeId, id, 1, false, ct);
        return await SaveAsync(accountId, storeId, null, input, ct);
    }
    public async Task<ManagedVoucherDto> DisableAsync(Guid accountId, Guid? storeId, Guid id, VoucherDisableInput input, bool admin, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(input.Reason) || input.Reason.Trim().Length > 1000) throw new BusinessException("Vui lòng nhập lý do, tối đa 1000 ký tự");
        await using var tx = await store.BeginTransactionAsync(ct);
        var scope = await Scope(accountId, storeId, admin, ct);
        if (!await scope.AnyAsync(x => x.Id == id, ct)) throw new BusinessException("Không tìm thấy voucher", 404);
        var voucher = await store.Set<Voucher>().SingleAsync(x => x.Id == id, ct);
        if (voucher.Status is "ACTIVE" or "MEMBER_OFFER")
        {
            voucher.Status = "INACTIVE"; voucher.UpdatedAt = DateTimeOffset.UtcNow; voucher.DisabledAt = voucher.UpdatedAt;
            voucher.DisabledReason = input.Reason.Trim(); voucher.DisabledByAccountId = accountId;
            store.Set<AuditLog>().Add(new AuditLog { AccountId = accountId, Action = admin ? "ADMIN_DISABLE_VOUCHER" : "SELLER_DISABLE_VOUCHER", EntityType = "Voucher", EntityId = id, Details = voucher.DisabledReason, CreatedAt = voucher.UpdatedAt });
            await store.SaveChangesAsync(ct);
        }
        await tx.CommitAsync(ct);
        return (await DetailAsync(accountId, storeId, id, 1, admin, ct)).Voucher;
    }
}
