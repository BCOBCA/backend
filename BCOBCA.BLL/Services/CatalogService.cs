using BCOBCA.BLL.Common;
using BCOBCA.BLL.DTOs;
using BCOBCA.BLL.Interfaces;
using BCOBCA.DAL.Entities;
using BCOBCA.DAL.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BCOBCA.BLL.Services;

public sealed class CatalogService(ICommerceStore store) : ICatalogService
{
    public static (int Page, int Size) Paging(CatalogQuery query)
    {
        if (query.Page < 1 || query.Page > 100000 || query.PageSize < 1 || query.PageSize > 100 || query.Search?.Length > 200)
            throw new BusinessException("Tham số phân trang không hợp lệ", 400);
        if (query.Sort is not ("newest" or "name" or "price-asc" or "price-desc")) throw new BusinessException("Cách sắp xếp không hợp lệ", 400);
        return (query.Page, query.PageSize);
    }

    private static async Task<PageResult<T>> PageAsync<T>(IQueryable<T> source, CatalogQuery query, CancellationToken ct)
    {
        var (page, size) = Paging(query);
        var count = await source.CountAsync(ct);
        return new(await source.Skip((page - 1) * size).Take(size).ToListAsync(ct), page, size, count);
    }

    public async Task<PageResult<BrandDto>> BrandsAsync(CatalogQuery query, CancellationToken ct)
    {
        var source = store.Set<Brand>().AsNoTracking().Where(x => x.Status == 1);
        if (!string.IsNullOrWhiteSpace(query.Search)) source = source.Where(x => x.Name.ToLower().Contains(query.Search.ToLower()));
        return await PageAsync(source.OrderBy(x => x.Name).ThenBy(x => x.Id).Select(x => new BrandDto(x.Id, x.Name, x.Slug, x.LogoUrl, x.CoverUrl, x.Description, x.Website, x.Phone)), query, ct);
    }

    public async Task<BrandDto> BrandAsync(Guid id, CancellationToken ct)
        => await store.Set<Brand>().AsNoTracking().Where(x => x.Id == id && x.Status == 1).Select(x => new BrandDto(x.Id, x.Name, x.Slug, x.LogoUrl, x.CoverUrl, x.Description, x.Website, x.Phone)).SingleOrDefaultAsync(ct)
            ?? throw new BusinessException("Không tìm thấy thương hiệu", 404);

    public async Task<PageResult<CategoryDto>> CategoriesAsync(CatalogQuery query, CancellationToken ct)
        => await PageAsync(store.Set<Category>().AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.SortOrder).ThenBy(x => x.Id)
            .Select(x => new CategoryDto(x.Id, x.ParentId, x.Name, x.Slug, x.ImageUrl)), query, ct);

    public async Task<IReadOnlyList<CategoryAttributeDefinitionDto>> CategoryAttributesAsync(Guid id, CancellationToken ct)
    {
        if (!await store.Set<Category>().AnyAsync(c => c.Id == id && c.IsActive, ct)) throw new BusinessException("Không tìm thấy danh mục", 404);
        return await ProductSpecificationRules.ResolveDefinitionsAsync(store, id, ct);
    }

    private IQueryable<Product> ProductQuery() => ProductCatalogScope.Available(store);

    internal IQueryable<CatalogItemDto> ProjectProducts(IQueryable<Product> source, Guid? userId = null) => source.Select(p => new CatalogItemDto(p.Id, p.BrandId, p.Name, p.Slug, p.Description,
            store.Set<ProductImage>().Where(i => i.ProductId == p.Id).OrderByDescending(i => i.IsPrimary).ThenBy(i => i.SortOrder).Select(i => i.ImageUrl).FirstOrDefault() ?? "",
            store.Set<ProductVariant>().Where(v => v.ProductId == p.Id && v.IsActive).OrderByDescending(v => v.StockQuantity > v.ReservedQuantity).ThenBy(v => v.SalePrice ?? v.Price).ThenBy(v => v.Id).Select(v => (decimal?)v.Price).FirstOrDefault() ?? p.BasePrice,
            store.Set<ProductVariant>().Where(v => v.ProductId == p.Id && v.IsActive).OrderByDescending(v => v.StockQuantity > v.ReservedQuantity).ThenBy(v => v.SalePrice ?? v.Price).ThenBy(v => v.Id).Select(v => v.SalePrice).FirstOrDefault(), false,
            store.Set<ProductVariant>().Where(v => v.ProductId == p.Id && v.IsActive).OrderByDescending(v => v.StockQuantity > v.ReservedQuantity).ThenBy(v => v.SalePrice ?? v.Price).ThenBy(v => v.Id).Select(v => (Guid?)v.Id).FirstOrDefault(),
            store.Set<ProductVariant>().Where(v => v.ProductId == p.Id && v.IsActive).Sum(v => v.StockQuantity - v.ReservedQuantity),
            userId != null && store.Set<ProductFavorite>().Any(f => f.UserId == userId && f.ProductId == p.Id), null, null, null, p.SKU, null, p.StoreId));

    public async Task<PageResult<CatalogItemDto>> ProductsAsync(CatalogQuery query, CancellationToken ct, Guid? userId = null)
    {
        var source = ProductQuery();
        if (query.BrandId is not null) source = source.Where(x => x.BrandId == query.BrandId);
        if (query.StoreId is not null) source = source.Where(x => x.StoreId == query.StoreId);
        if (query.CategoryId is { } categoryId)
        {
            var ids = CategoryTree.Descendants(categoryId, CategoryTree.Active(await store.Set<Category>().AsNoTracking().ToListAsync(ct)));
            source = source.Where(x => ids.Contains(x.CategoryId));
        }
        if (!string.IsNullOrWhiteSpace(query.Search)) source = source.Where(x => x.Name.ToLower().Contains(query.Search.ToLower()));
        var priced = source.Select(p => new { Product = p, EffectivePrice =
            store.Set<ProductVariant>().Where(v => v.ProductId == p.Id && v.IsActive && v.StockQuantity > v.ReservedQuantity).Select(v => (decimal?)(v.SalePrice ?? v.Price)).Min()
            ?? store.Set<ProductVariant>().Where(v => v.ProductId == p.Id && v.IsActive).Select(v => (decimal?)(v.SalePrice ?? v.Price)).Min() ?? p.BasePrice });
        var ordered = query.Sort switch { "name" => priced.OrderBy(x => x.Product.Name).ThenBy(x => x.Product.Id), "price-asc" => priced.OrderBy(x => x.EffectivePrice).ThenBy(x => x.Product.Id), "price-desc" => priced.OrderByDescending(x => x.EffectivePrice).ThenBy(x => x.Product.Id), _ => priced.OrderByDescending(x => x.Product.CreatedAt).ThenBy(x => x.Product.Id) };
        return await PageAsync(ProjectProducts(ordered.Select(x => x.Product), userId), query, ct);
    }

    public async Task<CatalogItemDto> ProductAsync(Guid id, CancellationToken ct, Guid? userId = null)
    {
        var product = await ProjectProducts(ProductQuery().Where(x => x.Id == id), userId).SingleOrDefaultAsync(ct) ?? throw new BusinessException("Không tìm thấy sản phẩm", 404);
        var variantRows = await store.Set<ProductVariant>().AsNoTracking().Where(x => x.ProductId == id && x.IsActive).OrderBy(x => x.Price).ThenBy(x => x.Id).ToListAsync(ct);
        var variants = variantRows.Select(CatalogVariantMapper.ToDto).ToArray();
        var images = await store.Set<ProductImage>().AsNoTracking().Where(x => x.ProductId == id).OrderByDescending(x => x.IsPrimary).ThenBy(x => x.SortOrder).Select(x => x.ImageUrl).ToListAsync(ct);
        var metadata = await store.Set<Product>().AsNoTracking().Where(p => p.Id == id).Select(p => new { p.CategoryId, p.Specifications, p.SKU, p.StoreId }).SingleAsync(ct);
        var definitions = await ProductSpecificationRules.ResolveDefinitionsAsync(store, metadata.CategoryId, ct);
        var specifications = ProductSpecificationRules.BuildGroups(ProductSpecificationRules.ParseValues(metadata.Specifications), definitions);
        var sellers = await store.Set<Store>().AsNoTracking()
            .Where(s => s.Id == metadata.StoreId && s.StoreType == 1 && s.Status == 1 && s.DeletedAt == null)
            .OrderBy(s => s.Name).ThenBy(s => s.Id)
            .Select(s => new ProductSellerDto(s.Id, s.Name, s.Slug, s.LogoUrl, s.Description,
                store.Set<StoreSettings>().Any(settings => settings.StoreId == s.Id && settings.ShowPhone) ? s.Phone : null))
            .ToListAsync(ct);
        return product with { Variants = variants, ImageUrls = images, Specifications = specifications, SKU = metadata.SKU, StoreId = metadata.StoreId, Sellers = sellers };
    }

    private IQueryable<Service> ServiceQuery() => store.Set<Service>().AsNoTracking()
        .Where(s => s.Status == "ACTIVE" && store.Set<Brand>().Any(b => b.Id == s.BrandId && b.Status == 1));
    private static IQueryable<CatalogItemDto> ProjectServices(IQueryable<Service> source) => source.Select(s => new CatalogItemDto(s.Id, s.BrandId, s.Name, s.Slug, s.Description, s.ThumbnailUrl, s.PriceFrom, null, true, null, 0));

    public async Task<PageResult<CatalogItemDto>> ServicesAsync(CatalogQuery query, CancellationToken ct)
    {
        var source = ServiceQuery();
        if (query.BrandId is not null) source = source.Where(x => x.BrandId == query.BrandId);
        if (!string.IsNullOrWhiteSpace(query.Search)) source = source.Where(x => x.Name.ToLower().Contains(query.Search.ToLower()));
        source = query.Sort switch { "price-asc" => source.OrderBy(x => x.PriceFrom == null).ThenBy(x => x.PriceFrom).ThenBy(x => x.Id), "price-desc" => source.OrderBy(x => x.PriceFrom == null).ThenByDescending(x => x.PriceFrom).ThenBy(x => x.Id), "newest" => source.OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id), _ => source.OrderBy(x => x.Name).ThenBy(x => x.Id) };
        return await PageAsync(ProjectServices(source), query, ct);
    }

    public async Task<CatalogItemDto> ServiceAsync(Guid id, CancellationToken ct)
        => await ProjectServices(ServiceQuery().Where(x => x.Id == id)).SingleOrDefaultAsync(ct) ?? throw new BusinessException("Không tìm thấy dịch vụ", 404);

    public async Task<IReadOnlyList<BannerDto>> BannersAsync(CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        return await store.Set<Banner>().AsNoTracking().Where(x => x.IsActive && x.StartsAt <= now && x.EndsAt > now)
            .OrderByDescending(x => x.StartsAt).ThenBy(x => x.Id).Take(20).Select(x => new BannerDto(x.Id, x.ImageUrl, x.TargetUrl)).ToListAsync(ct);
    }

    public async Task<PageResult<ArticleDto>> ArticlesAsync(CatalogQuery query, CancellationToken ct)
        => await PageAsync(store.Set<Article>().AsNoTracking().Where(x => x.Status == "PUBLISHED" && x.PublishedAt <= DateTimeOffset.UtcNow)
            .OrderByDescending(x => x.PublishedAt).ThenBy(x => x.Id).Select(x => new ArticleDto(x.Id, x.Title, x.Slug, x.ThumbnailUrl, x.Summary, "", x.PublishedAt)), query, ct);

    public async Task<ArticleDto> ArticleAsync(Guid id, CancellationToken ct)
        => await store.Set<Article>().AsNoTracking().Where(x => x.Id == id && x.Status == "PUBLISHED" && x.PublishedAt <= DateTimeOffset.UtcNow)
            .Select(x => new ArticleDto(x.Id, x.Title, x.Slug, x.ThumbnailUrl, x.Summary, x.Content, x.PublishedAt)).SingleOrDefaultAsync(ct)
                ?? throw new BusinessException("Không tìm thấy bài viết", 404);

    public async Task<Guid> ConsultAsync(Guid? userId, ConsultationRequest input, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(input.CustomerName) || input.CustomerName.Length > 150 || !System.Text.RegularExpressions.Regex.IsMatch(input.Phone, "^\\+?[0-9]{9,15}$") || input.Email.Length > 255 || input.Email != "" && !System.Net.Mail.MailAddress.TryCreate(input.Email, out _) || input.Description.Length is < 5 or > 10000)
            throw new BusinessException("Vui lòng kiểm tra họ tên, điện thoại và nội dung tư vấn");
        await ServiceAsync(input.ServiceId, ct);
        var request = new ServiceRequest { ServiceId = input.ServiceId, UserId = userId, CustomerName = input.CustomerName, Phone = input.Phone, Email = input.Email, Description = input.Description, Status = "PENDING", CreatedAt = DateTimeOffset.UtcNow };
        store.Set<ServiceRequest>().Add(request);
        await store.SaveChangesAsync(ct);
        return request.Id;
    }
}
