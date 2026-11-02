namespace BCOBCA.BLL.DTOs;

public sealed record ReviewInput(short Rating, string Content, IReadOnlyList<string>? ImageUrls = null);
public sealed record ReviewDto(Guid Id, Guid ProductId, string DisplayName, short Rating, string Content, short Status, DateTimeOffset CreatedAt, IReadOnlyList<string> ImageUrls, Guid Revision);
public sealed record ReviewModerationRequest(short Status, Guid ExpectedRevision);
