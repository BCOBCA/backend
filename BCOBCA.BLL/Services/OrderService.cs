using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BCOBCA.BLL.Common;
using BCOBCA.BLL.DTOs;
using BCOBCA.BLL.Interfaces;
using BCOBCA.DAL.Entities;
using BCOBCA.DAL.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace BCOBCA.BLL.Services;

public sealed class OrderService(ICommerceStore store, IConfiguration config, IFinanceService finance, IShippingQuoteService shipping) : IOrderService
{
    private static string CartHash(CartDto cart) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(
        cart.Items.Where(x => x.IsSelected).OrderBy(x => x.Id).Select(x => new { x.Id, x.ProductId, x.VariantId, x.Quantity, x.UnitPrice })))));
    public async Task<CartDto> CartAsync(Guid userId, CancellationToken ct)
    {
        var items = await (from c in store.Set<Cart>().AsNoTracking() join i in store.Set<CartItem>() on c.Id equals i.CartId
            join p in store.Set<Product>().IgnoreQueryFilters() on i.ProductId equals p.Id join v in store.Set<ProductVariant>() on i.VariantId equals v.Id
            join b in store.Set<Brand>().IgnoreQueryFilters() on p.BrandId equals b.Id where c.UserId == userId
            orderby i.CreatedAt, i.Id
            select new CartItemDto(i.Id, p.Id, v.Id, b.Id, b.Name, p.Name, v.VariantName,
                store.Set<ProductImage>().Where(x => x.ProductId == p.Id && x.IsPrimary).Select(x => x.ImageUrl).FirstOrDefault() ?? "",
                v.SalePrice ?? v.Price, i.Quantity, v.StockQuantity - v.ReservedQuantity, i.IsSelected, !p.IsDeleted && p.Status == 1 && !b.IsDeleted && b.Status == 1 && v.IsActive && store.Set<Store>().Any(s => s.Id == p.StoreId && s.StoreType == 1 && s.Status == 1 && s.DeletedAt == null), p.StoreId, store.Set<Store>().Where(s => s.Id == p.StoreId).Select(s => s.Name).FirstOrDefault())).ToListAsync(ct);
        return new(items, items.Where(x => x.IsSelected).Sum(x => x.UnitPrice * x.Quantity), items.Where(x => x.IsSelected).Sum(x => x.Quantity));
    }

    public async Task AddAsync(Guid userId, CartItemRequest input, CancellationToken ct)
    {
        if (input.Quantity is < 1 or > 999) throw new BusinessException("Số lượng phải từ 1 đến 999");
        await using var tx = await store.BeginTransactionAsync(ct);
        var cart = await store.Set<Cart>().SingleAsync(x => x.UserId == userId, ct);
        var variant = await store.Set<ProductVariant>().SingleOrDefaultAsync(x => x.Id == input.VariantId && x.ProductId == input.ProductId && x.IsActive, ct) ?? throw new BusinessException("Phân loại không khả dụng", 404);
        if (!await ProductCatalogScope.Available(store).AnyAsync(x => x.Id == input.ProductId, ct)) throw new BusinessException("Sản phẩm không khả dụng", 404);
        var item = await store.Set<CartItem>().SingleOrDefaultAsync(x => x.CartId == cart.Id && x.VariantId == variant.Id, ct);
        var quantity = input.Quantity + (item?.Quantity ?? 0);
        if (quantity > 999 || quantity > variant.StockQuantity - variant.ReservedQuantity) throw new BusinessException("Không đủ tồn kho", 409);
        if (item is null) store.Set<CartItem>().Add(new CartItem { CartId = cart.Id, ProductId = input.ProductId, VariantId = variant.Id, Quantity = quantity, IsSelected = true, CreatedAt = DateTimeOffset.UtcNow });
        else item.Quantity = quantity;
        cart.UpdatedAt = DateTimeOffset.UtcNow;
        await store.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }

    private async Task<CartItem> OwnedItem(Guid userId, Guid itemId, CancellationToken ct)
        => await (from i in store.Set<CartItem>() join c in store.Set<Cart>() on i.CartId equals c.Id where i.Id == itemId && c.UserId == userId select i).SingleOrDefaultAsync(ct) ?? throw new BusinessException("Không tìm thấy sản phẩm trong giỏ", 404);

    public async Task UpdateAsync(Guid userId, Guid itemId, UpdateCartRequest input, CancellationToken ct)
    {
        if (input.Quantity is < 1 or > 999) throw new BusinessException("Số lượng phải từ 1 đến 999");
        var item = await OwnedItem(userId, itemId, ct);
        var variant = await store.Set<ProductVariant>().SingleAsync(x => x.Id == item.VariantId, ct);
        if (input.IsSelected && input.Quantity > variant.StockQuantity - variant.ReservedQuantity) throw new BusinessException("Không đủ tồn kho", 409);
        item.Quantity = input.Quantity; item.IsSelected = input.IsSelected;
        await store.SaveChangesAsync(ct);
    }

    public async Task RemoveAsync(Guid userId, Guid itemId, CancellationToken ct)
    {
        store.Set<CartItem>().Remove(await OwnedItem(userId, itemId, ct)); await store.SaveChangesAsync(ct);
    }

    public async Task<OrderDto> CheckoutAsync(Guid userId, string idempotencyKey, CheckoutRequest input, CancellationToken ct, string? affiliateVisitToken = null)
    {
        CheckoutVoucherRules.NormalizeCodes(input.VoucherCode, input.VoucherCodes, false);
        if (input.AffiliateVisitToken is not null && input.AffiliateVisitToken.Length != 96) throw new BusinessException("Nguồn Affiliate không hợp lệ");
        if (string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Length > 100 || input.Note.Length > 2000 || input.VoucherCode?.Length > 50 || input.ExpectedCartHash?.Length > 64) throw new BusinessException("Mã yêu cầu, voucher hoặc ghi chú không hợp lệ", 400);
        if (input.PaymentMethod != "COD") throw new BusinessException("Phương thức thanh toán chưa được cấu hình", 409);
        var requestHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(input))));
        await using var tx = await store.BeginTransactionAsync(ct);
        var existing = await store.Set<Order>().SingleOrDefaultAsync(x => x.UserId == userId && x.IdempotencyKey == idempotencyKey, ct);
        if (existing is not null)
        {
            if (existing.RequestHash != requestHash) throw new BusinessException("Mã yêu cầu đã được dùng cho nội dung khác", 409);
            return await CheckoutResultAsync(userId, existing, ct);
        }
        var address = await store.Set<UserAddress>().SingleOrDefaultAsync(x => x.Id == input.AddressId && x.UserId == userId, ct) ?? throw new BusinessException("Địa chỉ giao hàng không hợp lệ", 404);
        if (input.ExpectedCartHash is not null && input.ExpectedCartHash != CartHash(await CartAsync(userId, ct))) throw new BusinessException("Giỏ hàng đã thay đổi. Vui lòng xem lại trước khi đặt hàng", 409);
        var cartId = await store.Set<Cart>().Where(x => x.UserId == userId).Select(x => x.Id).SingleAsync(ct);
        var cartItems = await store.Set<CartItem>().Where(x => x.CartId == cartId && x.IsSelected).OrderBy(x => x.Id).ToListAsync(ct);
        if (cartItems.Count == 0) throw new BusinessException("Chưa chọn sản phẩm");
        var now = DateTimeOffset.UtcNow;
        var order = new Order { UserId = userId, OrderCode = "BCA" + Guid.NewGuid().ToString("N")[..20].ToUpperInvariant(), Status = "PENDING", PaymentStatus = "UNPAID", RecipientName = address.RecipientName, RecipientPhone = address.Phone,
            ShippingAddress = $"{address.AddressLine}, {address.WardCode}, {address.ProvinceCode}", Note = input.Note, IdempotencyKey = idempotencyKey, RequestHash = requestHash, CreatedAt = now };
        var lines = new List<OrderItem>();
        foreach (var item in cartItems)
        {
            var product = await store.Set<Product>().SingleOrDefaultAsync(x => x.Id == item.ProductId && x.Status == 1, ct) ?? throw new BusinessException("Sản phẩm không còn khả dụng", 409);
            if (!await store.Set<Brand>().AnyAsync(x => x.Id == product.BrandId && x.Status == 1, ct)) throw new BusinessException("Thương hiệu không khả dụng", 409);
            if (!await store.Set<Store>().AnyAsync(s => s.Id == product.StoreId && s.StoreType == 1 && s.Status == 1 && s.DeletedAt == null, ct)) throw new BusinessException("Cửa hàng không còn khả dụng", 409);
            var variant = await store.Set<ProductVariant>().SingleOrDefaultAsync(x => x.Id == item.VariantId && x.ProductId == product.Id && x.IsActive, ct) ?? throw new BusinessException("Phân loại không khả dụng", 409);
            if (item.Quantity > variant.StockQuantity - variant.ReservedQuantity) throw new BusinessException("Không đủ tồn kho", 409);
            var unitPrice = variant.SalePrice ?? variant.Price;
            if (unitPrice < 0) throw new BusinessException("Giá sản phẩm không hợp lệ", 409);
            variant.ReservedQuantity += item.Quantity;
            lines.Add(new OrderItem { OrderId = order.Id, ProductId = product.Id, VariantId = variant.Id, BrandId = product.BrandId, StoreId = product.StoreId, ProductName = product.Name, SKU = variant.SKU, Quantity = item.Quantity, UnitPrice = unitPrice, TotalPrice = unitPrice * item.Quantity });
        }
        order.Subtotal = lines.Sum(x => x.TotalPrice);
        var pricing = await ApplyVouchersAsync(order, lines, address, input.VoucherCode, input.VoucherCodes, false, ct);
        var voucherSelection = pricing.Vouchers;
        var totalAmount = order.Subtotal - order.DiscountAmount + order.ShippingFee - order.ShippingDiscountAmount;
        if (input.ExpectedTotal is not null && input.ExpectedTotal != totalAmount) throw new BusinessException("Tổng tiền đã thay đổi. Vui lòng xem lại trước khi đặt hàng", 409);
        var allocations = StoreSplitRules.Summarize(lines, pricing.Rates.ToDictionary(x => x.Key, x => x.Value.Fee), voucherSelection.ShippingDiscounts);
        var checkoutGroupId = order.Id;
        var children = new List<Order>();
        foreach (var amounts in allocations)
        {
            var child = children.Count == 0 ? order : new Order
            {
                UserId = userId, OrderCode = "BCA" + Guid.NewGuid().ToString("N")[..20].ToUpperInvariant(), Status = "PENDING", PaymentStatus = "UNPAID",
                RecipientName = address.RecipientName, RecipientPhone = address.Phone, ShippingAddress = order.ShippingAddress,
                Note = input.Note, IdempotencyKey = "child:" + Guid.NewGuid().ToString("N"), RequestHash = requestHash, CreatedAt = now
            };
            child.CheckoutGroupId = checkoutGroupId;
            child.Subtotal = amounts.Subtotal; child.DiscountAmount = amounts.DiscountAmount;
            child.ShippingFee = amounts.ShippingFee; child.ShippingDiscountAmount = amounts.ShippingDiscountAmount; child.TotalAmount = amounts.TotalAmount;
            var childLines = lines.Where(x => x.StoreId == amounts.StoreId).ToList();
            foreach (var line in childLines)
            {
                line.OrderId = child.Id;
                store.Set<InventoryReservation>().Add(new InventoryReservation { OrderId = child.Id, VariantId = line.VariantId!.Value, Quantity = line.Quantity, Status = "HELD", ExpiresAt = now.AddHours(config.GetValue<int>("Checkout:ReservationHours")), CreatedAt = now });
            }
            store.Set<Order>().Add(child);
            await AffiliateSources.CaptureAsync(store, child, childLines, affiliateVisitToken ?? input.AffiliateVisitToken, ct);
            foreach (var group in childLines.GroupBy(x => x.BrandId)) store.Set<SellerOrder>().Add(new SellerOrder { OrderId = child.Id, BrandId = group.Key, StoreId = amounts.StoreId, Status = "PENDING", Subtotal = group.Sum(x => x.UnitPrice * x.Quantity), DiscountAmount = group.Sum(x => x.DiscountAmount), TotalAmount = group.Sum(x => x.TotalPrice), CreatedAt = now });
            store.Set<Payment>().Add(new Payment { OrderId = child.Id, PaymentMethod = "COD", Amount = child.TotalAmount, Currency = "VND", Status = "PENDING", CreatedAt = now });
            store.Set<OrderStatusHistory>().Add(new OrderStatusHistory { OrderId = child.Id, OldStatus = "", NewStatus = "PENDING", Note = "Tạo đơn hàng", CreatedAt = now });
            await UserNotifications.AddAsync(store, userId, "ORDER_CREATED", "Đã tạo đơn hàng " + child.OrderCode, "Đơn hàng của bạn đang chờ xác nhận.", "Order", child.Id, ct);
            children.Add(child);
        }
        foreach (var appliedVoucher in voucherSelection.Applied)
        {
            var voucher = appliedVoucher.Voucher;
            var voucherDiscount = appliedVoucher.Option.DiscountAmount;
            store.Set<VoucherUsage>().Add(new VoucherUsage { VoucherId = voucher.Id, UserId = order.UserId, OrderId = order.Id, DiscountAmount = voucherDiscount, Status = "APPLIED", CreatedAt = now });
            foreach (var child in children)
            {
                var childStore = lines.First(x => x.OrderId == child.Id).StoreId!.Value;
                var allocated = voucher.DiscountType == "SHIPPING" ? child.ShippingDiscountAmount : appliedVoucher.Option.AppliedStoreId == childStore ? voucherDiscount : 0;
                if (allocated > 0) store.Set<OrderVoucherApplication>().Add(new OrderVoucherApplication { OrderId = child.Id, VoucherId = voucher.Id, DiscountAmount = allocated });
            }
            var userVoucher = await store.Set<UserVoucher>().SingleOrDefaultAsync(x => x.UserId == userId && x.VoucherId == voucher.Id, ct);
            if (userVoucher is null) { userVoucher = new UserVoucher { UserId = userId, VoucherId = voucher.Id, ClaimedAt = now }; store.Set<UserVoucher>().Add(userVoucher); }
            var usedCount = await store.Set<VoucherUsage>().CountAsync(x => x.UserId == userId && x.VoucherId == voucher.Id && x.Status == "APPLIED", ct);
            userVoucher.Status = usedCount + 1 >= voucher.PerUserLimit ? "USED" : "AVAILABLE"; userVoucher.UsedAt = now;
        }
        store.Set<OrderItem>().AddRange(lines);
        store.Set<CartItem>().RemoveRange(cartItems);
        await store.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return await CheckoutResultAsync(userId, order, ct);
    }

    public async Task<CheckoutPreviewDto> PreviewAsync(Guid userId, CheckoutPreviewRequest input, CancellationToken ct)
    {
        CheckoutVoucherRules.NormalizeCodes(input.VoucherCode, input.VoucherCodes, input.AutoSelectVouchers);
        var cart = await CartAsync(userId, ct);
        var selected = cart.Items.Where(x => x.IsSelected).ToList();
        if (selected.Count == 0) throw new BusinessException("Chưa chọn sản phẩm");
        if (selected.Any(x => !x.IsAvailable || x.Quantity > x.AvailableStock)) throw new BusinessException("Sản phẩm không còn khả dụng hoặc không đủ tồn kho", 409);
        var addressQuery = store.Set<UserAddress>().AsNoTracking().Where(x => x.UserId == userId);
        var address = input.AddressId is { } addressId ? await addressQuery.SingleOrDefaultAsync(x => x.Id == addressId, ct) ?? throw new BusinessException("Địa chỉ giao hàng không hợp lệ", 404)
            : await addressQuery.OrderByDescending(x => x.IsDefault).ThenBy(x => x.Id).FirstOrDefaultAsync(ct);
        var order = new Order { UserId = userId, Subtotal = cart.SelectedTotal };
        var lines = selected.Select(x => new OrderItem { ProductId = x.ProductId, VariantId = x.VariantId, BrandId = x.BrandId, StoreId = x.StoreId, Quantity = x.Quantity, UnitPrice = x.UnitPrice, TotalPrice = x.UnitPrice * x.Quantity }).ToList();
        var pricing = await ApplyVouchersAsync(order, lines, address, input.VoucherCode, input.VoucherCodes, input.AutoSelectVouchers, ct);
        var selection = pricing.Vouchers;
        var amounts = StoreSplitRules.Summarize(lines, pricing.Rates.ToDictionary(x => x.Key, x => x.Value.Fee), selection.ShippingDiscounts);
        var stores = amounts.Select(x => new CheckoutStoreOrderDto(x.StoreId, selected.First(i => i.StoreId == x.StoreId).StoreName ?? "Cửa hàng", x.Subtotal, x.DiscountAmount, x.ShippingFee, x.ShippingDiscountAmount, x.TotalAmount,
            selection.Applied.Where(v => v.Voucher.StoreId == x.StoreId).Sum(v => v.Option.DiscountAmount), selection.Applied.Where(v => v.Voucher.StoreId == null && v.Voucher.DiscountType != "SHIPPING" && v.Option.AppliedStoreId == x.StoreId).Sum(v => v.Option.DiscountAmount),
            selection.Applied.FirstOrDefault(v => v.Voucher.StoreId == x.StoreId)?.Voucher.Code, pricing.Rates[x.StoreId].ExpectedDeliveryAt)).ToArray();
        return new(order.Subtotal, order.DiscountAmount, order.ShippingFee, order.Subtotal - order.DiscountAmount + order.ShippingFee - order.ShippingDiscountAmount, cart.SelectedCount, CartHash(cart), order.ShippingDiscountAmount, selection.Options, selection.Applied.Select(x => x.Voucher.Code).ToArray(), stores,
            selection.Applied.Where(x => x.Voucher.StoreId != null).Sum(x => x.Option.DiscountAmount), selection.Applied.Where(x => x.Voucher.StoreId == null && x.Voucher.DiscountType != "SHIPPING").Sum(x => x.Option.DiscountAmount));
    }

    private sealed record CheckoutPricing(StoreVoucherSelection Vouchers, IReadOnlyDictionary<Guid, ShippingRate> Rates);

    private async Task<CheckoutPricing> ApplyVouchersAsync(Order order, List<OrderItem> lines, UserAddress? address, string? legacyCode, IReadOnlyList<string>? voucherCodes, bool autoSelect, CancellationToken ct)
    {
        var codes = CheckoutVoucherRules.NormalizeCodes(legacyCode, voucherCodes, autoSelect);
        var collected = voucherCodes is not null || autoSelect;
        var now = DateTimeOffset.UtcNow;
        var owned = await store.Set<UserVoucher>().AsNoTracking().Where(x => x.UserId == order.UserId && x.Status == "AVAILABLE").Select(x => x.VoucherId).ToListAsync(ct);
        var vouchers = await store.Set<Voucher>().AsNoTracking().Where(x => owned.Contains(x.Id) || !collected && codes.Contains(x.Code)).OrderBy(x => x.ExpiresAt).ThenBy(x => x.Code).ToListAsync(ct);
        var ids = vouchers.Select(x => x.Id).ToArray();
        var targets = await store.Set<VoucherTarget>().AsNoTracking().Where(x => ids.Contains(x.VoucherId)).ToListAsync(ct);
        var usages = store.Set<VoucherUsage>().AsNoTracking().Where(x => ids.Contains(x.VoucherId) && x.Status == "APPLIED");
        var totalUses = await usages.GroupBy(x => x.VoucherId).Select(g => new { Id = g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Id, x => x.Count, ct);
        var userUses = await usages.Where(x => x.UserId == order.UserId).GroupBy(x => x.VoucherId).Select(g => new { Id = g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Id, x => x.Count, ct);
        var level = 1;
        if (vouchers.Any(x => x.MinLevel > 1))
        {
            var earned = await store.Set<UserMembership>().Where(x => x.UserId == order.UserId).Select(x => x.LifetimeEarnedPoints).SingleAsync(ct);
            level = MembershipLevelRules.Calculate(earned, await LoyaltyQueries.MembershipLevelsAsync(store, ct)).Level;
        }
        var productIds = lines.Select(x => x.ProductId).ToArray();
        var categories = await store.Set<Product>().Where(x => productIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.CategoryId, ct);
        var contexts = vouchers.Select(v => new CheckoutVoucherContext(v, targets.Where(t => t.VoucherId == v.Id).ToArray(), totalUses.GetValueOrDefault(v.Id), userUses.GetValueOrDefault(v.Id), owned.Contains(v.Id))).ToArray();
        var preliminary = lines.Select(x => new OrderItem { ProductId = x.ProductId, VariantId = x.VariantId, BrandId = x.BrandId, StoreId = x.StoreId, ProductName = x.ProductName, Quantity = x.Quantity, UnitPrice = x.UnitPrice, TotalPrice = x.UnitPrice * x.Quantity }).ToArray();
        var zeroFees = lines.Select(x => x.StoreId!.Value).Distinct().ToDictionary(id => id, _ => 0m);
        var merchandiseContexts = contexts.Where(x => x.Voucher.DiscountType != "SHIPPING").ToArray();
        var merchandiseCodes = codes.Where(code => !contexts.Any(x => x.Voucher.Code == code && x.Voucher.DiscountType == "SHIPPING")).ToArray();
        StoreVoucherPricing.Apply(preliminary, merchandiseContexts, categories, zeroFees, level, merchandiseCodes, autoSelect, collected, now);
        var rates = await shipping.QuoteAsync(address, preliminary, ct);
        var result = StoreVoucherPricing.Apply(lines, contexts, categories, rates.ToDictionary(x => x.Key, x => x.Value.Fee), level, codes, autoSelect, collected, now);
        order.DiscountAmount = lines.Sum(x => x.DiscountAmount); order.ShippingFee = rates.Values.Sum(x => x.Fee); order.ShippingDiscountAmount = result.ShippingDiscounts.Values.Sum();
        return new(result, rates);
    }

    public async Task<PageResult<OrderSummaryDto>> OrdersAsync(Guid userId, CatalogQuery query, CancellationToken ct)
    {
        var (page, size) = CatalogService.Paging(query);
        var source = store.Set<Order>().AsNoTracking().Where(x => x.UserId == userId);
        if (query.CheckoutGroupId is not null) source = source.Where(x => x.CheckoutGroupId == query.CheckoutGroupId);
        if (query.Status is not null)
        {
            if (query.Status is not ("PENDING" or "CONFIRMED" or "READY" or "SHIPPING" or "COMPLETED" or "CANCELLED" or "REFUNDED")) throw new BusinessException("Trạng thái không hợp lệ", 400);
            source = source.Where(x => x.Status == query.Status);
        }
        var summaries = await Summaries(source.OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id).Skip((page - 1) * size).Take(size)).ToListAsync(ct);
        var orderIds = summaries.Select(x => x.Id).ToArray();
        var lines = await store.Set<OrderItem>().AsNoTracking().Where(x => orderIds.Contains(x.OrderId)).OrderBy(x => x.Id).Select(x => new { x.OrderId, Item = new OrderItemDto(x.Id, x.ProductId, x.BrandId, x.ProductName, x.SKU, x.Quantity, x.UnitPrice, x.DiscountAmount, x.TotalPrice, x.StoreId,
            store.Set<Store>().IgnoreQueryFilters().Where(s => s.Id == x.StoreId).Select(s => s.Name).FirstOrDefault(),
            store.Set<Store>().Where(s => s.Id == x.StoreId && s.StoreType == 1 && s.Status == 1 && s.DeletedAt == null).Select(s => s.Slug).FirstOrDefault(),
            store.Set<ProductImage>().Where(i => i.ProductId == x.ProductId).OrderByDescending(i => i.IsPrimary).ThenBy(i => i.SortOrder).ThenBy(i => i.Id).Select(i => i.ImageUrl).FirstOrDefault(),
            store.Set<ProductVariant>().Where(v => v.Id == x.VariantId).Select(v => v.VariantName).FirstOrDefault(),
            store.Set<Brand>().IgnoreQueryFilters().Where(b => b.Id == x.BrandId).Select(b => b.Name).FirstOrDefault()) }).ToListAsync(ct);
        var grouped = lines.ToLookup(x => x.OrderId, x => x.Item);
        return new(summaries.Select(x => x with { Items = grouped[x.Id].ToArray() }).ToArray(), page, size, await source.CountAsync(ct));
    }

    private IQueryable<OrderSummaryDto> Summaries(IQueryable<Order> source) => source.Select(x => new OrderSummaryDto(x.Id, x.OrderCode, x.Status, x.PaymentStatus, x.TotalAmount, x.CreatedAt,
        (from line in store.Set<OrderItem>() join seller in store.Set<Store>().IgnoreQueryFilters() on line.StoreId equals seller.Id where line.OrderId == x.Id orderby line.Id select seller.Name).FirstOrDefault()));

    private async Task<OrderDto> CheckoutResultAsync(Guid userId, Order order, CancellationToken ct)
    {
        var detail = await OrderAsync(userId, order.Id, ct);
        if (order.CheckoutGroupId is null) return detail;
        var children = await Summaries(store.Set<Order>().AsNoTracking().Where(x => x.UserId == userId && x.CheckoutGroupId == order.CheckoutGroupId).OrderBy(x => x.Id)).ToArrayAsync(ct);
        return detail with { CheckoutOrders = children };
    }

    public async Task<OrderDto> OrderAsync(Guid userId, Guid id, CancellationToken ct)
    {
        var order = await store.Set<Order>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.UserId == userId, ct) ?? throw new BusinessException("Không tìm thấy đơn hàng", 404);
        var items = await store.Set<OrderItem>().AsNoTracking().Where(x => x.OrderId == id).OrderBy(x => x.Id).Select(x => new OrderItemDto(x.Id, x.ProductId, x.BrandId, x.ProductName, x.SKU, x.Quantity, x.UnitPrice, x.DiscountAmount, x.TotalPrice, x.StoreId,
            store.Set<Store>().IgnoreQueryFilters().Where(s => s.Id == x.StoreId).Select(s => s.Name).FirstOrDefault(),
            store.Set<Store>().Where(s => s.Id == x.StoreId && s.StoreType == 1 && s.Status == 1 && s.DeletedAt == null).Select(s => s.Slug).FirstOrDefault(),
            store.Set<ProductImage>().Where(i => i.ProductId == x.ProductId).OrderByDescending(i => i.IsPrimary).ThenBy(i => i.SortOrder).ThenBy(i => i.Id).Select(i => i.ImageUrl).FirstOrDefault(),
            store.Set<ProductVariant>().Where(v => v.Id == x.VariantId).Select(v => v.VariantName).FirstOrDefault(),
            store.Set<Brand>().IgnoreQueryFilters().Where(b => b.Id == x.BrandId).Select(b => b.Name).FirstOrDefault())).ToListAsync(ct);
        var shipments = await store.Set<Shipment>().AsNoTracking().Where(x => x.OrderId == id).OrderBy(x => x.ShippedAt).Take(50).Select(x => new ShipmentDto(x.Id, x.Carrier, x.TrackingCode, x.Status, x.ShippedAt, x.DeliveredAt)).ToListAsync(ct);
        var history = await store.Set<OrderStatusHistory>().AsNoTracking().Where(x => x.OrderId == id).OrderBy(x => x.CreatedAt).Select(x => new OrderHistoryDto(x.OldStatus, x.NewStatus, x.Note, x.CreatedAt)).ToListAsync(ct);
        var paymentMethod = await store.Set<Payment>().AsNoTracking().Where(x => x.OrderId == id).Select(x => x.PaymentMethod).FirstOrDefaultAsync(ct);
        return new(order.Id, order.OrderCode, order.Status, order.PaymentStatus, order.Subtotal, order.DiscountAmount, order.ShippingFee, order.TotalAmount, order.RecipientName, order.RecipientPhone, order.ShippingAddress, order.CreatedAt, items, shipments, history, order.ShippingDiscountAmount, paymentMethod, order.CheckoutGroupId);
    }

    public async Task<OrderDto?> FindRequestAsync(Guid userId, string key, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(key) || key.Length > 100) throw new BusinessException("Mã yêu cầu không hợp lệ", 400);
        var order = await store.Set<Order>().SingleOrDefaultAsync(x => x.UserId == userId && x.IdempotencyKey == key, ct);
        return order is null ? null : await CheckoutResultAsync(userId, order, ct);
    }

    public async Task CancelAsync(Guid userId, Guid id, CancellationToken ct)
    {
        var order = await store.Set<Order>().SingleOrDefaultAsync(x => x.Id == id && x.UserId == userId, ct) ?? throw new BusinessException("Không tìm thấy đơn hàng", 404);
        if (order.Status == "CANCELLED") return;
        if (order.Status != "PENDING" || order.PaymentStatus != "UNPAID") throw new BusinessException("Đơn hàng không thể tự hủy ở trạng thái hiện tại", 409);
        await ChangeAsync(order, "CANCELLED", null, false, ct);
    }

    public async Task TransitionAsync(Guid accountId, Guid orderId, OrderStatusRequest input, CancellationToken ct)
    {
        if (!await store.Set<AccountRole>().AnyAsync(x => x.AccountId == accountId && (x.Role == "Admin" || x.Role == "SuperAdmin"), ct)) throw new BusinessException("Không có quyền quản lý đơn hàng", 403);
        var order = await store.Set<Order>().SingleOrDefaultAsync(x => x.Id == orderId, ct) ?? throw new BusinessException("Không tìm thấy đơn hàng", 404);
        await ChangeAsync(order, input.Status, accountId, input.ConfirmCodCollected, ct);
    }

    public async Task<int> ExpireOneReservationAsync(CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var order = await store.Set<Order>().Where(o => o.Status == "PENDING" && o.PaymentStatus == "UNPAID"
            && store.Set<InventoryReservation>().Any(r => r.OrderId == o.Id && r.Status == "HELD" && r.ExpiresAt <= now))
            .OrderBy(o => o.CreatedAt).FirstOrDefaultAsync(ct);
        if (order is null) return 0;
        await ChangeAsync(order, "CANCELLED", null, false, ct);
        return 1;
    }

    private async Task ChangeAsync(Order order, string status, Guid? accountId, bool confirmCod, CancellationToken ct)
    {
        if (!CommerceRules.CanTransition(order.Status, status)) throw new BusinessException("Chuyển trạng thái không hợp lệ", 409);
        await using var tx = await store.BeginTransactionAsync(ct);
        var now = DateTimeOffset.UtcNow;
        if (status == "CANCELLED" && order.PaymentStatus != "UNPAID") throw new BusinessException("Cần quy trình hoàn tiền cho đơn đã thanh toán", 409);
        if (status is "CANCELLED" or "COMPLETED")
        {
            if (status == "COMPLETED")
            {
                var payment = await store.Set<Payment>().SingleAsync(x => x.OrderId == order.Id, ct);
                if (payment.PaymentMethod != "COD" || !confirmCod) throw new BusinessException("Cần xác nhận đã thu tiền COD", 409);
                payment.Status = "PAID"; payment.PaidAt = now; order.PaymentStatus = "PAID"; order.CompletedAt = now;
                store.Set<PaymentTransaction>().Add(new PaymentTransaction { PaymentId = payment.Id, Provider = "COD", TransactionCode = order.OrderCode, EventId = "cod:" + order.Id, Amount = payment.Amount, Status = "SUCCESS", ResponseData = "{}", CreatedAt = now });
            }
            foreach (var reservation in await store.Set<InventoryReservation>().Where(x => x.OrderId == order.Id && x.Status == "HELD").ToListAsync(ct))
            {
                var variant = await store.Set<ProductVariant>().SingleAsync(x => x.Id == reservation.VariantId, ct);
                variant.ReservedQuantity -= reservation.Quantity;
                if (status == "COMPLETED") { variant.StockQuantity -= reservation.Quantity; store.Set<InventoryMovement>().Add(new InventoryMovement { VariantId = variant.Id, OrderId = order.Id, Quantity = -reservation.Quantity, Type = "SALE", CreatedAt = now }); }
                reservation.Status = status == "COMPLETED" ? "CONSUMED" : "RELEASED";
            }
            if (status == "CANCELLED")
            {
                order.CancelledAt = now;
                var relatedIds = order.CheckoutGroupId is null ? new[] { order.Id }
                    : await store.Set<Order>().Where(x => x.UserId == order.UserId && x.CheckoutGroupId == order.CheckoutGroupId).Select(x => x.Id).ToArrayAsync(ct);
                foreach (var usage in await store.Set<VoucherUsage>().Where(x => relatedIds.Contains(x.OrderId) && x.Status == "APPLIED").ToListAsync(ct))
                {
                    var stillApplied = await (from application in store.Set<OrderVoucherApplication>() join sibling in store.Set<Order>() on application.OrderId equals sibling.Id
                        where application.VoucherId == usage.VoucherId && relatedIds.Contains(sibling.Id) && sibling.Id != order.Id && sibling.Status != "CANCELLED" && application.DiscountAmount > 0
                        select application.Id).AnyAsync(ct);
                    if (stillApplied) continue;
                    usage.Status = "RELEASED";
                    var userVoucher = await store.Set<UserVoucher>().SingleOrDefaultAsync(x => x.UserId == order.UserId && x.VoucherId == usage.VoucherId, ct);
                    if (userVoucher is not null)
                    {
                        var limit = await store.Set<Voucher>().Where(x => x.Id == usage.VoucherId).Select(x => x.PerUserLimit).SingleAsync(ct);
                        var otherUsages = store.Set<VoucherUsage>().Where(x => x.UserId == order.UserId && x.VoucherId == usage.VoucherId && x.Id != usage.Id && x.Status == "APPLIED");
                        userVoucher.Status = await otherUsages.CountAsync(ct) >= limit ? "USED" : "AVAILABLE";
                        userVoucher.UsedAt = await otherUsages.Select(x => (DateTimeOffset?)x.CreatedAt).MaxAsync(ct);
                    }
                }
            }
        }
        if (status == "CONFIRMED") order.ConfirmedAt = now;
        if (status is "COMPLETED" or "CANCELLED")
            foreach (var shipment in await store.Set<Shipment>().Where(x => x.OrderId == order.Id && x.Status != "CANCELLED").ToListAsync(ct)) { shipment.Status = status == "COMPLETED" ? "DELIVERED" : "CANCELLED"; if (status == "COMPLETED") shipment.DeliveredAt = now; }
        store.Set<OrderStatusHistory>().Add(new OrderStatusHistory { OrderId = order.Id, OldStatus = order.Status, NewStatus = status, ChangedByAccountId = accountId, CreatedAt = now });
        order.Status = status;
        var label = status switch { "CONFIRMED" => "Đã xác nhận", "READY" => "Sẵn sàng giao", "SHIPPING" => "Đang giao", "COMPLETED" => "Hoàn thành", "CANCELLED" => "Đã hủy", _ => status };
        await UserNotifications.AddAsync(store, order.UserId, "ORDER_STATUS", "Đơn hàng " + order.OrderCode, label, "Order", order.Id, ct);
        foreach (var seller in await store.Set<SellerOrder>().Where(x => x.OrderId == order.Id).ToListAsync(ct)) seller.Status = status;
        await store.SaveChangesAsync(ct);
        if (status == "COMPLETED") await finance.AccrueOrderAsync(order.Id, ct);
        await tx.CommitAsync(ct);
    }
}
