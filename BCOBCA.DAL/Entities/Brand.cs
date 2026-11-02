namespace BCOBCA.DAL.Entities;

public sealed class Brand
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OwnerAccountId { get; set; }
    public string Name { get; set; } = "";
    public string Slug { get; set; } = "";
    public string LogoUrl { get; set; } = "";
    public string CoverUrl { get; set; } = "";
    public string Description { get; set; } = "";
    public string Website { get; set; } = "";
    public string ContactEmail { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Address { get; set; } = "";
    public short Status { get; set; }
    public bool IsVerified { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
}
