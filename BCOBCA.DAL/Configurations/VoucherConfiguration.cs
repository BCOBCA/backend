using BCOBCA.DAL.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BCOBCA.DAL.Configurations;

public sealed class VoucherConfiguration : IEntityTypeConfiguration<Voucher>
{
    public void Configure(EntityTypeBuilder<Voucher> builder)
    {
        builder.ToTable("Vouchers");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.StoreId).HasColumnType("uuid");
        builder.HasOne<Store>().WithMany().HasForeignKey(x => x.StoreId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.Code).HasColumnType("varchar(50)").IsRequired();
        builder.HasIndex(x => x.Code).IsUnique();
        builder.Property(x => x.Name).HasColumnType("varchar(150)").IsRequired();
        builder.Property(x => x.Description).HasColumnType("text").IsRequired();
        builder.Property(x => x.DiscountType).HasColumnType("varchar(20)").IsRequired();
        builder.Property(x => x.DiscountValue).HasColumnType("numeric(18,2)").IsRequired();
        builder.Property(x => x.MaxDiscount).HasColumnType("numeric(18,2)");
        builder.Property(x => x.MinOrderAmount).HasColumnType("numeric(18,2)").IsRequired();
        builder.Property(x => x.UsageLimit).HasColumnType("int");
        builder.Property(x => x.PerUserLimit).HasColumnType("int").IsRequired();
        builder.Property(x => x.StartsAt).HasColumnType("timestamptz").IsRequired();
        builder.Property(x => x.ExpiresAt).HasColumnType("timestamptz").IsRequired();
        builder.Property(x => x.Status).HasColumnType("varchar(20)").IsRequired();
        builder.Property(x => x.PointCost).HasColumnType("bigint").HasDefaultValue(0L);
        builder.Property(x => x.MinLevel).HasDefaultValue(1);
        builder.Property(x => x.IsFlashSale).HasDefaultValue(false);
        builder.ToTable(t => t.HasCheckConstraint("CK_Vouchers_Membership", "\"PointCost\" >= 0 AND \"MinLevel\" BETWEEN 1 AND 10 AND (\"PointCost\" = 0 OR \"PerUserLimit\" = 1)"));
        builder.Property(x => x.Version).IsRowVersion();
    }
}
