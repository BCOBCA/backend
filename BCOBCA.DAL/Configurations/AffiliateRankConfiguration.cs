using BCOBCA.DAL.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BCOBCA.DAL.Configurations;

public sealed class AffiliateRankConfiguration : IEntityTypeConfiguration<AffiliateRank>
{
    public void Configure(EntityTypeBuilder<AffiliateRank> builder)
    {
        builder.ToTable("AffiliateRanks");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Rank).HasColumnType("int");
        builder.HasIndex(x => x.Rank).IsUnique();
        builder.Property(x => x.Name).HasColumnType("varchar(100)").IsRequired();
        builder.Property(x => x.MinSuccessfulReferrals).HasColumnType("int");
        builder.Property(x => x.CommissionRate).HasColumnType("numeric(5,4)");
    }
}
