namespace BCOBCA.DAL.Entities;

public sealed class Category
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? ParentId { get; set; }
    public string Name { get; set; } = "";
    public string Slug { get; set; } = "";
    public string ImageUrl { get; set; } = "";
    public string AttributeDefinitions { get; set; } = "[]";
    public int SortOrder { get; set; }
    public bool IsActive { get; set; }
}
