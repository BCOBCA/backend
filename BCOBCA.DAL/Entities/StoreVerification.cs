namespace BCOBCA.DAL.Entities;

public sealed class StoreVerification
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid StoreId { get; set; }
    public short VerificationType { get; set; }
    public string LegalName { get; set; } = "";
    public string TaxCode { get; set; } = "";
    public string RepresentativeName { get; set; } = "";
    public string Documents { get; set; } = "[]";
    public short Status { get; set; }
    public string? RejectionReason { get; set; }
    public Guid? ReviewedByAccountId { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
