using BCOBCA.DAL.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BCOBCA.DAL.Configurations;

public sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("Notifications");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.RecipientAccountId).HasColumnType("uuid").IsRequired();
        builder.HasOne<Account>().WithMany().HasForeignKey(x => x.RecipientAccountId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.Type).HasColumnType("varchar(50)").IsRequired();
        builder.Property(x => x.Title).HasColumnType("varchar(200)").IsRequired();
        builder.Property(x => x.Body).HasColumnType("text").IsRequired();
        builder.Property(x => x.TargetType).HasColumnType("varchar(50)").IsRequired();
        builder.Property(x => x.TargetId).HasColumnType("uuid");
        builder.Property(x => x.ReadAt).HasColumnType("timestamptz");
        builder.Property(x => x.CreatedAt).HasColumnType("timestamptz").IsRequired();
    }
}
