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
                v.SalePrice ?? v.Price, i.Quantity, v.StockQuantity - v.ReservedQuantity, i.IsSelected, !p.IsDeleted && p.Status == 1 && !b.IsDeleted && b.Status == 1 && v.IsActive)).ToListAsync(ct);
        return new(items, items.Where(x => x.IsSelected).Sum(x => x.UnitPrice * x.Quantity), items.Where(x => x.IsSelected).Sum(x => x.Quantity));
    }

    public async Task AddAsync(Guid userId, CartItemRequest input, CancellationToken ct)
    {
        if (input.Quantity is < 1 or > 999) throw new BusinessException("Số lượng phải từ 1 đến 999");
        await using var tx = await store.BeginTransactionAsync(ct);
        var cart = await store.Set<Cart>().SingleAsync(x => x.UserId == userId, ct);
        var variant = await store.Set<ProductVariant>().SingleOrDefaultAsync(x => x.Id == input.VariantId && x.ProductId == input.ProductId && x.IsActive, ct) ?? throw new BusinessException("Phân loại không khả dụng", 404);
        if (!await store.Set<Product>().AnyAsync(x => x.Id == input.ProductId && x.Status == 1 && store.Set<Brand>().Any(b => b.Id == x.BrandId && b.Status == 1), ct)) throw new BusinessException("Sản phẩm không khả dụng", 404);
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
            var variant = await store.Set<ProductVariant>().SingleOrDefaultAsync(x => x.Id == item.VariantId && x.ProductId == product.Id && x.IsActive, ct) ?? throw new BusinessException("Phân loại không khả dụng", 409);
            if (item.Quantity > variant.StockQuantity - variant.ReservedQuantity) throw new BusinessException("Không đủ tồn kho", 409);
            var unitPrice = variant.SalePrice ?? variant.Price;
            if (unitPrice < 0) throw new BusinessException("Giá sản phẩm không hợp lệ", 409);
            variant.ReservedQuantity += item.Quantity;
            lines.Add(new OrderItem { OrderId = order.Id, ProductId = product.Id, VariantId = variant.Id, BrandId = product.BrandId, ProductName = product.Name, SKU = variant.SKU, Quantity = item.Quantity, UnitPrice = unitPrice, TotalPrice = unitPrice * item.Quantity });
            store.Set<InventoryReservation>().Add(new InventoryReservation { OrderId = order.Id, VariantId = variant.Id, Quantity = item.Quantity, Status = "HELD", ExpiresAt = now.AddHours(config.GetValue<int>("Checkout:ReservationHours")), CreatedAt = now });
        }
        order.Subtotal = lines.Sum(x => x.TotalPrice);
        if (!string.IsNullOrWhiteSpace(input.VoucherCode))
        {
            var voucher = await ApplyVoucherAsync(order, lines, input.VoucherCode.Trim().ToUpperInvariant(), ct);
            store.Set<VoucherUsage>().Add(new VoucherUsage { VoucherId = voucher.Id, UserId = order.UserId, OrderId = order.Id, DiscountAmount = order.DiscountAmount, Status = "APPLIED", CreatedAt = now });
            store.Set<OrderVoucherApplication>().Add(new OrderVoucherApplication { OrderId = order.Id, VoucherId = voucher.Id, DiscountAmount = order.DiscountAmount });
            var userVoucher = await store.Set<UserVoucher>().SingleOrDefaultAsync(x => x.UserId == userId && x.VoucherId == voucher.Id, ct);
            if (userVoucher is null) { userVoucher = new UserVoucher { UserId = userId, VoucherId = voucher.Id, ClaimedAt = now }; store.Set<UserVoucher>().Add(userVoucher); }
            var usedCount = await store.Set<VoucherUsage>().CountAsync(x => x.UserId == userId && x.VoucherId == voucher.Id && x.Status == "APPLIED", ct);
            userVoucher.Status = usedCount + 1 >= voucher.PerUserLimit ? "USED" : "AVAILABLE"; userVoucher.UsedAt = now;
        }
        order.ShippingFee = config.GetValue<decimal>("Checkout:ShippingFee");
        if (order.ShippingFee < 0) throw new InvalidOperationException("Shipping fee must not be negative.");
        order.TotalAmount = order.Subtotal - order.DiscountAmount + order.ShippingFee;
        if (input.ExpectedTotal is not null && input.ExpectedTotal != order.TotalAmount) throw new BusinessException("Tổng tiền đã thay đổi. Vui lòng xem lại trước khi đặt hàng", 409);
        store.Set<Order>().Add(order); store.Set<OrderItem>().AddRange(lines);
        await AffiliateSources.CaptureAsync(store, order, lines, affiliateVisitToken ?? input.AffiliateVisitToken, ct);
        foreach (var group in lines.GroupBy(x => x.BrandId)) store.Set<SellerOrder>().Add(new SellerOrder { OrderId = order.Id, BrandId = group.Key, Status = "PENDING", Subtotal = group.Sum(x => x.UnitPrice * x.Quantity), DiscountAmount = group.Sum(x => x.DiscountAmount), TotalAmount = group.Sum(x => x.TotalPrice), CreatedAt = now });
        store.Set<Payment>().Add(new Payment { OrderId = order.Id, PaymentMethod = "COD", Amount = order.TotalAmount, Currency = "VND", Status = "PENDING", CreatedAt = now });
        store.Set<OrderStatusHistory>().Add(new OrderStatusHistory { OrderId = order.Id, OldStatus = "", NewStatus = "PENDING", Note = "Tạo đơn hàng", CreatedAt = now });
        await UserNotifications.AddAsync(store, userId, "ORDER_CREATED", "Đã tạo đơn hàng " + order.OrderCode, "Đơn hàng của bạn đang chờ xác nhận.", "Order", order.Id, ct);
        store.Set<CartItem>().RemoveRange(cartItems);
        await store.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return await OrderAsync(userId, order.Id, ct);
    }

    public async Task<CheckoutPreviewDto> PreviewAsync(Guid userId, CheckoutPreviewRequest input, CancellationToken ct)
    {
        if (input.VoucherCode?.Length > 50) throw new BusinessException("Mã voucher không hợp lệ", 400);
        var cart = await CartAsync(userId, ct);
        var selected = cart.Items.Where(x => x.IsSelected).ToList();
        if (selected.Count == 0) throw new BusinessException("Chưa chọn sản phẩm");
        if (selected.Any(x => !x.IsAvailable || x.Quantity > x.AvailableStock)) throw new BusinessException("Sản phẩm không còn khả dụng hoặc không đủ tồn kho", 409);
        var order = new Order { UserId = userId, Subtotal = cart.SelectedTotal };
        var lines = selected.Select(x => new OrderItem { ProductId = x.ProductId, BrandId = x.BrandId, Quantity = x.Quantity, UnitPrice = x.UnitPrice, TotalPrice = x.UnitPrice * x.Quantity }).ToList();
        if (!string.IsNullOrWhiteSpace(input.VoucherCode)) await ApplyVoucherAsync(order, lines, input.VoucherCode.Trim().ToUpperInvariant(), ct);
        var shippingFee = config.GetValue<decimal>("Checkout:ShippingFee");
        if (shippingFee < 0) throw new InvalidOperationException("Shipping fee must not be negative.");
        return new(order.Subtotal, order.DiscountAmount, shippingFee, order.Subtotal - order.DiscountAmount + shippingFee, cart.SelectedCount, CartHash(cart));
    }

    private async Task<Voucher> ApplyVoucherAsync(Order order, List<OrderItem> lines, string code, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var voucher = await store.Set<Voucher>().SingleOrDefaultAsync(x => x.Code == code && x.Status == "ACTIVE" && x.StartsAt <= now && x.ExpiresAt > now, ct) ?? throw new BusinessException("Voucher không khả dụng");
        var usage = store.Set<VoucherUsage>().Where(x => x.VoucherId == voucher.Id && x.Status == "APPLIED");
        if (voucher.UsageLimit is not null && await usage.CountAsync(ct) >= voucher.UsageLimit || await usage.CountAsync(x => x.UserId == order.UserId, ct) >= voucher.PerUserLimit) throw new BusinessException("Voucher đã hết lượt sử dụng", 409);
        var targets = await store.Set<VoucherTarget>().Where(x => x.VoucherId == voucher.Id).ToListAsync(ct);
        var categories = await store.Set<Product>().Where(x => lines.Select(l => l.ProductId).Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.CategoryId, ct);
        var eligible = lines.Where(line => targets.Count == 0 || targets.Any(t => (t.BrandId == null || t.BrandId == line.BrandId) && (t.ProductId == null || t.ProductId == line.ProductId) && (t.CategoryId == null || t.CategoryId == categories[line.ProductId]))).ToList();
        var eligibleTotal = eligible.Sum(x => x.TotalPrice);
        if (eligibleTotal <= 0 || eligibleTotal < voucher.MinOrderAmount) throw new BusinessException("Đơn hàng chưa đủ điều kiện voucher");
        order.DiscountAmount = CommerceRules.Discount(eligibleTotal, voucher.DiscountType, voucher.DiscountValue, voucher.MaxDiscount);
        var allocations = CommerceRules.AllocateDiscount(eligible.Select(x => x.TotalPrice).ToArray(), order.DiscountAmount);
        for (var index = 0; index < eligible.Count; index++)
        {
            var line = eligible[index];
            line.DiscountAmount = allocations[index]; line.TotalPrice -= allocations[index];
        }
        return voucher;
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
        var items = await store.Set<OrderItem>().AsNoTracking().Where(x => x.OrderId == id).OrderBy(x => x.Id).Select(x => new OrderItemDto(x.Id, x.ProductId, x.BrandId, x.ProductName, x.SKU, x.Quantity, x.UnitPrice, x.DiscountAmount, x.TotalPrice)).ToListAsync(ct);
        var shipments = await store.Set<Shipment>().AsNoTracking().Where(x => x.OrderId == id).OrderBy(x => x.ShippedAt).Take(50).Select(x => new ShipmentDto(x.Id, x.Carrier, x.TrackingCode, x.Status, x.ShippedAt, x.DeliveredAt)).ToListAsync(ct);
        var history = await store.Set<OrderStatusHistory>().AsNoTracking().Where(x => x.OrderId == id).OrderBy(x => x.CreatedAt).Take(50).Select(x => new OrderHistoryDto(x.OldStatus, x.NewStatus, x.Note, x.CreatedAt)).ToListAsync(ct);
        return new(order.Id, order.OrderCode, order.Status, order.PaymentStatus, order.Subtotal, order.DiscountAmount, order.ShippingFee, order.TotalAmount, order.RecipientName, order.RecipientPhone, order.ShippingAddress, order.CreatedAt, items, shipments, history);
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
