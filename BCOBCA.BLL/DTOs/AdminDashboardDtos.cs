namespace BCOBCA.BLL.DTOs;

public sealed record DashboardMetricDto(decimal Value, decimal? ChangePercent);
public sealed record DashboardDayDto(DateOnly Date, decimal Revenue, int Orders);
public sealed record DashboardStatusDto(string Key, string Label, int Count);
public sealed record DashboardOrderDto(Guid Id, string Code, string StoreName, string BuyerName, decimal Amount, string Status, DateTimeOffset CreatedAt);
public sealed record DashboardProductDto(Guid Id, string Name, string ImageUrl, decimal Price, int Sold);
public sealed record AdminDashboardDto(DateOnly From, DateOnly To, DashboardMetricDto Revenue, DashboardMetricDto Orders,
    DashboardMetricDto CancellationRate, DashboardMetricDto NewUsers, DashboardMetricDto ActiveStores, DashboardMetricDto ActiveProducts,
    IReadOnlyList<DashboardDayDto> Daily, IReadOnlyList<DashboardStatusDto> Statuses, IReadOnlyList<DashboardOrderDto> RecentOrders, IReadOnlyList<DashboardProductDto> TopProducts);
