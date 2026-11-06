using BCOBCA.BLL.Common;
using BCOBCA.BLL.DTOs;
using BCOBCA.BLL.Interfaces;
using BCOBCA.DAL.Entities;
using BCOBCA.DAL.Interfaces;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace BCOBCA.BLL.Services;

public sealed class DealerService(ICommerceStore store) : IDealerService
{
    private IQueryable<Store> PublicDealers() => store.Set<Store>().AsNoTracking()
        .Where(x => x.StoreType == 1 && x.Status == 1 && x.DeletedAt == null);

    private IQueryable<StoreBranch> PublicBranches() => store.Set<StoreBranch>().AsNoTracking()
        .Where(b => b.IsActive && store.Set<StoreSettings>().Any(s => s.StoreId == b.StoreId && s.ShowBranches));

    private IQueryable<DealerDto> Project(IQueryable<Store> source, Guid? accountId) => source.Select(x => new DealerDto(
        x.Id, x.Name, x.Slug, x.LogoUrl, x.CoverUrl, x.Description, x.IsVerified,
        store.Set<StoreFollower>().Count(f => f.StoreId == x.Id),
        PublicBranches().Where(b => b.StoreId == x.Id && b.ProvinceCode != "")
            .OrderByDescending(b => b.IsMain).ThenBy(b => b.Id).Select(b => b.ProvinceCode).FirstOrDefault() ?? "Chưa công bố khu vực",
        store.Set<StoreBranch>().Count(b => b.StoreId == x.Id && b.IsActive),
        accountId != null && store.Set<StoreFollower>().Any(f => f.StoreId == x.Id && f.AccountId == accountId)));

    public async Task<PageResult<DealerDto>> ListAsync(DealerQuery query, Guid? accountId, CancellationToken ct)
    {
        if (query.Page is < 1 or > 100000 || query.PageSize is < 1 or > 50 || query.Search?.Length > 200 || query.ProvinceCode?.Length > 20 || query.SortBy is not ("popular" or "newest" or "name"))
            throw new BusinessException("Tham số tìm kiếm đại lý không hợp lệ", 400);
        var source = PublicDealers();
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToLower();
            source = source.Where(x => x.Name.ToLower().Contains(search));
        }
        if (!string.IsNullOrWhiteSpace(query.ProvinceCode)) source = source.Where(x => PublicBranches().Any(b => b.StoreId == x.Id && b.ProvinceCode == query.ProvinceCode));
        if (query.VerifiedOnly) source = source.Where(x => x.IsVerified);
        var total = await source.CountAsync(ct);
        var sorted = query.SortBy switch
        {
            "name" => source.OrderBy(x => x.Name).ThenBy(x => x.Id),
            "newest" => source.OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id),
            _ => source.OrderByDescending(x => store.Set<StoreFollower>().Count(f => f.StoreId == x.Id)).ThenBy(x => x.Id)
        };
        var items = await Project(sorted.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize), accountId).ToListAsync(ct);
        return new(items, query.Page, query.PageSize, total);
    }

    public async Task<DealerDetailDto> DetailAsync(string slug, Guid? accountId, CancellationToken ct)
    {
        if (slug.Length > 220) throw new BusinessException("Không tìm thấy đại lý", 404);
        var dealer = await Project(PublicDealers().Where(x => x.Slug == slug), accountId).SingleOrDefaultAsync(ct)
            ?? throw new BusinessException("Không tìm thấy đại lý", 404);
        var contact = await PublicDealers().Where(x => x.Id == dealer.Id).Select(x => new
        {
            Phone = store.Set<StoreSettings>().Any(s => s.StoreId == x.Id && s.ShowPhone) ? x.Phone : null,
            Email = store.Set<StoreSettings>().Any(s => s.StoreId == x.Id && s.ShowEmail) ? x.Email : null,
            x.Website,
            ShowPhone = store.Set<StoreSettings>().Any(s => s.StoreId == x.Id && s.ShowPhone)
        }).SingleAsync(ct);
        var branches = await PublicBranches().Where(b => b.StoreId == dealer.Id).OrderByDescending(b => b.IsMain).ThenBy(b => b.Id)
            .Select(b => new DealerBranchDto(b.Id, b.Name, b.Address, b.ProvinceCode, b.WardCode, contact.ShowPhone ? b.Phone : null, b.Latitude, b.Longitude, b.IsMain)).ToListAsync(ct);
        return new(dealer, contact.Phone, contact.Email, contact.Website, branches);
    }

    public async Task<IReadOnlyList<string>> RegionsAsync(CancellationToken ct) => await PublicBranches()
        .Where(b => b.ProvinceCode != "" && PublicDealers().Any(s => s.Id == b.StoreId))
        .Select(b => b.ProvinceCode).Distinct().OrderBy(x => x).ToListAsync(ct);

    public async Task<DealerFollowDto> FollowAsync(Guid id, Guid accountId, bool following, CancellationToken ct)
    {
        if (!await PublicDealers().AnyAsync(x => x.Id == id, ct)) throw new BusinessException("Không tìm thấy đại lý", 404);
        if (!await store.Set<Account>().AnyAsync(x => x.Id == accountId && x.Status == 1 && !x.IsDeleted, ct)) throw new BusinessException("Tài khoản không hoạt động", 403);
        if (following && !await store.Set<StoreFollower>().AnyAsync(f => f.StoreId == id && f.AccountId == accountId, ct))
        {
            var follower = new StoreFollower { StoreId = id, AccountId = accountId };
            store.Set<StoreFollower>().Add(follower);
            try { await store.SaveChangesAsync(ct); }
            catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505", ConstraintName: "PK_StoreFollowers" })
            {
                store.Set<StoreFollower>().Entry(follower).State = EntityState.Detached;
            }
        }
        else if (!following) await store.Set<StoreFollower>().Where(f => f.StoreId == id && f.AccountId == accountId).ExecuteDeleteAsync(ct);
        return new(following, await store.Set<StoreFollower>().CountAsync(f => f.StoreId == id, ct));
    }
}
