namespace BCOBCA.DAL.Entities;

public sealed class BrandCategoryMapping
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BrandId { get; set; }
    public Guid BrandCategoryId { get; set; }
}
