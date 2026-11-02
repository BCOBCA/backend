namespace BCOBCA.DAL.Entities;

public sealed class CheckInRule
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public int RewardPoints { get; set; }
    public bool IsActive { get; set; }
}
