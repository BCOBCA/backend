using BCOBCA.DAL.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BCOBCA.DAL.Configurations;

public sealed class DailyCheckInConfiguration : IEntityTypeConfiguration<DailyCheckIn>
{
    public void Configure(EntityTypeBuilder<DailyCheckIn> builder)
    {
        builder.ToTable("DailyCheckIns");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.UserId).HasColumnType("uuid").IsRequired();
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.CheckInDate).HasColumnType("date").IsRequired();
        builder.Property(x => x.RewardPoints).HasColumnType("int").IsRequired();
        builder.Property(x => x.StreakDays).HasColumnType("int").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnType("timestamptz").IsRequired();
        builder.HasIndex(x => new { x.UserId, x.CheckInDate }).IsUnique();
        builder.ToTable(t => t.HasCheckConstraint("CK_DailyCheckIns_Valid", "\"RewardPoints\" >= 0"));
    }
}
