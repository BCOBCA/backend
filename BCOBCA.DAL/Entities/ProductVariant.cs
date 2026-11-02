namespace BCOBCA.DAL.Entities;

public sealed class ProductVariant
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProductId { get; set; }
    public string SKU { get; set; } = "";
    public string VariantName { get; set; } = "";
    public string Attributes { get; set; } = "{}";
    public decimal Price { get; set; }
    public decimal? SalePrice { get; set; }
    public int StockQuantity { get; set; }
    public int ReservedQuantity { get; set; }
    public bool IsActive { get; set; }
    public uint Version { get; set; }
}
