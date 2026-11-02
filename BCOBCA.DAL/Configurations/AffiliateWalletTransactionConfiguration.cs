using BCOBCA.DAL.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BCOBCA.DAL.Configurations;

public sealed class AffiliateWalletTransactionConfiguration : IEntityTypeConfiguration<AffiliateWalletTransaction>
{
    public void Configure(EntityTypeBuilder<AffiliateWalletTransaction> builder)
    {
        builder.ToTable("AffiliateWalletTransactions");
        builder.HasKey(x => x.Id);
        builder.ToTable(t => t.HasCheckConstraint("CK_AffiliateWalletTransactions_Balance", "\"BalanceAfter\" >= 0 AND ((\"Type\" IN ('CREDIT','PENDING_CREDIT','DEBT_CREDIT') AND \"Amount\" > 0) OR (\"Type\" IN ('DEBIT','PENDING_DEBIT','DEBT_DEBIT','RECOVERY_DEBIT') AND \"Amount\" < 0))"));
        builder.HasIndex(x => new { x.WalletId, x.Type, x.ReferenceType, x.ReferenceId }).IsUnique();
        builder.Property(x => x.WalletId).HasColumnType("uuid").IsRequired();
        builder.HasOne<AffiliateWallet>().WithMany().HasForeignKey(x => x.WalletId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.Type).HasColumnType("varchar(30)").IsRequired();
        builder.Property(x => x.Amount).HasColumnType("numeric(18,2)").IsRequired();
        builder.Property(x => x.BalanceAfter).HasColumnType("numeric(18,2)").IsRequired();
        builder.Property(x => x.ReferenceType).HasColumnType("varchar(30)").IsRequired();
        builder.Property(x => x.ReferenceId).HasColumnType("uuid").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnType("timestamptz").IsRequired();
    }
}
