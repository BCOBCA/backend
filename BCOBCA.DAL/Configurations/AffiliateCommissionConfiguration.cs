using BCOBCA.DAL.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BCOBCA.DAL.Configurations;

public sealed class AffiliateCommissionConfiguration : IEntityTypeConfiguration<AffiliateCommission>
{
    public void Configure(EntityTypeBuilder<AffiliateCommission> builder)
    {
        builder.ToTable("AffiliateCommissions");
        builder.HasKey(x => x.Id);
        builder.ToTable(t => t.HasCheckConstraint("CK_AffiliateCommissions_Amounts", "\"CommissionRate\" BETWEEN 0 AND 1 AND \"BaseAmount\" >= 0 AND \"CommissionAmount\" >= 0"));
        builder.Property(x => x.AffiliateProfileId).HasColumnType("uuid").IsRequired();
        builder.HasOne<AffiliateProfile>().WithMany().HasForeignKey(x => x.AffiliateProfileId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.OrderItemId).HasColumnType("uuid").IsRequired();
        builder.HasOne<OrderItem>().WithMany().HasForeignKey(x => x.OrderItemId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.CommissionRate).HasColumnType("numeric(7,4)").IsRequired();
        builder.Property(x => x.BaseAmount).HasColumnType("numeric(18,2)").IsRequired();
        builder.Property(x => x.CommissionAmount).HasColumnType("numeric(18,2)").IsRequired();
        builder.Property(x => x.Status).HasColumnType("varchar(20)").IsRequired();
        builder.Property(x => x.ApprovedAt).HasColumnType("timestamptz");
        builder.Property(x => x.CreatedAt).HasColumnType("timestamptz").IsRequired();
        builder.HasIndex(x => new { x.AffiliateProfileId, x.OrderItemId }).IsUnique();
        builder.Property(x => x.RankName).HasColumnType("varchar(100)").IsRequired();
        builder.Property(x => x.Rank).HasColumnType("int");
        builder.Property(x => x.PaidAmount).HasColumnType("numeric(18,2)");
        builder.Property(x => x.Version).IsRowVersion();
        builder.ToTable(t => t.HasCheckConstraint("CK_AffiliateCommissions_Paid", "\"PaidAmount\" >= 0 AND \"PaidAmount\" <= \"CommissionAmount\""));
    }
}
