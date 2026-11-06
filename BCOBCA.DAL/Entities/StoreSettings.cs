namespace BCOBCA.DAL.Entities;

public sealed class StoreSettings
{
    public Guid StoreId { get; set; }
    public bool AllowMessages { get; set; }
    public bool ShowPhone { get; set; }
    public bool ShowEmail { get; set; }
    public bool ShowBranches { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
