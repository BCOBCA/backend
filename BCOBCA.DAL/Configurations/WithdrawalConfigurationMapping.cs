using BCOBCA.DAL.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BCOBCA.DAL.Configurations;

public sealed class WithdrawalConfigurationMapping : IEntityTypeConfiguration<WithdrawalConfiguration>
{
    public void Configure(EntityTypeBuilder<WithdrawalConfiguration> builder)
    {
        builder.ToTable("WithdrawalConfigurations", t =>
        {
            t.HasCheckConstraint("CK_WithdrawalConfigurations_Singleton", $"\"Id\" = '{WithdrawalConfiguration.SingletonId}'::uuid");
            t.HasCheckConstraint("CK_WithdrawalConfigurations_Amounts", "\"FeeAmount\" >= 0 AND \"MinimumAmount\" > \"FeeAmount\" AND \"MaximumAmount\" >= \"MinimumAmount\"");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.FeeAmount).HasPrecision(18, 2);
        builder.Property(x => x.MinimumAmount).HasPrecision(18, 2);
        builder.Property(x => x.MaximumAmount).HasPrecision(18, 2);
        builder.Property(x => x.Revision).IsConcurrencyToken();
        builder.Property(x => x.UpdatedAt).HasColumnType("timestamptz");
        builder.HasOne<Account>().WithMany().HasForeignKey(x => x.UpdatedByAccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasData(new WithdrawalConfiguration());
    }
}
