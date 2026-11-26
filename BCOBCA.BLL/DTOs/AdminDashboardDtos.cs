namespace BCOBCA.BLL.DTOs;

public sealed record DashboardMetricDto(decimal Value, decimal? ChangePercent);
public sealed record DashboardDayDto(DateOnly Date, decimal Revenue, int Orders);
public sealed record DashboardStatusDto(string Key, string Label, int Count);
public sealed record DashboardOrderDto(Guid Id, string Code, string StoreName, string BuyerName, decimal Amount, string Status, DateTimeOffset CreatedAt);
public sealed record DashboardProductDto(Guid Id, string Name, string ImageUrl, decimal Price, int Sold);
public sealed record DashboardTaskDto(string Key, int? Count);
public sealed record DashboardTradingDayDto(DateOnly Date, decimal Gmv, decimal PlatformRevenue);
public sealed record AdminDashboardDto(DateOnly From, DateOnly To, DashboardMetricDto Revenue, DashboardMetricDto Orders,
    DashboardMetricDto CancellationRate, DashboardMetricDto NewUsers, DashboardMetricDto ActiveStores, DashboardMetricDto ActiveProducts,
    IReadOnlyList<DashboardDayDto> Daily, IReadOnlyList<DashboardStatusDto> Statuses, IReadOnlyList<DashboardOrderDto> RecentOrders, IReadOnlyList<DashboardProductDto> TopProducts)
{
    public DashboardMetricDto TotalUsers { get; init; } = new(0, null);
    public DashboardMetricDto TotalStores { get; init; } = new(0, null);
    public DashboardMetricDto TotalProducts { get; init; } = new(0, null);
    public DashboardMetricDto Gmv { get; init; } = new(0, null);
    public DashboardMetricDto PlatformRevenue { get; init; } = new(0, null);
    public IReadOnlyList<DashboardTradingDayDto> TradingDaily { get; init; } = [];
    public IReadOnlyList<DashboardStatusDto> StoreStatuses { get; init; } = [];
    public IReadOnlyList<DashboardTaskDto> Tasks { get; init; } = [];
}
