using BCOBCA.DAL.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BCOBCA.DAL.Configurations;

public sealed class AffiliateVisitConfiguration : IEntityTypeConfiguration<AffiliateVisit>
{
    public void Configure(EntityTypeBuilder<AffiliateVisit> builder)
    {
        builder.ToTable("AffiliateVisits"); builder.HasKey(x => x.Id);
        builder.HasOne<AffiliateLink>().WithMany().HasForeignKey(x => x.AffiliateLinkId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.TokenHash).HasColumnType("varchar(64)").IsRequired();
        builder.HasIndex(x => x.TokenHash).IsUnique();
        builder.Property(x => x.CreatedAt).HasColumnType("timestamptz");
        builder.Property(x => x.ExpiresAt).HasColumnType("timestamptz");
        builder.HasIndex(x => new { x.AffiliateLinkId, x.CreatedAt });
        builder.HasIndex(x => x.ExpiresAt);
    }
}
