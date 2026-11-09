namespace BCOBCA.DAL.Entities;

public sealed class Product
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BrandId { get; set; }
    public Guid CategoryId { get; set; }
    public string Name { get; set; } = "";
    public string Slug { get; set; } = "";
    public string SKU { get; set; } = "";
    public string ShortDescription { get; set; } = "";
    public string Description { get; set; } = "";
    public string Specifications { get; set; } = "{}";
    public decimal BasePrice { get; set; }
    public decimal? SalePrice { get; set; }
    public short ProductType { get; set; }
    public short Status { get; set; }
    public bool IsFeatured { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
    public bool AffiliateEnabled { get; set; } = true;
    public decimal? AffiliateMaxRate { get; set; }
    public decimal? AffiliateBudget { get; set; }
}
