namespace BCOBCA.BLL.DTOs;

public sealed record PlatformFeeConfigurationDto(decimal PlatformFeeRate, Guid Revision, DateTimeOffset? UpdatedAt, Guid? UpdatedByAccountId);
public sealed record UpdatePlatformFeeRequest(decimal? PlatformFeeRate, Guid? ExpectedRevision);
