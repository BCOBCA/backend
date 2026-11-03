namespace BCOBCA.DAL.Entities;

public sealed class Banner
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string ImageUrl { get; set; } = "";
    public string TargetUrl { get; set; } = "";
    public DateTimeOffset StartsAt { get; set; }
    public DateTimeOffset EndsAt { get; set; }
    public bool IsActive { get; set; }
}
