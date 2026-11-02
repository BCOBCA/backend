using BCOBCA.DAL.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BCOBCA.DAL.Configurations;

public sealed class AffiliateAttributionConfiguration : IEntityTypeConfiguration<AffiliateAttribution>
{
    public void Configure(EntityTypeBuilder<AffiliateAttribution> builder)
    {
        builder.ToTable("AffiliateAttributions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.AffiliateProfileId).HasColumnType("uuid").IsRequired();
        builder.HasOne<AffiliateProfile>().WithMany().HasForeignKey(x => x.AffiliateProfileId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.OrderId).HasColumnType("uuid").IsRequired();
        builder.HasIndex(x => x.OrderId).IsUnique();
        builder.HasOne<Order>().WithMany().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.AffiliateLinkId).HasColumnType("uuid");
        builder.HasOne<AffiliateLink>().WithMany().HasForeignKey(x => x.AffiliateLinkId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.CreatedAt).HasColumnType("timestamptz").IsRequired();
    }
}
