namespace BCOBCA.DAL.Entities;

public sealed class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AccountId { get; set; }
    public string FullName { get; set; } = "";
    public string Nickname { get; set; } = "";
    public string AvatarUrl { get; set; } = "";
    public string CoverUrl { get; set; } = "";
    public short Gender { get; set; }
    public DateOnly DateOfBirth { get; set; }
    public string ReferralCode { get; set; } = "";
    public Guid? ReferredByUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
}
