namespace BCOBCA.DAL.Entities;

public sealed class PlatformFeeConfiguration
{
    public static readonly Guid SingletonId = Guid.Parse("e312fc37-3690-41a2-80dd-8f0276adf492");
    public static readonly Guid InitialRevision = Guid.Parse("07e3ef2e-844e-49b1-b0ef-822eb4f17483");
    public Guid Id { get; set; } = SingletonId;
    public decimal PlatformFeeRate { get; set; }
    public Guid Revision { get; set; } = InitialRevision;
    public DateTimeOffset? UpdatedAt { get; set; }
    public Guid? UpdatedByAccountId { get; set; }
}
