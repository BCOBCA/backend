namespace BCOBCA.DAL.Entities;

public sealed class MembershipTier
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public decimal MinSpend { get; set; }
    public int MinPoints { get; set; }
    public string Benefits { get; set; } = "{}";
    public int SortOrder { get; set; }
    public bool IsActive { get; set; }
}
