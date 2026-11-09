using BCOBCA.DAL.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BCOBCA.DAL.Configurations;

public sealed class SellerSettlementConfiguration : IEntityTypeConfiguration<SellerSettlement>
{
    public void Configure(EntityTypeBuilder<SellerSettlement> builder)
    {
        builder.ToTable("SellerSettlements");
        builder.HasKey(x => x.Id);
        builder.HasOne<Store>().WithMany().HasForeignKey(x => x.StoreId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.PaidAmount).HasPrecision(18, 2);
        builder.Property(x => x.TransactionReference).HasMaxLength(150).IsRequired();
        builder.Property(x => x.Version).IsRowVersion();
        builder.HasIndex(x => x.TransactionReference).IsUnique().HasFilter("\"TransactionReference\" <> ''");
        builder.ToTable(t => t.HasCheckConstraint("CK_SellerSettlements_Amounts", "\"GrossAmount\" >= 0 AND \"FeeAmount\" >= 0 AND \"NetAmount\" >= 0 AND \"PaidAmount\" BETWEEN 0 AND \"NetAmount\""));
        builder.Property(x => x.BrandId).HasColumnType("uuid").IsRequired();
        builder.HasOne<Brand>().WithMany().HasForeignKey(x => x.BrandId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.SellerOrderId).HasColumnType("uuid").IsRequired();
        builder.HasOne<SellerOrder>().WithMany().HasForeignKey(x => x.SellerOrderId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.GrossAmount).HasColumnType("numeric(18,2)").IsRequired();
        builder.Property(x => x.FeeAmount).HasColumnType("numeric(18,2)").IsRequired();
        builder.Property(x => x.NetAmount).HasColumnType("numeric(18,2)").IsRequired();
        builder.Property(x => x.Status).HasColumnType("varchar(30)").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnType("timestamptz").IsRequired();
        builder.Property(x => x.PaidAt).HasColumnType("timestamptz");
        builder.HasIndex(x => x.SellerOrderId).IsUnique();
    }
}
