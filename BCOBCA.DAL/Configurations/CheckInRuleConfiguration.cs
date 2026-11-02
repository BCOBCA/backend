using BCOBCA.DAL.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BCOBCA.DAL.Configurations;

public sealed class CheckInRuleConfiguration : IEntityTypeConfiguration<CheckInRule>
{
    public void Configure(EntityTypeBuilder<CheckInRule> builder)
    {
        builder.ToTable("CheckInRules");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasColumnType("varchar(100)").IsRequired();
        builder.Property(x => x.RewardPoints).HasColumnType("int").IsRequired();
        builder.Property(x => x.IsActive).HasColumnType("boolean").IsRequired();
    }
}
