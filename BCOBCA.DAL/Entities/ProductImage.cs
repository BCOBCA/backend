namespace BCOBCA.DAL.Entities;

public sealed class ProductImage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProductId { get; set; }
    public string ImageUrl { get; set; } = "";
    public bool IsPrimary { get; set; }
    public int SortOrder { get; set; }
}
