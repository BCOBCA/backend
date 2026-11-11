namespace BCOBCA.DAL.Entities;

public sealed class Voucher
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? StoreId { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string DiscountType { get; set; } = "";
    public decimal DiscountValue { get; set; }
    public decimal? MaxDiscount { get; set; }
    public decimal MinOrderAmount { get; set; }
    public int? UsageLimit { get; set; }
    public int PerUserLimit { get; set; }
    public DateTimeOffset StartsAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public string Status { get; set; } = "";
    public long PointCost { get; set; }
    public int MinLevel { get; set; } = 1;
    public bool IsFlashSale { get; set; }
    public uint Version { get; set; }
}
