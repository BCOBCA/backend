using BCOBCA.BLL.Common;
using BCOBCA.BLL.DTOs;
using BCOBCA.BLL.Interfaces;
using BCOBCA.DAL.Entities;
using BCOBCA.DAL.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BCOBCA.BLL.Services;

public sealed class AdminDashboardService(ICommerceStore store) : IAdminDashboardService
{
    public static decimal? ChangePercent(decimal current, decimal previous)
        => previous == 0 ? null : decimal.Round((current - previous) / Math.Abs(previous) * 100, 1);

    public static string StatusBucket(string status, string paymentStatus)
        => paymentStatus == "REFUNDED" || status is "CANCELLED" or "RETURNED" or "REFUNDED" ? "cancelled"
            : status switch { "PENDING" => "pending", "SHIPPING" => "shipping", "COMPLETED" => "completed", _ => "processing" };

    public async Task<AdminDashboardDto> GetAsync(Guid accountId, DateOnly from, DateOnly to, CancellationToken ct)
    {
        if (!await store.Set<Account>().AnyAsync(a => a.Id == accountId && a.Status == 1 && (a.Role == "Admin" || a.Role == "SuperAdmin"), ct))
            throw new BusinessException("Không có quyền xem tổng quan hệ thống", 403);
        var days = to.DayNumber - from.DayNumber + 1;
        if (from < new DateOnly(2000, 1, 1) || to > CommerceRules.VietnamDate(DateTimeOffset.UtcNow) || days is < 1 or > 93)
            throw new BusinessException("Chọn khoảng thời gian hợp lệ, tối đa 93 ngày và không vượt quá hôm nay.");
        static DateTimeOffset Midnight(DateOnly date) => new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), TimeSpan.FromHours(7)).ToUniversalTime();
        var start = Midnight(from); var end = Midnight(to.AddDays(1)); var previousStart = start.AddDays(-days);
        var source = store.Set<Order>().AsNoTracking();
        var current = source.Where(o => o.CreatedAt >= start && o.CreatedAt < end);
        var rows = await source.Where(o => o.CreatedAt >= previousStart && o.CreatedAt < end)
            .GroupBy(o => new { o.CreatedAt.UtcDateTime.AddHours(7).Year, o.CreatedAt.UtcDateTime.AddHours(7).Month, o.CreatedAt.UtcDateTime.AddHours(7).Day })
            .Select(g => new {
                g.Key.Year, g.Key.Month, g.Key.Day, Orders = g.Count(),
                Revenue = g.Sum(o => o.Status == "COMPLETED" && o.PaymentStatus != "REFUNDED" ? o.Subtotal - o.DiscountAmount : 0m),
                Cancelled = g.Count(o => o.Status == "CANCELLED" || o.Status == "RETURNED" || o.Status == "REFUNDED" || o.PaymentStatus == "REFUNDED")
            }).ToListAsync(ct);
        var grouped = rows.Select(d => new { Date = new DateOnly(d.Year, d.Month, d.Day), d.Orders, d.Revenue, d.Cancelled }).ToList();
        var currentDays = grouped.Where(d => d.Date >= from).ToList();
        var previousDays = grouped.Where(d => d.Date < from).ToList();
        var revenue = currentDays.Sum(d => d.Revenue); var previousRevenue = previousDays.Sum(d => d.Revenue);
        var orders = currentDays.Sum(d => d.Orders); var previousOrders = previousDays.Sum(d => d.Orders);
        var cancellationRate = orders == 0 ? 0 : 100m * currentDays.Sum(d => d.Cancelled) / orders;
        var previousRate = previousOrders == 0 ? 0 : 100m * previousDays.Sum(d => d.Cancelled) / previousOrders;
        var users = store.Set<User>().AsNoTracking();
        var newUsers = await users.CountAsync(u => u.CreatedAt >= start && u.CreatedAt < end, ct);
        var previousUsers = await users.CountAsync(u => u.CreatedAt >= previousStart && u.CreatedAt < start, ct);
        var activeStores = await store.Set<Store>().CountAsync(s => s.StoreType == 1 && s.Status == 1 && s.DeletedAt == null, ct);
        var activeProducts = await store.Set<Product>().CountAsync(p => p.Status == 1 && p.StoreId != null
            && store.Set<Store>().Any(s => s.Id == p.StoreId && s.StoreType == 1 && s.Status == 1 && s.DeletedAt == null), ct);
        var statusCounts = await current.GroupBy(o => new { o.Status, o.PaymentStatus }).Select(g => new { g.Key.Status, g.Key.PaymentStatus, Count = g.Count() }).ToListAsync(ct);
        var statusDefinitions = new[] { ("pending", "Chờ duyệt"), ("processing", "Đang xử lý"), ("shipping", "Đang giao"), ("completed", "Hoàn thành"), ("cancelled", "Đã hủy / hoàn") };
        var statuses = statusDefinitions.Select(s => new DashboardStatusDto(s.Item1, s.Item2,
            statusCounts.Where(c => StatusBucket(c.Status, c.PaymentStatus) == s.Item1).Sum(c => c.Count))).ToList();
        var recent = await current.OrderByDescending(o => o.CreatedAt).ThenBy(o => o.Id).Take(5)
            .Select(o => new DashboardOrderDto(o.Id, o.OrderCode,
                store.Set<Store>().Where(s => store.Set<OrderItem>().Any(i => i.OrderId == o.Id && i.StoreId == s.Id)).OrderBy(s => s.Name).Select(s => s.Name).FirstOrDefault() ?? "—",
                users.Where(u => u.Id == o.UserId).Select(u => u.FullName).FirstOrDefault() ?? o.RecipientName,
                o.TotalAmount, o.PaymentStatus == "REFUNDED" ? "REFUNDED" : o.Status, o.CreatedAt)).ToListAsync(ct);
        var rankings = await (from item in store.Set<OrderItem>().AsNoTracking()
                join order in current.Where(o => o.Status == "COMPLETED" && o.PaymentStatus != "REFUNDED") on item.OrderId equals order.Id
                group item by item.ProductId into g select new { Id = g.Key, Sold = g.Sum(i => i.Quantity) })
            .OrderByDescending(p => p.Sold).ThenBy(p => p.Id).Take(5).ToListAsync(ct);
        var ids = rankings.Select(p => p.Id).ToList();
        var products = await store.Set<Product>().IgnoreQueryFilters().AsNoTracking().Where(p => ids.Contains(p.Id))
            .Select(p => new { p.Id, p.Name, Price = p.SalePrice ?? p.BasePrice,
                ImageUrl = store.Set<ProductImage>().Where(i => i.ProductId == p.Id).OrderByDescending(i => i.IsPrimary).ThenBy(i => i.SortOrder).ThenBy(i => i.Id).Select(i => i.ImageUrl).FirstOrDefault() ?? "" }).ToListAsync(ct);
        var top = rankings.Select(r => { var product = products.First(p => p.Id == r.Id); return new DashboardProductDto(r.Id, product.Name, product.ImageUrl, product.Price, r.Sold); }).ToList();
        var daily = Enumerable.Range(0, days).Select(i => {
            var date = from.AddDays(i); var point = currentDays.FirstOrDefault(d => d.Date == date);
            return new DashboardDayDto(date, point?.Revenue ?? 0, point?.Orders ?? 0);
        }).ToList();
        var shops = store.Set<Store>().AsNoTracking().Where(s => s.StoreType == 1 && s.DeletedAt == null);
        var catalogue = store.Set<Product>().AsNoTracking().Where(p => p.StoreId != null && shops.Any(s => s.Id == p.StoreId));
        var totalUsers = await users.CountAsync(ct);
        var totalStores = await shops.CountAsync(ct);
        var totalProducts = await catalogue.CountAsync(ct);
        var pendingShops = shops.Where(s => store.Set<StoreVerification>().Any(v => v.StoreId == s.Id && v.Status == 0));
        var pendingCount = await pendingShops.CountAsync(ct);
        var liveCount = await shops.CountAsync(s => s.Status == 1 && !store.Set<StoreVerification>().Any(v => v.StoreId == s.Id && v.Status == 0), ct);
        var feeRows = await (from settlement in store.Set<SellerSettlement>().AsNoTracking()
            join seller in store.Set<SellerOrder>() on settlement.SellerOrderId equals seller.Id
            join order in source on seller.OrderId equals order.Id
            where settlement.CreatedAt >= previousStart && settlement.CreatedAt < end
                && (settlement.Status == "PENDING" || settlement.Status == "PAID") && order.Status == "COMPLETED" && order.PaymentStatus != "REFUNDED"
            group settlement by new { settlement.CreatedAt.UtcDateTime.AddHours(7).Year, settlement.CreatedAt.UtcDateTime.AddHours(7).Month, settlement.CreatedAt.UtcDateTime.AddHours(7).Day } into g
            select new { g.Key.Year, g.Key.Month, g.Key.Day, Amount = g.Sum(s => s.FeeAmount) }).ToListAsync(ct);
        var fees = feeRows.ToDictionary(r => new DateOnly(r.Year, r.Month, r.Day), r => r.Amount);
        var platformRevenue = fees.Where(r => r.Key >= from).Sum(r => r.Value);
        var previousFees = fees.Where(r => r.Key < from).Sum(r => r.Value);
        return new(from, to, new(revenue, ChangePercent(revenue, previousRevenue)), new(orders, ChangePercent(orders, previousOrders)),
            new(decimal.Round(cancellationRate, 1), ChangePercent(cancellationRate, previousRate)), new(newUsers, ChangePercent(newUsers, previousUsers)),
            new(activeStores, null), new(activeProducts, null), daily, statuses, recent, top)
        {
            TotalUsers = new(totalUsers, ChangePercent(await users.CountAsync(u => u.CreatedAt < end, ct), await users.CountAsync(u => u.CreatedAt < start, ct))),
            TotalStores = new(totalStores, ChangePercent(await shops.CountAsync(s => s.CreatedAt < end, ct), await shops.CountAsync(s => s.CreatedAt < start, ct))),
            TotalProducts = new(totalProducts, ChangePercent(await catalogue.CountAsync(p => p.CreatedAt < end, ct), await catalogue.CountAsync(p => p.CreatedAt < start, ct))),
            Gmv = new(revenue, ChangePercent(revenue, previousRevenue)),
            PlatformRevenue = new(platformRevenue, ChangePercent(platformRevenue, previousFees)),
            TradingDaily = daily.Select(d => new DashboardTradingDayDto(d.Date, d.Revenue, fees.GetValueOrDefault(d.Date))).ToList(),
            StoreStatuses = [new("active", "Đang hoạt động", liveCount), new("pending", "Chờ duyệt hồ sơ", pendingCount), new("inactive", "Ngừng hoạt động", totalStores - liveCount - pendingCount)],
            Tasks = [new("stores", pendingCount), new("products", null),
                new("withdrawals", await store.Set<AffiliateWithdrawal>().CountAsync(w => w.Status == "PENDING", ct)),
                new("refunds", await store.Set<Refund>().CountAsync(r => r.Status == "PENDING" || r.Status == "APPROVED", ct)),
                new("reports", null), new("support", await store.Set<Feedback>().CountAsync(f => f.Type == "SUPPORT" && f.Status != "RESOLVED" && f.AdminResponse == "", ct))]
        };
    }
}
