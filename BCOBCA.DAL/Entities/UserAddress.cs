namespace BCOBCA.DAL.Entities;

public sealed class UserAddress
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string RecipientName { get; set; } = "";
    public string Phone { get; set; } = "";
    public string ProvinceCode { get; set; } = "";
    public string WardCode { get; set; } = "";
    public string AddressLine { get; set; } = "";
    public short AddressType { get; set; }
    public bool IsDefault { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
