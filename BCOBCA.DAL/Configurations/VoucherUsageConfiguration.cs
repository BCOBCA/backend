using BCOBCA.DAL.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BCOBCA.DAL.Configurations;

public sealed class VoucherUsageConfiguration : IEntityTypeConfiguration<VoucherUsage>
{
    public void Configure(EntityTypeBuilder<VoucherUsage> builder)
    {
        builder.ToTable("VoucherUsages");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.VoucherId).HasColumnType("uuid").IsRequired();
        builder.HasOne<Voucher>().WithMany().HasForeignKey(x => x.VoucherId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.UserId).HasColumnType("uuid").IsRequired();
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.OrderId).HasColumnType("uuid").IsRequired();
        builder.HasOne<Order>().WithMany().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.DiscountAmount).HasColumnType("numeric(18,2)").IsRequired();
        builder.Property(x => x.Status).HasColumnType("varchar(20)").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnType("timestamptz").IsRequired();
        builder.HasIndex(x => new { x.VoucherId, x.OrderId }).IsUnique();
    }
}
