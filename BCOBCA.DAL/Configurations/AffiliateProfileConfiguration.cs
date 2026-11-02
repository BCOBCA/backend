using BCOBCA.DAL.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BCOBCA.DAL.Configurations;

public sealed class AffiliateProfileConfiguration : IEntityTypeConfiguration<AffiliateProfile>
{
    public void Configure(EntityTypeBuilder<AffiliateProfile> builder)
    {
        builder.ToTable("AffiliateProfiles");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.UserId).HasColumnType("uuid").IsRequired();
        builder.HasIndex(x => x.UserId).IsUnique();
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.AffiliateCode).HasColumnType("varchar(40)").IsRequired();
        builder.HasIndex(x => x.AffiliateCode).IsUnique();
        builder.Property(x => x.Status).HasColumnType("varchar(20)").IsRequired();
        builder.Property(x => x.TotalReferrals).HasColumnType("int").IsRequired();
        builder.Property(x => x.JoinedAt).HasColumnType("timestamptz").IsRequired();
    }
}
