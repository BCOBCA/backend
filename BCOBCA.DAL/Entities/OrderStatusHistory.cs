namespace BCOBCA.DAL.Entities;

public sealed class OrderStatusHistory
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrderId { get; set; }
    public string OldStatus { get; set; } = "";
    public string NewStatus { get; set; } = "";
    public Guid? ChangedByAccountId { get; set; }
    public string Note { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
}
