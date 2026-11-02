using BCOBCA.DAL.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BCOBCA.DAL.Configurations;

public sealed class AffiliateWalletConfiguration : IEntityTypeConfiguration<AffiliateWallet>
{
    public void Configure(EntityTypeBuilder<AffiliateWallet> builder)
    {
        builder.ToTable("AffiliateWallets");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.DebtBalance).HasPrecision(18, 2);
        builder.ToTable(t => t.HasCheckConstraint("CK_AffiliateWallets_Debt", "\"DebtBalance\" >= 0"));
        builder.Property(x => x.AffiliateProfileId).HasColumnType("uuid").IsRequired();
        builder.HasIndex(x => x.AffiliateProfileId).IsUnique();
        builder.HasOne<AffiliateProfile>().WithMany().HasForeignKey(x => x.AffiliateProfileId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.AvailableBalance).HasColumnType("numeric(18,2)").IsRequired();
        builder.Property(x => x.PendingBalance).HasColumnType("numeric(18,2)").IsRequired();
        builder.Property(x => x.Currency).HasColumnType("char(3)").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnType("timestamptz").IsRequired();
        builder.Property(x => x.Version).IsRowVersion();
        builder.ToTable(t => t.HasCheckConstraint("CK_AffiliateWallets_Valid", "\"AvailableBalance\" >= 0 AND \"PendingBalance\" >= 0"));
    }
}
