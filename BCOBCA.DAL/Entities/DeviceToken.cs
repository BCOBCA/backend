namespace BCOBCA.DAL.Entities;

public sealed class DeviceToken
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AccountId { get; set; }
    public string TokenHash { get; set; } = "";
    public string Platform { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
}
