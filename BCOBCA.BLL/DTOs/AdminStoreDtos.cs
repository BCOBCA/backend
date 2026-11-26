namespace BCOBCA.BLL.DTOs;

public sealed record AdminStoreQuery(int Page = 1, int PageSize = 8, string? Search = null, string Status = "all", string Verification = "all", string Joined = "all", string Sort = "newest");
public sealed record AdminStoreSummary(int Total, int Pending, int Active, int Inactive, decimal? Growth);
public sealed record AdminStoreRow(Guid Id, string Name, string Slug, string LogoUrl, string OwnerName, string Email, string Phone, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, string Status, bool IsVerified, bool Active);
public sealed record AdminStorePreview(Guid Id, string Title, string Subtitle, decimal? Value, DateTimeOffset? CreatedAt);
public sealed record AdminStoreVerification(Guid Id, string LegalName, string TaxCode, string RepresentativeName, short Status, string? RejectionReason, DateTimeOffset CreatedAt, IReadOnlyList<string> Documents);
public sealed record AdminStoreStats(int Products, int Orders, decimal Sales, double? Rating, int Reviews);
public sealed record AdminStoreDetail(AdminStoreRow Store, string Description, string Categories, AdminStoreStats Stats, AdminStoreVerification? Verification,
    IReadOnlyList<AdminStorePreview> Products, IReadOnlyList<AdminStorePreview> Orders, IReadOnlyList<AdminStorePreview> Reviews, IReadOnlyList<AdminStorePreview> History);
public sealed record AdminStoreInput(string Name, string Slug, string Email, string Phone, string Description, DateTimeOffset? UpdatedAt = null);
public sealed record AdminStoreStatusInput(bool Active, string Reason, DateTimeOffset UpdatedAt);
public sealed record AdminStoreReviewInput(Guid VerificationId, bool Approved, string Reason, DateTimeOffset UpdatedAt);
public sealed record AdminStoreDocument(string FileName, string ContentType, string Content);
