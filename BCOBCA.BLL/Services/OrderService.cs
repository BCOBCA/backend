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

public sealed class OrderService(ICommerceStore store, IConfiguration config, IFinanceService finance) : IOrderService
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
            return await OrderAsync(userId, existing.Id, ct);
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
            store.Set<InventoryReservation>().Add(new InventoryReservation { OrderId = order.Id, VariantId = variant.Id, Quantity = item.Quantity, Status = "HELD", ExpiresAt = now.AddHours(config.GetValue<int>("Checkout:ReservationHours")), CreatedAt = now });
        }
        order.Subtotal = lines.Sum(x => x.TotalPrice);
        order.ShippingFee = config.GetValue<decimal>("Checkout:ShippingFee");
        if (order.ShippingFee < 0) throw new InvalidOperationException("Shipping fee must not be negative.");
        var voucherSelection = await ApplyVouchersAsync(order, lines, input.VoucherCode, input.VoucherCodes, false, ct);
        foreach (var voucher in voucherSelection.Applied)
        {
            var voucherDiscount = voucher.DiscountType == "SHIPPING" ? order.ShippingDiscountAmount : order.DiscountAmount;
            store.Set<VoucherUsage>().Add(new VoucherUsage { VoucherId = voucher.Id, UserId = order.UserId, OrderId = order.Id, DiscountAmount = voucherDiscount, Status = "APPLIED", CreatedAt = now });
            store.Set<OrderVoucherApplication>().Add(new OrderVoucherApplication { OrderId = order.Id, VoucherId = voucher.Id, DiscountAmount = voucherDiscount });
            var userVoucher = await store.Set<UserVoucher>().SingleOrDefaultAsync(x => x.UserId == userId && x.VoucherId == voucher.Id, ct);
            if (userVoucher is null) { userVoucher = new UserVoucher { UserId = userId, VoucherId = voucher.Id, ClaimedAt = now }; store.Set<UserVoucher>().Add(userVoucher); }
            var usedCount = await store.Set<VoucherUsage>().CountAsync(x => x.UserId == userId && x.VoucherId == voucher.Id && x.Status == "APPLIED", ct);
            userVoucher.Status = usedCount + 1 >= voucher.PerUserLimit ? "USED" : "AVAILABLE"; userVoucher.UsedAt = now;
        }
        order.TotalAmount = order.Subtotal - order.DiscountAmount + order.ShippingFee - order.ShippingDiscountAmount;
        if (input.ExpectedTotal is not null && input.ExpectedTotal != order.TotalAmount) throw new BusinessException("Tổng tiền đã thay đổi. Vui lòng xem lại trước khi đặt hàng", 409);
        store.Set<Order>().Add(order); store.Set<OrderItem>().AddRange(lines);
        await AffiliateSources.CaptureAsync(store, order, lines, affiliateVisitToken ?? input.AffiliateVisitToken, ct);
        foreach (var group in lines.GroupBy(x => new { x.StoreId, x.BrandId })) store.Set<SellerOrder>().Add(new SellerOrder { OrderId = order.Id, BrandId = group.Key.BrandId, StoreId = group.Key.StoreId, Status = "PENDING", Subtotal = group.Sum(x => x.UnitPrice * x.Quantity), DiscountAmount = group.Sum(x => x.DiscountAmount), TotalAmount = group.Sum(x => x.TotalPrice), CreatedAt = now });
        store.Set<Payment>().Add(new Payment { OrderId = order.Id, PaymentMethod = "COD", Amount = order.TotalAmount, Currency = "VND", Status = "PENDING", CreatedAt = now });
        store.Set<OrderStatusHistory>().Add(new OrderStatusHistory { OrderId = order.Id, OldStatus = "", NewStatus = "PENDING", Note = "Tạo đơn hàng", CreatedAt = now });
        await UserNotifications.AddAsync(store, userId, "ORDER_CREATED", "Đã tạo đơn hàng " + order.OrderCode, "Đơn hàng của bạn đang chờ xác nhận.", "Order", order.Id, ct);
        store.Set<CartItem>().RemoveRange(cartItems);
        await store.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return await OrderAsync(userId, order.Id, ct);
    }

    public async Task<CheckoutPreviewDto> PreviewAsync(Guid userId, CheckoutPreviewRequest input, CancellationToken ct)
    {
        CheckoutVoucherRules.NormalizeCodes(input.VoucherCode, input.VoucherCodes, input.AutoSelectVouchers);
        var cart = await CartAsync(userId, ct);
        var selected = cart.Items.Where(x => x.IsSelected).ToList();
        if (selected.Count == 0) throw new BusinessException("Chưa chọn sản phẩm");
        if (selected.Any(x => !x.IsAvailable || x.Quantity > x.AvailableStock)) throw new BusinessException("Sản phẩm không còn khả dụng hoặc không đủ tồn kho", 409);
        var shippingFee = config.GetValue<decimal>("Checkout:ShippingFee");
        if (shippingFee < 0) throw new InvalidOperationException("Shipping fee must not be negative.");
        var order = new Order { UserId = userId, Subtotal = cart.SelectedTotal, ShippingFee = shippingFee };
        var lines = selected.Select(x => new OrderItem { ProductId = x.ProductId, BrandId = x.BrandId, StoreId = x.StoreId, Quantity = x.Quantity, UnitPrice = x.UnitPrice, TotalPrice = x.UnitPrice * x.Quantity }).ToList();
        var selection = await ApplyVouchersAsync(order, lines, input.VoucherCode, input.VoucherCodes, input.AutoSelectVouchers, ct);
        return new(order.Subtotal, order.DiscountAmount, shippingFee, order.Subtotal - order.DiscountAmount + shippingFee - order.ShippingDiscountAmount, cart.SelectedCount, CartHash(cart), order.ShippingDiscountAmount, selection.Options, selection.Applied.Select(x => x.Code).ToArray());
    }

    private async Task<(IReadOnlyList<CheckoutVoucherDto> Options, IReadOnlyList<Voucher> Applied)> ApplyVouchersAsync(Order order, List<OrderItem> lines, string? legacyCode, IReadOnlyList<string>? voucherCodes, bool autoSelect, CancellationToken ct)
    {
        var codes = CheckoutVoucherRules.NormalizeCodes(legacyCode, voucherCodes, autoSelect);
        var collected = voucherCodes is not null || autoSelect;
        if (!collected && codes.Length == 0) return ([], []);
        var now = DateTimeOffset.UtcNow;
        var owned = await store.Set<UserVoucher>().AsNoTracking().Where(x => x.UserId == order.UserId && x.Status == "AVAILABLE").Select(x => x.VoucherId).ToListAsync(ct);
        var vouchers = await store.Set<Voucher>().Where(x => owned.Contains(x.Id) || !collected && codes.Contains(x.Code)).OrderBy(x => x.ExpiresAt).ThenBy(x => x.Code).ToListAsync(ct);
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
        // Both categories use original merchandise totals, so a product discount cannot invalidate shipping eligibility.
        var originalLines = lines.Select(x => new CheckoutVoucherLine(x.ProductId, x.BrandId, categories[x.ProductId], x.UnitPrice * x.Quantity)).ToArray();
        var options = vouchers.Select(v => CheckoutVoucherRules.Evaluate(v, targets.Where(t => t.VoucherId == v.Id).ToArray(), originalLines, order.ShippingFee, level, totalUses.GetValueOrDefault(v.Id), userUses.GetValueOrDefault(v.Id), owned.Contains(v.Id), collected, now)).ToArray();
        if (autoSelect) codes = CheckoutVoucherRules.BestCodes(options);
        var selected = codes.Select(code => options.SingleOrDefault(x => x.Code == code) ?? throw new BusinessException("Voucher chưa được thu thập hoặc không khả dụng", 409)).ToArray();
        if (selected.GroupBy(x => x.DiscountType == "SHIPPING").Any(g => g.Count() > 1)) throw new BusinessException("Chỉ chọn tối đa một voucher sản phẩm và một voucher vận chuyển", 400);
        foreach (var option in selected)
        {
            if (!option.IsEligible) throw new BusinessException(option.Reason ?? "Voucher không khả dụng", 409);
            if (option.DiscountType == "SHIPPING") { order.ShippingDiscountAmount = option.DiscountAmount; continue; }
            order.DiscountAmount = option.DiscountAmount;
            var voucherTargets = targets.Where(t => t.VoucherId == option.Id).ToArray();
            var eligible = lines.Where(x => CheckoutVoucherRules.Matches(x.ProductId, x.BrandId, categories[x.ProductId], voucherTargets)).ToArray();
            var allocations = CommerceRules.AllocateDiscount(eligible.Select(x => x.TotalPrice).ToArray(), option.DiscountAmount);
            for (var index = 0; index < eligible.Length; index++) { eligible[index].DiscountAmount = allocations[index]; eligible[index].TotalPrice -= allocations[index]; }
        }
        return (options.Where(x => owned.Contains(x.Id)).ToArray(), selected.Select(x => vouchers.Single(v => v.Id == x.Id)).ToArray());
    }

    public async Task<PageResult<OrderSummaryDto>> OrdersAsync(Guid userId, CatalogQuery query, CancellationToken ct)
    {
        var (page, size) = CatalogService.Paging(query);
        var source = store.Set<Order>().AsNoTracking().Where(x => x.UserId == userId);
        if (query.Status is not null)
        {
            if (query.Status is not ("PENDING" or "CONFIRMED" or "READY" or "SHIPPING" or "COMPLETED" or "CANCELLED" or "REFUNDED")) throw new BusinessException("Trạng thái không hợp lệ", 400);
            source = source.Where(x => x.Status == query.Status);
        }
        return new(await source.OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id).Skip((page - 1) * size).Take(size).Select(x => new OrderSummaryDto(x.Id, x.OrderCode, x.Status, x.PaymentStatus, x.TotalAmount, x.CreatedAt)).ToListAsync(ct), page, size, await source.CountAsync(ct));
    }

    public async Task<OrderDto> OrderAsync(Guid userId, Guid id, CancellationToken ct)
    {
        var order = await store.Set<Order>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.UserId == userId, ct) ?? throw new BusinessException("Không tìm thấy đơn hàng", 404);
        var items = await store.Set<OrderItem>().AsNoTracking().Where(x => x.OrderId == id).OrderBy(x => x.Id).Select(x => new OrderItemDto(x.Id, x.ProductId, x.BrandId, x.ProductName, x.SKU, x.Quantity, x.UnitPrice, x.DiscountAmount, x.TotalPrice, x.StoreId)).ToListAsync(ct);
        var shipments = await store.Set<Shipment>().AsNoTracking().Where(x => x.OrderId == id).OrderBy(x => x.ShippedAt).Take(50).Select(x => new ShipmentDto(x.Id, x.Carrier, x.TrackingCode, x.Status, x.ShippedAt, x.DeliveredAt)).ToListAsync(ct);
        var history = await store.Set<OrderStatusHistory>().AsNoTracking().Where(x => x.OrderId == id).OrderBy(x => x.CreatedAt).Take(50).Select(x => new OrderHistoryDto(x.OldStatus, x.NewStatus, x.Note, x.CreatedAt)).ToListAsync(ct);
        return new(order.Id, order.OrderCode, order.Status, order.PaymentStatus, order.Subtotal, order.DiscountAmount, order.ShippingFee, order.TotalAmount, order.RecipientName, order.RecipientPhone, order.ShippingAddress, order.CreatedAt, items, shipments, history, order.ShippingDiscountAmount);
    }

    public async Task<OrderDto?> FindRequestAsync(Guid userId, string key, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(key) || key.Length > 100) throw new BusinessException("Mã yêu cầu không hợp lệ", 400);
        var id = await store.Set<Order>().Where(x => x.UserId == userId && x.IdempotencyKey == key).Select(x => (Guid?)x.Id).SingleOrDefaultAsync(ct);
        return id is null ? null : await OrderAsync(userId, id.Value, ct);
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
                foreach (var usage in await store.Set<VoucherUsage>().Where(x => x.OrderId == order.Id && x.Status == "APPLIED").ToListAsync(ct))
                {
                    usage.Status = "RELEASED";
                    var userVoucher = await store.Set<UserVoucher>().SingleOrDefaultAsync(x => x.UserId == order.UserId && x.VoucherId == usage.VoucherId, ct);
                    if (userVoucher is not null)
                    {
                        var limit = await store.Set<Voucher>().Where(x => x.Id == usage.VoucherId).Select(x => x.PerUserLimit).SingleAsync(ct);
                        var otherUsages = store.Set<VoucherUsage>().Where(x => x.UserId == order.UserId && x.VoucherId == usage.VoucherId && x.OrderId != order.Id && x.Status == "APPLIED");
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
