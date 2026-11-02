using BCOBCA.DAL.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BCOBCA.DAL.Configurations;

public sealed class AffiliateWithdrawalConfiguration : IEntityTypeConfiguration<AffiliateWithdrawal>
{
    public void Configure(EntityTypeBuilder<AffiliateWithdrawal> builder)
    {
        builder.ToTable("AffiliateWithdrawals");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.BankDetailsEncrypted).HasColumnType("text").IsRequired();
        builder.Property(x => x.IdempotencyKey).HasMaxLength(100).IsRequired();
        builder.Property(x => x.RequestHash).HasMaxLength(64).IsRequired();
        builder.HasIndex(x => new { x.WalletId, x.IdempotencyKey }).IsUnique();
        builder.HasIndex(x => x.TransactionReference).IsUnique().HasFilter("\"TransactionReference\" <> ''");
        builder.Property(x => x.WalletId).HasColumnType("uuid").IsRequired();
        builder.HasOne<AffiliateWallet>().WithMany().HasForeignKey(x => x.WalletId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.Amount).HasColumnType("numeric(18,2)").IsRequired();
        builder.Property(x => x.BankName).HasColumnType("varchar(150)").IsRequired();
        builder.Property(x => x.AccountNumber).HasColumnType("varchar(50)").IsRequired();
        builder.Property(x => x.AccountHolder).HasColumnType("varchar(150)").IsRequired();
        builder.Property(x => x.Status).HasColumnType("varchar(30)").IsRequired();
        builder.Property(x => x.TransactionReference).HasColumnType("varchar(150)").IsRequired();
        builder.Property(x => x.RequestedAt).HasColumnType("timestamptz").IsRequired();
        builder.Property(x => x.ProcessedAt).HasColumnType("timestamptz");
        builder.Property(x => x.Version).IsRowVersion();
        builder.ToTable(t => t.HasCheckConstraint("CK_AffiliateWithdrawals_Valid", "\"Amount\" > 0"));
    }
}
