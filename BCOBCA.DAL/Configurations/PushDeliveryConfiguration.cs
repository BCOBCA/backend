using BCOBCA.DAL.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace BCOBCA.DAL.Configurations;
public sealed class PushDeliveryConfiguration : IEntityTypeConfiguration<PushDelivery>
{
    public void Configure(EntityTypeBuilder<PushDelivery> b)
    {
        b.ToTable("PushDeliveries"); b.HasKey(x => x.Id);
        b.HasOne<Notification>().WithMany().HasForeignKey(x => x.NotificationId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<DeviceToken>().WithMany().HasForeignKey(x => x.DeviceTokenId).OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.Status).HasMaxLength(20); b.Property(x => x.TicketId).HasMaxLength(100);
        b.HasIndex(x => new { x.NotificationId, x.DeviceTokenId }).IsUnique();
        b.HasIndex(x => new { x.Status, x.NextAttemptAt });
    }
}
