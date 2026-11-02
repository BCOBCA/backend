namespace BCOBCA.DAL.Entities;

public sealed class Service
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BrandId { get; set; }
    public string Name { get; set; } = "";
    public string Slug { get; set; } = "";
    public string Description { get; set; } = "";
    public string ThumbnailUrl { get; set; } = "";
    public decimal? PriceFrom { get; set; }
    public string PricingType { get; set; } = "";
    public string Status { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public bool IsDeleted { get; set; }
}
