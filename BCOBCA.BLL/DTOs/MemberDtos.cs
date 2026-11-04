namespace BCOBCA.BLL.DTOs;

public sealed record MembershipDto(string MemberCode, string Tier, long AvailablePoints, long LifetimeEarnedPoints, int OrderCount, decimal TotalSpend, bool CheckedInToday, decimal AffiliateBalance, IReadOnlyDictionary<string, int> OrderCounts, long PointDebt = 0, MembershipProgressDto? LevelInfo = null);
public sealed record MembershipLevelDto(int Level, int MinPoints, IReadOnlyList<string> Benefits);
public sealed record MembershipProgressDto(int Level, int MinPoints, int? NextLevel, int? NextLevelMinPoints, long PointsToNextLevel, decimal ProgressPercent, IReadOnlyList<string> Benefits);
public sealed record UpdateMembershipLevelsRequest(IReadOnlyList<MembershipLevelDto> Levels);
public sealed record CheckInDto(DateOnly Date, int RewardPoints, int StreakDays, long Balance, long DebtRepaid = 0);
public sealed record PointDto(Guid Id, string Type, long Points, long BalanceAfter, string Description, DateTimeOffset CreatedAt);
public sealed record VoucherDto(Guid Id, string Code, string Name, string Description, string DiscountType, decimal DiscountValue, decimal? MaxDiscount, decimal MinOrderAmount, DateTimeOffset ExpiresAt, bool Claimed, long PointCost = 0, int MinLevel = 1, bool IsFlashSale = false);
public sealed record NotificationDto(Guid Id, string Title, string Body, string TargetType, Guid? TargetId, DateTimeOffset? ReadAt, DateTimeOffset CreatedAt);
public sealed record FeedbackRequest(string Type, string Subject, string Content);
public sealed record FeedbackDto(Guid Id, string Subject, string Content, string Status, string AdminResponse, DateTimeOffset CreatedAt);
public sealed record AffiliateDto(string Code, decimal AvailableBalance, decimal PendingBalance, int TotalReferrals, decimal DebtBalance = 0, AffiliateRankProgressDto? RankInfo = null);
public sealed record AffiliateRankDto(int Rank, string Name, int MinSuccessfulReferrals, decimal CommissionRate);
public sealed record AffiliateRankProgressDto(int Rank, string Name, int SuccessfulReferrals, decimal CommissionRate, string? NextRank, int? NextRankMinReferrals, int ReferralsToNextRank);
public sealed record UpdateAffiliateRanksRequest(IReadOnlyList<AffiliateRankDto> Ranks);
public sealed record ReferralDto(Guid Id, string FullName, string Status, DateTimeOffset CreatedAt);
public sealed record QuotationDto(Guid Id, string QuoteCode, decimal Amount, string Description, DateTimeOffset ValidUntil, string Status);
