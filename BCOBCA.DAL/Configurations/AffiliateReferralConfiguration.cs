using BCOBCA.DAL.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BCOBCA.DAL.Configurations;

public sealed class AffiliateReferralConfiguration : IEntityTypeConfiguration<AffiliateReferral>
{
    public void Configure(EntityTypeBuilder<AffiliateReferral> builder)
    {
        builder.ToTable("AffiliateReferrals");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ReferrerUserId).HasColumnType("uuid").IsRequired();
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.ReferrerUserId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.ReferredUserId).HasColumnType("uuid").IsRequired();
        builder.HasIndex(x => x.ReferredUserId).IsUnique();
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.ReferredUserId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.ReferralCode).HasColumnType("varchar(40)").IsRequired();
        builder.Property(x => x.Status).HasColumnType("varchar(20)").IsRequired();
        builder.Property(x => x.QualifiedAt).HasColumnType("timestamptz");
        builder.Property(x => x.CreatedAt).HasColumnType("timestamptz").IsRequired();
    }
}
