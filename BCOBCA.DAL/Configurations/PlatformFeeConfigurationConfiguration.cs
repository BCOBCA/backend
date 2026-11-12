using BCOBCA.DAL.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BCOBCA.DAL.Configurations;

public sealed class PlatformFeeConfigurationConfiguration : IEntityTypeConfiguration<PlatformFeeConfiguration>
{
    public void Configure(EntityTypeBuilder<PlatformFeeConfiguration> builder)
    {
        builder.ToTable("PlatformFeeConfigurations", t =>
        {
            t.HasCheckConstraint("CK_PlatformFeeConfigurations_Singleton", $"\"Id\" = '{PlatformFeeConfiguration.SingletonId}'::uuid");
            t.HasCheckConstraint("CK_PlatformFeeConfigurations_Rate", "\"PlatformFeeRate\" BETWEEN 0 AND 1");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.PlatformFeeRate).HasPrecision(7, 4).HasDefaultValue(0m);
        builder.Property(x => x.Revision).IsConcurrencyToken();
        builder.Property(x => x.UpdatedAt).HasColumnType("timestamptz");
        builder.HasOne<Account>().WithMany().HasForeignKey(x => x.UpdatedByAccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasData(new PlatformFeeConfiguration());
    }
}
