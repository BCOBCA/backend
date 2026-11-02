using BCOBCA.DAL.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BCOBCA.DAL.Configurations;

public sealed class AffiliateLinkConfiguration : IEntityTypeConfiguration<AffiliateLink>
{
    public void Configure(EntityTypeBuilder<AffiliateLink> builder)
    {
        builder.ToTable("AffiliateLinks");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.AffiliateProfileId).HasColumnType("uuid").IsRequired();
        builder.HasOne<AffiliateProfile>().WithMany().HasForeignKey(x => x.AffiliateProfileId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.ProductId).HasColumnType("uuid");
        builder.HasOne<Product>().WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.Code).HasColumnType("varchar(50)").IsRequired();
        builder.HasIndex(x => x.Code).IsUnique();
        builder.Property(x => x.CreatedAt).HasColumnType("timestamptz").IsRequired();
    }
}
