using BCOBCA.DAL.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BCOBCA.DAL.Configurations;

public sealed class UserMembershipConfiguration : IEntityTypeConfiguration<UserMembership>
{
    public void Configure(EntityTypeBuilder<UserMembership> builder)
    {
        builder.ToTable("UserMemberships");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.PointDebt).HasColumnType("bigint");
        builder.ToTable(t => t.HasCheckConstraint("CK_UserMemberships_PointDebt", "\"PointDebt\" >= 0"));
        builder.Property(x => x.UserId).HasColumnType("uuid").IsRequired();
        builder.HasIndex(x => x.UserId).IsUnique();
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.TierId).HasColumnType("uuid").IsRequired();
        builder.HasOne<MembershipTier>().WithMany().HasForeignKey(x => x.TierId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.MemberCode).HasColumnType("varchar(50)").IsRequired();
        builder.HasIndex(x => x.MemberCode).IsUnique();
        builder.Property(x => x.AvailablePoints).HasColumnType("bigint").IsRequired();
        builder.Property(x => x.LifetimeEarnedPoints).HasColumnType("bigint").IsRequired();
        builder.Property(x => x.TotalQualifiedSpend).HasColumnType("numeric(18,2)").IsRequired();
        builder.Property(x => x.JoinedAt).HasColumnType("timestamptz").IsRequired();
        builder.Property(x => x.TierExpiresAt).HasColumnType("timestamptz");
        builder.Property(x => x.Version).IsRowVersion();
        builder.ToTable(t => t.HasCheckConstraint("CK_UserMemberships_Valid", "\"AvailablePoints\" >= 0"));
    }
}
