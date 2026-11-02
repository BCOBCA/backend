using BCOBCA.DAL.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BCOBCA.DAL.Configurations;

public sealed class MembershipTierConfiguration : IEntityTypeConfiguration<MembershipTier>
{
    public void Configure(EntityTypeBuilder<MembershipTier> builder)
    {
        builder.ToTable("MembershipTiers");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasColumnType("varchar(100)").IsRequired();
        builder.Property(x => x.MinSpend).HasColumnType("numeric(18,2)").IsRequired();
        builder.Property(x => x.MinPoints).HasColumnType("int").IsRequired();
        builder.Property(x => x.Benefits).HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.SortOrder).HasColumnType("int").IsRequired();
        builder.Property(x => x.IsActive).HasColumnType("boolean").IsRequired();
    }
}
