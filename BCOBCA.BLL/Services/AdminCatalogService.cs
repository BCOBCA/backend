using System.Text.Json;
using System.Text.RegularExpressions;
using BCOBCA.BLL.Common;
using BCOBCA.BLL.DTOs;
using BCOBCA.BLL.Interfaces;
using BCOBCA.DAL.Entities;
using BCOBCA.DAL.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BCOBCA.BLL.Services;

public sealed class AdminCatalogService(ICommerceStore store) : IAdminCatalogService
{
    private static void Text(string value, string field, int max, bool required = true)
    {
        if (value.Length > max || required && string.IsNullOrWhiteSpace(value)) throw new BusinessException($"{field} không hợp lệ");
    }
    private static void Slug(string value, int max)
    {
        if (value.Length > max || !Regex.IsMatch(value, "^[a-z0-9]+(?:-[a-z0-9]+)*$")) throw new BusinessException("Slug cần chữ thường không dấu, số và dấu gạch ngang");
    }
    private static void Url(string value, bool required = false)
    {
        if (value == "" && !required) return;
        if (value.Length > 2000 || !Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https")) throw new BusinessException("URL không hợp lệ");
    }
    private static void Money(decimal value)
    {
        if (value < 0 || value > 9999999999999999.99m || decimal.Round(value, 2) != value) throw new BusinessException("Số tiền không hợp lệ");
    }
    private Task<bool> Admin(Guid accountId, CancellationToken ct) => store.Set<AccountRole>().AnyAsync(x => x.AccountId == accountId && (x.Role == "Admin" || x.Role == "SuperAdmin"), ct);
    private async Task RequireAdmin(Guid accountId, CancellationToken ct)
    {
        if (!await Admin(accountId, ct)) throw new BusinessException("Không có quyền quản trị", 403);
    }
    private async Task StoreAccess(Guid accountId, Guid storeId, CancellationToken ct)
    {
        var owner = await store.Set<Store>().Where(s => s.Id == storeId && s.StoreType == 1 && s.DeletedAt == null).Select(s => (Guid?)s.OwnerAccountId).SingleOrDefaultAsync(ct)
            ?? throw new BusinessException("Không tìm thấy cửa hàng", 404);
        if (owner != accountId && !await Admin(accountId, ct)) throw new BusinessException("Không có quyền quản lý sản phẩm của cửa hàng này", 403);
    }
    private async Task BrandAccess(Guid accountId, Guid brandId, CancellationToken ct)
    {
        if (await Admin(accountId, ct)) return;
        if (!await store.Set<AccountRole>().AnyAsync(x => x.AccountId == accountId && (x.Role == "BrandOwner" || x.Role == "BrandStaff"), ct)) throw new BusinessException("Cần quyền quản lý thương hiệu", 403);
        if (!await store.Set<Brand>().AnyAsync(x => x.Id == brandId && x.OwnerAccountId == accountId, ct)
            && !await store.Set<BrandMember>().AnyAsync(x => x.BrandId == brandId && x.AccountId == accountId && x.Status == 1 && (x.Role == "Owner" || x.Role == "Manager"), ct)) throw new BusinessException("Không có quyền quản lý thương hiệu này", 403);
    }
    private async Task Audit(Guid accountId, string action, string entity, Guid id, CancellationToken ct)
    {
        store.Set<AuditLog>().Add(new AuditLog { AccountId = accountId, Action = action, EntityType = entity, EntityId = id, CreatedAt = DateTimeOffset.UtcNow });
        await store.SaveChangesAsync(ct);
    }

    public async Task<Guid> SaveBrandAsync(Guid accountId, Guid? id, BrandInput input, CancellationToken ct)
    {
        Text(input.Name, "Tên thương hiệu", 200); Slug(input.Slug, 220); Text(input.Description, "Mô tả", 50000, false);
        Text(input.Phone, "Điện thoại", 20, false); Text(input.ContactEmail, "Email", 255, false); Text(input.Address, "Địa chỉ", 2000, false);
        Url(input.LogoUrl); Url(input.CoverUrl); Url(input.Website);
        await using var tx = await store.BeginTransactionAsync(ct);
        var owner = input.OwnerAccountId ?? accountId;
        if (id is null)
        {
            if (!await Admin(accountId, ct) && !await store.Set<AccountRole>().AnyAsync(x => x.AccountId == accountId && x.Role == "BrandOwner", ct)) throw new BusinessException("Cần quyền chủ thương hiệu", 403);
            if (owner != accountId) await RequireAdmin(accountId, ct);
        }
        else await BrandAccess(accountId, id.Value, ct);
        if (!await store.Set<Account>().AnyAsync(x => x.Id == owner && x.Status == 1, ct)) throw new BusinessException("Chủ thương hiệu không hợp lệ");
        var brand = id is null ? new Brand { OwnerAccountId = owner, CreatedAt = DateTimeOffset.UtcNow } : await store.Set<Brand>().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new BusinessException("Không tìm thấy thương hiệu", 404);
        if (input.OwnerAccountId is not null && brand.OwnerAccountId != owner) { await RequireAdmin(accountId, ct); brand.OwnerAccountId = owner; }
        brand.Name = input.Name.Trim(); brand.Slug = input.Slug; brand.Description = input.Description; brand.LogoUrl = input.LogoUrl; brand.CoverUrl = input.CoverUrl; brand.Website = input.Website;
        brand.ContactEmail = input.ContactEmail; brand.Phone = input.Phone; brand.Address = input.Address; brand.Status = (short)(input.IsActive ? 1 : 0); brand.UpdatedAt = DateTimeOffset.UtcNow;
        if (id is null) store.Set<Brand>().Add(brand);
        await Audit(accountId, id is null ? "CREATE" : "UPDATE", "Brand", brand.Id, ct); await tx.CommitAsync(ct); return brand.Id;
    }

    public async Task<Guid> SaveCategoryAsync(Guid accountId, Guid? id, CategoryInput input, CancellationToken ct)
    {
        await RequireAdmin(accountId, ct); Text(input.Name, "Tên danh mục", 150); Slug(input.Slug, 180); Url(input.ImageUrl);
        await using var tx = await store.BeginTransactionAsync(ct);
        var parent = input.ParentId;
        for (var depth = 0; parent is not null; depth++)
        {
            if (parent == id || depth > 32) throw new BusinessException("Danh mục cha tạo vòng lặp hoặc quá sâu");
            var ancestor = await store.Set<Category>().SingleOrDefaultAsync(x => x.Id == parent, ct) ?? throw new BusinessException("Danh mục cha không tồn tại"); parent = ancestor.ParentId;
        }
        var category = id is null ? new Category() : await store.Set<Category>().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new BusinessException("Không tìm thấy danh mục", 404);
        var localDefinitions = input.AttributeDefinitions ?? ProductSpecificationRules.ParseDefinitions(category.AttributeDefinitions);
        ProductSpecificationRules.ValidateDefinitions(localDefinitions);
        var inheritedDefinitions = input.ParentId is { } parentId ? await ProductSpecificationRules.ResolveDefinitionsAsync(store, parentId, ct) : Array.Empty<CategoryAttributeDefinitionDto>();
        ProductSpecificationRules.MergeDefinitions(new[] { inheritedDefinitions, localDefinitions });
        if (input.AttributeDefinitions is not null) category.AttributeDefinitions = JsonSerializer.Serialize(input.AttributeDefinitions, ProductSpecificationRules.JsonOptions);
        category.Name = input.Name.Trim(); category.Slug = input.Slug; category.ParentId = input.ParentId; category.ImageUrl = input.ImageUrl; category.SortOrder = input.SortOrder; category.IsActive = input.IsActive;
        if (id is null) store.Set<Category>().Add(category);
        await Audit(accountId, id is null ? "CREATE" : "UPDATE", "Category", category.Id, ct); await tx.CommitAsync(ct); return category.Id;
    }

    public async Task<Guid> SaveProductAsync(Guid accountId, Guid? id, ProductInput input, CancellationToken ct)
    {
        Text(input.Name, "Tên sản phẩm", 250); Slug(input.Slug, 280); Text(input.SKU, "SKU", 100); Text(input.Description, "Mô tả", 100000, false); Text(input.ShortDescription, "Mô tả ngắn", 2000, false);
        Money(input.BasePrice); if (input.SalePrice is not null) { Money(input.SalePrice.Value); if (input.SalePrice > input.BasePrice) throw new BusinessException("Giá khuyến mãi vượt giá gốc"); }
        if (input.Variants is null || input.ImageUrls is null || input.Variants.Any(x => x is null) || input.ImageUrls.Any(x => x is null) || input.Variants.Count is < 1 or > 100 || input.ImageUrls.Count > 20 || !input.Variants.Any(x => x.IsActive) || input.Variants.Select(x => x.SKU).Distinct().Count() != input.Variants.Count) throw new BusinessException("Biến thể hoặc bộ ảnh không hợp lệ");
        foreach (var url in input.ImageUrls) Url(url, true);
        foreach (var v in input.Variants)
        {
            Text(v.SKU, "SKU biến thể", 100); Text(v.VariantName, "Tên biến thể", 150); Money(v.Price);
            if (v.StockQuantity < 0) throw new BusinessException("Tồn kho không hợp lệ");
            if (v.SalePrice is not null) { Money(v.SalePrice.Value); if (v.SalePrice > v.Price) throw new BusinessException("Giá khuyến mãi vượt giá gốc"); }
            if (v.Attributes.Length > 10000) throw new BusinessException("Thuộc tính quá dài");
            try { using var json = JsonDocument.Parse(v.Attributes); if (json.RootElement.ValueKind != JsonValueKind.Object) throw new JsonException(); }
            catch (JsonException) { throw new BusinessException("Thuộc tính biến thể phải là JSON object"); }
        }
        await using var tx = await store.BeginTransactionAsync(ct);
        if (!await store.Set<Brand>().AnyAsync(x => x.Id == input.BrandId, ct) || !await store.Set<Category>().AnyAsync(x => x.Id == input.CategoryId, ct)) throw new BusinessException("Thương hiệu hoặc danh mục không tồn tại");
        var product = id is null ? new Product { BrandId = input.BrandId, CreatedAt = DateTimeOffset.UtcNow } : await store.Set<Product>().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new BusinessException("Không tìm thấy sản phẩm", 404);
        var owningStore = product.StoreId ?? input.StoreId ?? throw new BusinessException("Cần chọn cửa hàng đăng bán sản phẩm");
        await StoreAccess(accountId, owningStore, ct);
        if (input.StoreId is not null && input.StoreId != owningStore) throw new BusinessException("Không chuyển sản phẩm sang cửa hàng khác", 409);
        if (id is not null && product.BrandId != input.BrandId) throw new BusinessException("Không chuyển sản phẩm sang thương hiệu khác");
        product.StoreId = owningStore;
        var definitions = await ProductSpecificationRules.ResolveDefinitionsAsync(store, input.CategoryId, ct);
        if (id is null || input.Specifications is not null || product.CategoryId != input.CategoryId)
            ProductSpecificationRules.ValidateValues(input.Specifications ?? ProductSpecificationRules.ParseValues(product.Specifications), definitions, "product");
        foreach (var v in input.Variants) ProductSpecificationRules.ValidateValues(ProductSpecificationRules.ParseValues(v.Attributes), definitions, "variant");
        if (input.Specifications is not null) product.Specifications = JsonSerializer.Serialize(input.Specifications, ProductSpecificationRules.JsonOptions);
        product.CategoryId = input.CategoryId; product.Name = input.Name; product.Slug = input.Slug; product.SKU = input.SKU; product.Description = input.Description; product.ShortDescription = input.ShortDescription;
        if (input.ShippingWeightGrams is <= 0 or > 50000 || input.ShippingLengthCm is <= 0 or > 200 || input.ShippingWidthCm is <= 0 or > 200 || input.ShippingHeightCm is <= 0 or > 200) throw new BusinessException("Kích thước hoặc khối lượng vận chuyển không hợp lệ");
        product.ShippingWeightGrams = input.ShippingWeightGrams ?? product.ShippingWeightGrams;
        product.ShippingLengthCm = input.ShippingLengthCm ?? product.ShippingLengthCm; product.ShippingWidthCm = input.ShippingWidthCm ?? product.ShippingWidthCm; product.ShippingHeightCm = input.ShippingHeightCm ?? product.ShippingHeightCm;
        product.BasePrice = input.BasePrice; product.SalePrice = input.SalePrice; product.Status = (short)(input.IsActive ? 1 : 0); product.IsFeatured = input.IsFeatured; product.UpdatedAt = DateTimeOffset.UtcNow;
        if (id is null) store.Set<Product>().Add(product);
        var existing = await store.Set<ProductVariant>().Where(x => x.ProductId == product.Id).ToListAsync(ct);
        if (input.Variants.Where(x => x.Id is not null).Select(x => x.Id).Distinct().Count() != input.Variants.Count(x => x.Id is not null)) throw new BusinessException("Biến thể bị trùng");
        foreach (var old in existing.Where(x => !input.Variants.Any(v => v.Id == x.Id))) old.IsActive = false;
        foreach (var v in input.Variants)
        {
            var variant = v.Id is null ? new ProductVariant { ProductId = product.Id } : existing.SingleOrDefault(x => x.Id == v.Id) ?? throw new BusinessException("Biến thể không thuộc sản phẩm");
            if (v.StockQuantity < variant.ReservedQuantity) throw new BusinessException("Không thể giảm tồn kho dưới số lượng đang giữ", 409);
            var difference = v.StockQuantity - variant.StockQuantity;
            variant.SKU = v.SKU; variant.VariantName = v.VariantName; variant.Price = v.Price; variant.SalePrice = v.SalePrice; variant.Attributes = v.Attributes; variant.IsActive = v.IsActive; variant.StockQuantity = v.StockQuantity;
            if (v.Id is null) store.Set<ProductVariant>().Add(variant);
            if (difference != 0) store.Set<InventoryMovement>().Add(new InventoryMovement { VariantId = variant.Id, Quantity = difference, Type = "ADJUSTMENT", CreatedAt = DateTimeOffset.UtcNow });
        }
        store.Set<ProductImage>().RemoveRange(await store.Set<ProductImage>().Where(x => x.ProductId == product.Id).ToListAsync(ct));
        store.Set<ProductImage>().AddRange(input.ImageUrls.Select((url, index) => new ProductImage { ProductId = product.Id, ImageUrl = url, IsPrimary = index == 0, SortOrder = index }));
        await Audit(accountId, id is null ? "CREATE" : "UPDATE", "Product", product.Id, ct); await tx.CommitAsync(ct); return product.Id;
    }

    public async Task<Guid> SaveServiceAsync(Guid accountId, Guid? id, ServiceInput input, CancellationToken ct)
    {
        Text(input.Name, "Tên dịch vụ", 250); Slug(input.Slug, 280); Text(input.Description, "Mô tả", 100000); Url(input.ThumbnailUrl);
        if (input.PricingType is not ("CONSULTATION" or "FIXED" or "FROM") || input.PricingType != "CONSULTATION" && input.PriceFrom is null) throw new BusinessException("Cách tính giá dịch vụ không hợp lệ");
        if (input.PriceFrom is not null) Money(input.PriceFrom.Value);
        await using var tx = await store.BeginTransactionAsync(ct); await BrandAccess(accountId, input.BrandId, ct);
        if (!await store.Set<Brand>().AnyAsync(x => x.Id == input.BrandId, ct)) throw new BusinessException("Thương hiệu không tồn tại");
        var service = id is null ? new Service { BrandId = input.BrandId, CreatedAt = DateTimeOffset.UtcNow } : await store.Set<Service>().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new BusinessException("Không tìm thấy dịch vụ", 404);
        if (id is not null) { await BrandAccess(accountId, service.BrandId, ct); if (service.BrandId != input.BrandId) throw new BusinessException("Không chuyển dịch vụ sang thương hiệu khác"); }
        service.Name = input.Name; service.Slug = input.Slug; service.Description = input.Description; service.ThumbnailUrl = input.ThumbnailUrl; service.PriceFrom = input.PriceFrom; service.PricingType = input.PricingType; service.Status = input.IsActive ? "ACTIVE" : "DRAFT";
        if (id is null) store.Set<Service>().Add(service);
        await Audit(accountId, id is null ? "CREATE" : "UPDATE", "Service", service.Id, ct); await tx.CommitAsync(ct); return service.Id;
    }

    public async Task<Guid> SaveBannerAsync(Guid accountId, Guid? id, BannerInput input, CancellationToken ct)
    {
        await RequireAdmin(accountId, ct); Url(input.ImageUrl, true); Url(input.TargetUrl);
        if (input.EndsAt <= input.StartsAt) throw new BusinessException("Thời gian banner không hợp lệ");
        var banner = id is null ? new Banner() : await store.Set<Banner>().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new BusinessException("Không tìm thấy banner", 404);
        banner.ImageUrl = input.ImageUrl; banner.TargetUrl = input.TargetUrl; banner.StartsAt = input.StartsAt.ToUniversalTime(); banner.EndsAt = input.EndsAt.ToUniversalTime(); banner.IsActive = input.IsActive;
        if (id is null) store.Set<Banner>().Add(banner);
        await Audit(accountId, id is null ? "CREATE" : "UPDATE", "Banner", banner.Id, ct); return banner.Id;
    }

    public async Task<Guid> SaveArticleAsync(Guid accountId, Guid? id, ArticleInput input, CancellationToken ct)
    {
        await RequireAdmin(accountId, ct); Text(input.Title, "Tiêu đề", 250); Slug(input.Slug, 280); Text(input.Summary, "Tóm tắt", 10000); Text(input.Content, "Nội dung", 200000); Url(input.ThumbnailUrl);
        if (input.BrandId is not null && !await store.Set<Brand>().AnyAsync(x => x.Id == input.BrandId, ct)) throw new BusinessException("Thương hiệu không tồn tại");
        var article = id is null ? new Article() : await store.Set<Article>().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new BusinessException("Không tìm thấy bài viết", 404);
        article.Title = input.Title; article.Slug = input.Slug; article.Summary = input.Summary; article.Content = input.Content; article.ThumbnailUrl = input.ThumbnailUrl; article.BrandId = input.BrandId; article.Status = input.IsPublished ? "PUBLISHED" : "DRAFT";
        if (input.IsPublished) article.PublishedAt ??= DateTimeOffset.UtcNow;
        if (id is null) store.Set<Article>().Add(article);
        await Audit(accountId, id is null ? "CREATE" : "UPDATE", "Article", article.Id, ct); return article.Id;
    }

    public async Task SetStoreTestShippingAsync(Guid accountId, Guid storeId, StoreTestShippingInput input, CancellationToken ct)
    {
        await RequireAdmin(accountId, ct);
        await StoreAccess(accountId, storeId, ct);
        if (input.GhnTestShopId <= 0) throw new BusinessException("Mã cửa hàng GHN test không hợp lệ");
        var seller = await store.Set<Store>().SingleAsync(x => x.Id == storeId, ct);
        seller.GhnTestShopId = input.GhnTestShopId;
        await Audit(accountId, "UPDATE_TEST_SHIPPING", "Store", storeId, ct);
    }

    public async Task<Guid> SaveVoucherAsync(Guid accountId, Guid? id, VoucherInput input, CancellationToken ct)
    {
        if (input.StoreId is { } owningStore) await StoreAccess(accountId, owningStore, ct); else await RequireAdmin(accountId, ct);
        if (input.StoreId is not null && (input.PointCost > 0 || input.MinLevel > 1)) await RequireAdmin(accountId, ct);
        Text(input.Code, "Mã voucher", 50); Text(input.Name, "Tên voucher", 150); Text(input.Description, "Mô tả", 10000, false);
        Money(input.DiscountValue); Money(input.MinOrderAmount); if (input.MaxDiscount is not null) Money(input.MaxDiscount.Value);
        if (input.PointCost < 0 || input.MinLevel is < 1 or > 10 || input.PointCost > 0 && input.PerUserLimit != 1) throw new BusinessException("Quy tắc điểm và level của voucher không hợp lệ");
        if (input.DiscountType is not ("FIXED" or "PERCENT" or "SHIPPING") || input.DiscountType == "PERCENT" && input.DiscountValue > 100 || input.DiscountValue <= 0 || input.PerUserLimit < 1 || input.UsageLimit < 1 || input.ExpiresAt <= input.StartsAt || input.Targets is null || input.Targets.Any(x => x is null) || input.Targets.Count > 100) throw new BusinessException("Quy tắc voucher không hợp lệ");
        await using var tx = await store.BeginTransactionAsync(ct);
        var voucher = id is null ? new Voucher() : await store.Set<Voucher>().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new BusinessException("Không tìm thấy voucher", 404);
        if (id is not null && voucher.StoreId != input.StoreId && !await Admin(accountId, ct)) throw new BusinessException("Không có quyền đổi phạm vi voucher", 403);
        if (id is not null && (await store.Set<VoucherUsage>().AnyAsync(x => x.VoucherId == id, ct) || await store.Set<UserVoucher>().AnyAsync(x => x.VoucherId == id, ct))) throw new BusinessException("Voucher đã được nhận hoặc sử dụng. Vui lòng tạo mã mới", 409);
        voucher.Code = input.Code.Trim().ToUpperInvariant(); voucher.Name = input.Name; voucher.Description = input.Description; voucher.DiscountType = input.DiscountType; voucher.DiscountValue = input.DiscountValue; voucher.MaxDiscount = input.MaxDiscount;
        voucher.MinOrderAmount = input.MinOrderAmount; voucher.PerUserLimit = input.PerUserLimit; voucher.UsageLimit = input.UsageLimit; voucher.StartsAt = input.StartsAt.ToUniversalTime(); voucher.ExpiresAt = input.ExpiresAt.ToUniversalTime(); voucher.Status = input.IsActive ? "ACTIVE" : "INACTIVE";
        if (input.StoreId is { } sellerId && (!await store.Set<Store>().AnyAsync(x => x.Id == sellerId && x.StoreType == 1 && x.Status == 1, ct) || input.DiscountType == "SHIPPING")) throw new BusinessException("Voucher cửa hàng không hợp lệ", 409);
        voucher.StoreId = input.StoreId;
        voucher.PointCost = input.PointCost; voucher.MinLevel = input.MinLevel; voucher.IsFlashSale = input.IsFlashSale;
        // Older releases only recognize ACTIVE and cannot enforce point/level restrictions.
        if (input.IsActive && (input.PointCost > 0 || input.MinLevel > 1 || input.DiscountType == "SHIPPING")) voucher.Status = "MEMBER_OFFER";
        if (id is null) store.Set<Voucher>().Add(voucher);
        store.Set<VoucherTarget>().RemoveRange(await store.Set<VoucherTarget>().Where(x => x.VoucherId == voucher.Id).ToListAsync(ct));
        foreach (var target in input.Targets)
        {
            if (target.BrandId is not null && !await store.Set<Brand>().AnyAsync(x => x.Id == target.BrandId, ct) || target.CategoryId is not null && !await store.Set<Category>().AnyAsync(x => x.Id == target.CategoryId, ct) || target.ProductId is not null && !await store.Set<Product>().AnyAsync(x => x.Id == target.ProductId, ct)) throw new BusinessException("Phạm vi voucher không tồn tại");
            store.Set<VoucherTarget>().Add(new VoucherTarget { VoucherId = voucher.Id, BrandId = target.BrandId, CategoryId = target.CategoryId, ProductId = target.ProductId });
        }
        await Audit(accountId, id is null ? "CREATE" : "UPDATE", "Voucher", voucher.Id, ct); await tx.CommitAsync(ct); return voucher.Id;
    }

    public async Task<Guid> QuoteAsync(Guid accountId, QuotationInput input, CancellationToken ct)
    {
        Money(input.Amount); Text(input.Description, "Nội dung báo giá", 50000);
        if (input.ValidUntil <= DateTimeOffset.UtcNow) throw new BusinessException("Báo giá phải còn hiệu lực");
        await using var tx = await store.BeginTransactionAsync(ct);
        var request = await store.Set<ServiceRequest>().SingleOrDefaultAsync(x => x.Id == input.RequestId, ct) ?? throw new BusinessException("Không tìm thấy yêu cầu", 404);
        var brandId = await store.Set<Service>().Where(x => x.Id == request.ServiceId).Select(x => x.BrandId).SingleAsync(ct); await BrandAccess(accountId, brandId, ct);
        var quote = new ServiceQuotation { RequestId = request.Id, QuoteCode = "BG" + Guid.NewGuid().ToString("N")[..16].ToUpperInvariant(), Amount = input.Amount, Description = input.Description, ValidUntil = input.ValidUntil.ToUniversalTime(), Status = "SENT", CreatedAt = DateTimeOffset.UtcNow };
        request.Status = "QUOTED"; store.Set<ServiceQuotation>().Add(quote);
        if (request.UserId is not null) await UserNotifications.AddAsync(store, request.UserId.Value, "QUOTATION", "Bạn có báo giá mới", "Báo giá " + quote.QuoteCode + " đã sẵn sàng để xem.", "Quotation", quote.Id, ct);
        await Audit(accountId, "QUOTE", "ServiceQuotation", quote.Id, ct); await tx.CommitAsync(ct); return quote.Id;
    }

    public async Task ArchiveProductAsync(Guid accountId, Guid id, CancellationToken ct)
    {
        var product = await store.Set<Product>().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new BusinessException("Không tìm thấy sản phẩm", 404);
        await StoreAccess(accountId, product.StoreId ?? throw new BusinessException("Sản phẩm chưa có cửa hàng sở hữu", 409), ct);
        product.IsDeleted = true; product.UpdatedAt = DateTimeOffset.UtcNow; await Audit(accountId, "ARCHIVE", "Product", id, ct);
    }

    public async Task SetRoleAsync(Guid accountId, AccountRoleInput input, CancellationToken ct)
    {
        await using var tx = await store.BeginTransactionAsync(ct);
        if (!await store.Set<AccountRole>().AnyAsync(x => x.AccountId == accountId && x.Role == "SuperAdmin", ct)) throw new BusinessException("Cần quyền SuperAdmin", 403);
        if (input.Role is not ("Customer" or "Seller" or "BrandOwner" or "BrandStaff" or "Admin" or "SuperAdmin")) throw new BusinessException("Vai trò không hợp lệ");
        if (accountId == input.AccountId && input.Role == "SuperAdmin" && !input.Granted) throw new BusinessException("Không thể tự thu hồi quyền SuperAdmin");
        if (input.Role == "SuperAdmin" && !input.Granted)
        {
            var activeSuperAdmins = await (from assignedRole in store.Set<AccountRole>() join account in store.Set<Account>() on assignedRole.AccountId equals account.Id where assignedRole.Role == "SuperAdmin" && account.Status == 1 select assignedRole.Id).CountAsync(ct);
            if (activeSuperAdmins <= 1) throw new BusinessException("Phải giữ ít nhất một SuperAdmin đang hoạt động", 409);
        }
        if (!await store.Set<Account>().AnyAsync(x => x.Id == input.AccountId, ct)) throw new BusinessException("Không tìm thấy tài khoản", 404);
        var role = await store.Set<AccountRole>().SingleOrDefaultAsync(x => x.AccountId == input.AccountId && x.Role == input.Role, ct);
        if (input.Granted && role is null) store.Set<AccountRole>().Add(new AccountRole { AccountId = input.AccountId, Role = input.Role });
        if (!input.Granted && role is not null) store.Set<AccountRole>().Remove(role);
        foreach (var session in await store.Set<RefreshToken>().Where(x => x.AccountId == input.AccountId && x.RevokedAt == null).ToListAsync(ct)) session.RevokedAt = DateTimeOffset.UtcNow;
        await Audit(accountId, input.Granted ? "GRANT_ROLE" : "REVOKE_ROLE", "Account", input.AccountId, ct); await tx.CommitAsync(ct);
    }
}
