namespace BCOBCA.DAL.Entities;

public sealed class Store
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OwnerAccountId { get; set; }
    public short StoreType { get; set; }
    public string Name { get; set; } = "";
    public int? GhnTestShopId { get; set; }
    public string Slug { get; set; } = "";
    public string LogoUrl { get; set; } = "";
    public string CoverUrl { get; set; } = "";
    public string Description { get; set; } = "";
    public string? Provinces { get; set; }
    public string Email { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Website { get; set; } = "";
    public short Status { get; set; }
    public bool IsVerified { get; set; }
    public DateTimeOffset? VerifiedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? DeletedAt { get; set; }
}
