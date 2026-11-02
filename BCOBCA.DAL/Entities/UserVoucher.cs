namespace BCOBCA.DAL.Entities;

public sealed class UserVoucher
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid VoucherId { get; set; }
    public string Status { get; set; } = "";
    public DateTimeOffset ClaimedAt { get; set; }
    public DateTimeOffset? UsedAt { get; set; }
}
