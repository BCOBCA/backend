using BCOBCA.DAL.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BCOBCA.DAL.Configurations;

public sealed class PointExpirationConfiguration : IEntityTypeConfiguration<PointExpiration>
{
    public void Configure(EntityTypeBuilder<PointExpiration> builder)
    {
        builder.ToTable("PointExpirations");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.MembershipId).HasColumnType("uuid").IsRequired();
        builder.HasOne<UserMembership>().WithMany().HasForeignKey(x => x.MembershipId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.PointTransactionId).HasColumnType("uuid").IsRequired();
        builder.HasIndex(x => x.PointTransactionId).IsUnique();
        builder.HasOne<PointTransaction>().WithMany().HasForeignKey(x => x.PointTransactionId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.Points).HasColumnType("bigint").IsRequired();
        builder.Property(x => x.ExpiresAt).HasColumnType("timestamptz").IsRequired();
        builder.Property(x => x.ProcessedAt).HasColumnType("timestamptz");
    }
}
