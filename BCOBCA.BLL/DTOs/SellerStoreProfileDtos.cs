namespace BCOBCA.BLL.DTOs;

public sealed record SellerVisibilityDto(bool AllowMessages, bool ShowPhone, bool ShowEmail, bool ShowBranches);
public sealed record SellerMainBranchInput(string Name, string Phone, string Address);
public sealed record SellerStoreProfileInput(string Name, string Description, string LogoUrl, string CoverUrl,
    string Email, string Phone, string Website, string Provinces, Guid[] CategoryIds,
    SellerMainBranchInput MainBranch, SellerVisibilityDto Settings, DateTimeOffset UpdatedAt);
public sealed record SellerBranchProfileDto(Guid Id, string Name, string Phone, string Address, string ProvinceCode,
    string WardCode, bool IsMain, bool IsActive, decimal? Latitude, decimal? Longitude);
public sealed record SellerVerificationDto(string LegalName, string TaxCode, string RepresentativeName,
    short Status, string? RejectionReason, DateTimeOffset UpdatedAt);
public sealed record SellerStoreProfileDto(Guid Id, string Name, string Slug, string Description, string LogoUrl,
    string CoverUrl, string Email, string Phone, string Website, string? Provinces, short Status, bool IsVerified,
    DateTimeOffset? VerifiedAt, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, int FollowerCount,
    IReadOnlyList<DealerCategoryDto> Categories, IReadOnlyList<SellerBranchProfileDto> Branches,
    SellerVisibilityDto Settings, SellerVerificationDto? Verification);
