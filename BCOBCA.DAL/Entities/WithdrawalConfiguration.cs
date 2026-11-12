namespace BCOBCA.DAL.Entities;

public sealed class WithdrawalConfiguration
{
    public static readonly Guid SingletonId = Guid.Parse("ca401bc0-b07b-4eb4-9b1d-30e229fdc943");
    public static readonly Guid InitialRevision = Guid.Parse("c50e4db3-46b0-42de-a979-c2108fb1c599");
    public Guid Id { get; set; } = SingletonId;
    public decimal FeeAmount { get; set; }
    public decimal MinimumAmount { get; set; } = 50000m;
    public decimal MaximumAmount { get; set; } = 50000000m;
    public Guid Revision { get; set; } = InitialRevision;
    public DateTimeOffset? UpdatedAt { get; set; }
    public Guid? UpdatedByAccountId { get; set; }
}
