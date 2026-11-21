namespace BCOBCA.BLL.DTOs;

public sealed record ReviewInput(short Rating, string Content, IReadOnlyList<string>? ImageUrls = null);
public sealed record ReviewReplyDto(string Content, string StoreName, DateTimeOffset UpdatedAt);
public sealed record ReviewDto(Guid Id, Guid ProductId, string DisplayName, short Rating, string Content, short Status, DateTimeOffset CreatedAt, IReadOnlyList<string> ImageUrls, Guid Revision, ReviewReplyDto? Reply = null);
public sealed record ReviewModerationRequest(short Status, Guid ExpectedRevision);
public sealed record ReviewReplyInput(string Content, Guid ExpectedRevision);
public sealed record SellerReviewQuery(int Page = 1, int PageSize = 20, int? Rating = null, bool? Replied = null, Guid? ProductId = null, string? Search = null, DateTimeOffset? From = null, DateTimeOffset? To = null);
public sealed record SellerReviewDto(Guid Id, Guid ProductId, string ProductName, string? ProductImageUrl, string VariantName, string OrderCode, string DisplayName, short Rating, string Content, IReadOnlyList<string> ImageUrls, DateTimeOffset CreatedAt, Guid Revision, ReviewReplyDto? Reply);
