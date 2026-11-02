namespace BCOBCA.DAL.Entities;

public sealed class Banner
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = "";
    public string ImageUrl { get; set; } = "";
    public string TargetUrl { get; set; } = "";
    public string Position { get; set; } = "";
    public int SortOrder { get; set; }
    public DateTimeOffset StartsAt { get; set; }
    public DateTimeOffset EndsAt { get; set; }
    public bool IsActive { get; set; }
}
