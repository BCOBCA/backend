namespace BCOBCA.DAL.Entities;

public sealed class VoucherTarget
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid VoucherId { get; set; }
    public Guid? BrandId { get; set; }
    public Guid? CategoryId { get; set; }
    public Guid? ProductId { get; set; }
}
