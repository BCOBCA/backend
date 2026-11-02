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
        builder.Property(x => x.Version).IsRowVersion();
    }
}
