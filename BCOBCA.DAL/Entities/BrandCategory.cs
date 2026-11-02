namespace BCOBCA.DAL.Entities;

public sealed class BrandCategory
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string Slug { get; set; } = "";
    public string IconUrl { get; set; } = "";
    public int SortOrder { get; set; }
    public bool IsActive { get; set; }
}
