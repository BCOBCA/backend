namespace BCOBCA.DAL.Entities;

public sealed class Article
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? BrandId { get; set; }
    public string Title { get; set; } = "";
    public string Slug { get; set; } = "";
    public string ThumbnailUrl { get; set; } = "";
    public string Summary { get; set; } = "";
    public string Content { get; set; } = "";
    public string Status { get; set; } = "";
    public DateTimeOffset? PublishedAt { get; set; }
}
