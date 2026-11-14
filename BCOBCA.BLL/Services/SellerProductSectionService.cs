using System.Text.Json;
using System.Text.RegularExpressions;
using BCOBCA.BLL.Common;
using BCOBCA.BLL.DTOs;
using BCOBCA.DAL.Entities;
using Microsoft.EntityFrameworkCore;

namespace BCOBCA.BLL.Services;

public sealed partial class SellerService
{
    private static string SectionText(string? value, int max, bool required = false)
    {
        if (value is null || value.Length > max || required && string.IsNullOrWhiteSpace(value))
            throw new BusinessException($"Nội dung không hợp lệ hoặc vượt quá {max} ký tự.");
        return value.Trim();
    }

    public async Task<SellerProductDetailDto> UpdateProductSectionAsync(Guid accountId, Guid storeId, Guid id, string section, SellerProductSectionInput input, CancellationToken ct)
    {
        await using var tx = await store.BeginTransactionAsync(ct);
        await CheckProfileAccess(accountId, storeId, ct);
        var product = await store.Set<Product>().SingleOrDefaultAsync(p => p.Id == id && p.StoreId == storeId && !p.IsDeleted, ct)
            ?? throw new BusinessException("Không tìm thấy sản phẩm của cửa hàng.", 404);
        if (input.ExpectedUpdatedAt == default || input.ExpectedUpdatedAt != product.UpdatedAt)
            throw new BusinessException("Sản phẩm đã thay đổi. Vui lòng tải lại dữ liệu trước khi lưu.", 409);
        switch (section)
        {
            case "basic":
                product.Name = SectionText(input.Name, 250, true);
                if (input.CategoryId is null || input.CategoryId == Guid.Empty) throw new BusinessException("Vui lòng chọn danh mục.");
                if (input.CategoryId != product.CategoryId)
                {
                    var active = CategoryTree.Active(await store.Set<Category>().AsNoTracking().ToListAsync(ct));
                    if (!active.Any(c => c.Id == input.CategoryId)) throw new BusinessException("Danh mục không hoạt động.");
                    var definitions = await ProductSpecificationRules.ResolveDefinitionsAsync(store, input.CategoryId.Value, ct);
                    ProductSpecificationRules.ValidateValues(ProductSpecificationRules.ParseValues(product.Specifications), definitions, "product");
                    product.CategoryId = input.CategoryId.Value;
                }
                break;
            case "description":
                product.Description = SectionText(input.Description, 20000);
                break;
            case "info":
                product.Specifications = JsonSerializer.Serialize(new { free_text = SectionText(input.SpecificationText, 20000) }, ProductSpecificationRules.JsonOptions);
                break;
            case "shipping":
                if (input.ShippingWeightGrams is null or < 1 or > 50000 || input.ShippingLengthCm is null or < 1 or > 200
                    || input.ShippingWidthCm is null or < 1 or > 200 || input.ShippingHeightCm is null or < 1 or > 200)
                    throw new BusinessException("Khối lượng phải từ 1–50.000 g; kích thước từ 1–200 cm.");
                product.ShippingWeightGrams = input.ShippingWeightGrams; product.ShippingLengthCm = input.ShippingLengthCm;
                product.ShippingWidthCm = input.ShippingWidthCm; product.ShippingHeightCm = input.ShippingHeightCm;
                break;
            case "status":
                if (input.IsActive is null) throw new BusinessException("Vui lòng chọn trạng thái hiển thị.");
                product.Status = (short)(input.IsActive.Value ? 1 : 0);
                break;
            case "images":
                var urls = input.ImageUrls?.Select(url => url?.Trim() ?? "").ToArray();
                if (urls is null || urls.Length is < 1 or > 5 || urls.Distinct().Count() != urls.Length
                    || urls.Any(url => url.Length > 2000 || !Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != "https" || uri.UserInfo.Length != 0))
                    throw new BusinessException("Cần 1–5 ảnh HTTPS hợp lệ, không trùng nhau.");
                store.Set<ProductImage>().RemoveRange(await store.Set<ProductImage>().Where(i => i.ProductId == id).ToListAsync(ct));
                store.Set<ProductImage>().AddRange(urls.Select((url, index) => new ProductImage { ProductId = id, ImageUrl = url, IsPrimary = index == 0, SortOrder = index }));
                break;
            case "variants":
            case "stock":
                var rows = input.Variants;
                var existing = await store.Set<ProductVariant>().Where(v => v.ProductId == id).ToListAsync(ct);
                if (rows is null || rows.Count is < 1 or > 100 || rows.Any(r => r is null) || rows.Count != existing.Count
                    || rows.Select(r => r.Id).Distinct().Count() != rows.Count || rows.Any(r => !existing.Any(v => v.Id == r.Id)))
                    throw new BusinessException("Danh sách phân loại đã thay đổi hoặc không thuộc sản phẩm. Vui lòng tải lại.");
                if (section == "variants")
                {
                    var names = rows.Select(r => SectionText(r.Name, 150, true)).ToArray();
                    if (names.Any(name => name.Any(char.IsControl)) || names.Select(name => Regex.Replace(name, @"\s+", " ").Normalize().ToUpperInvariant()).Distinct().Count() != rows.Count)
                        throw new BusinessException("Tên phân loại không hợp lệ hoặc bị trùng.");
                    foreach (var row in rows)
                    {
                        if (row.Price is null or < 1 or > 9007199254740991m || decimal.Truncate(row.Price.Value) != row.Price.Value
                            || row.SalePrice is not null && (row.SalePrice < 1 || row.SalePrice > row.Price || decimal.Truncate(row.SalePrice.Value) != row.SalePrice.Value))
                            throw new BusinessException("Giá phải là số nguyên dương; giá khuyến mãi không vượt giá bán.");
                        var variant = existing.Single(v => v.Id == row.Id);
                        variant.VariantName = row.Name!.Trim(); variant.Price = row.Price.Value; variant.SalePrice = row.SalePrice;
                    }
                }
                else
                {
                    if (rows.Any(r => r.Stock is null or < 0) || rows.Sum(r => (long)r.Stock!.Value) > int.MaxValue)
                        throw new BusinessException("Tồn kho phải là số nguyên không âm; tổng tồn không vượt 2.147.483.647.");
                    foreach (var row in rows)
                    {
                        var variant = existing.Single(v => v.Id == row.Id);
                        if (row.Stock < variant.ReservedQuantity) throw new BusinessException($"Tồn kho của {variant.VariantName} không được thấp hơn số lượng đang giữ ({variant.ReservedQuantity}).", 409);
                        var delta = row.Stock!.Value - variant.StockQuantity;
                        if (delta != 0) store.Set<InventoryMovement>().Add(new InventoryMovement { VariantId = variant.Id, Type = "ADJUSTMENT", Quantity = delta, CreatedAt = DateTimeOffset.UtcNow });
                        variant.StockQuantity = row.Stock.Value;
                    }
                }
                var representative = existing.Where(v => v.IsActive).OrderByDescending(v => v.StockQuantity > v.ReservedQuantity).ThenBy(v => v.SalePrice ?? v.Price).FirstOrDefault();
                if (representative is not null) { product.BasePrice = representative.Price; product.SalePrice = representative.SalePrice; }
                break;
            default:
                throw new BusinessException("Không hỗ trợ chỉnh sửa phần này.", 404);
        }
        // PostgreSQL stores microseconds; keep the version strictly increasing at that precision.
        var now = new DateTimeOffset(DateTimeOffset.UtcNow.UtcTicks / 10 * 10, TimeSpan.Zero);
        product.UpdatedAt = now > product.UpdatedAt ? now : product.UpdatedAt.AddTicks(10);
        store.Set<AuditLog>().Add(new AuditLog { AccountId = accountId, EntityType = "Product", EntityId = id, Action = $"UPDATE_{section.ToUpperInvariant()}", CreatedAt = now });
        await store.SaveChangesAsync(ct);
        var result = await ProductAsync(accountId, storeId, id, ct);
        await tx.CommitAsync(ct);
        return result;
    }
}
