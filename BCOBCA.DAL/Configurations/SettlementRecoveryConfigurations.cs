using BCOBCA.DAL.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BCOBCA.DAL.Configurations;

public sealed class SellerSettlementAdjustmentConfiguration : IEntityTypeConfiguration<SellerSettlementAdjustment>
{
    public void Configure(EntityTypeBuilder<SellerSettlementAdjustment> builder)
    {
        builder.ToTable("SellerSettlementAdjustments", t => t.HasCheckConstraint("CK_SettlementAdjustment_Amounts", "\"Amount\" > 0 AND \"RemainingAmount\" BETWEEN 0 AND \"Amount\""));
        builder.HasKey(x => x.Id); builder.Property(x => x.Amount).HasPrecision(18, 2); builder.Property(x => x.RemainingAmount).HasPrecision(18, 2);
        builder.Property(x => x.Status).HasMaxLength(30).IsRequired(); builder.Property(x => x.Version).IsRowVersion();
        builder.HasIndex(x => new { x.SellerSettlementId, x.RefundId }).IsUnique();
        builder.HasOne<SellerSettlement>().WithMany().HasForeignKey(x => x.SellerSettlementId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Refund>().WithMany().HasForeignKey(x => x.RefundId).OnDelete(DeleteBehavior.Restrict);
    }
}
public sealed class SellerSettlementRecoveryConfiguration : IEntityTypeConfiguration<SellerSettlementRecovery>
{
    public void Configure(EntityTypeBuilder<SellerSettlementRecovery> builder)
    {
        builder.ToTable("SellerSettlementRecoveries", t => t.HasCheckConstraint("CK_SettlementRecovery_Amount", "\"Amount\" > 0"));
        builder.HasKey(x => x.Id); builder.Property(x => x.Amount).HasPrecision(18, 2);
        builder.HasIndex(x => new { x.AdjustmentId, x.AppliedToSettlementId }).IsUnique();
        builder.HasOne<SellerSettlementAdjustment>().WithMany().HasForeignKey(x => x.AdjustmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<SellerSettlement>().WithMany().HasForeignKey(x => x.AppliedToSettlementId).OnDelete(DeleteBehavior.Restrict);
    }
}
