namespace BCOBCA.DAL.Entities;

public sealed class DailyCheckIn
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public DateOnly CheckInDate { get; set; }
    public int RewardPoints { get; set; }
    public int StreakDays { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
