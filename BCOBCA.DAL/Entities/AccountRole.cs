namespace BCOBCA.DAL.Entities;

public sealed class AccountRole
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AccountId { get; set; }
    public string Role { get; set; } = "";
}
