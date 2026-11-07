namespace BCOBCA.BLL.DTOs;

public sealed record DealerQuery(int Page = 1, int PageSize = 12, string? Search = null, string? ProvinceCode = null, bool VerifiedOnly = false, string SortBy = "popular");
public sealed record DealerDto(Guid Id, string Name, string Slug, string LogoUrl, string CoverUrl, string Description, bool IsVerified, int FollowerCount, string LocationLabel, int BranchCount, bool IsFollowing, string? Provinces = null);
public sealed record DealerProfileDto(Guid Id, string Name, string? Provinces);
public sealed record DealerProvincesInput(string? Provinces);
public sealed record DealerBranchDto(Guid Id, string Name, string Address, string ProvinceCode, string WardCode, string? Phone, decimal? Latitude, decimal? Longitude, bool IsMain);
public sealed record DealerDetailDto(DealerDto Dealer, string? Phone, string? Email, string? Website, IReadOnlyList<DealerBranchDto> Branches);
public sealed record DealerFollowDto(bool IsFollowing, int FollowerCount);
