namespace BCOBCA.BLL.DTOs;

public sealed class AdminProductQuery
{
    public string Search { get; set; } = "";
    public Guid? StoreId { get; set; }
    public Guid? CategoryId { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    public string Status { get; set; } = "all";
    public string Stock { get; set; } = "all";
    public string Sort { get; set; } = "newest";
    public string From { get; set; } = "";
    public string To { get; set; } = "";
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}
public sealed record AdminProductOption(Guid Id, string Name);
public sealed record AdminProductRow(Guid Id, Guid StoreId, string StoreName, Guid CategoryId, string CategoryName,
    string Name, string SKU, string ImageUrl, decimal Price, int AvailableStock, short Status, DateTimeOffset CreatedAt,
    int Sold, decimal Revenue, double? Rating);
public sealed record AdminProductSummary(int Total, int Sold, decimal Revenue, double? Rating);
public sealed record AdminProductPage(IReadOnlyList<AdminProductRow> Items, int Page, int PageSize, int TotalItems,
    AdminProductSummary Summary, IReadOnlyList<AdminProductOption> Stores, IReadOnlyList<AdminProductOption> Categories);
