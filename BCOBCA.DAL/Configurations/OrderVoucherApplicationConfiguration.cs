using BCOBCA.DAL.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BCOBCA.DAL.Configurations;

public sealed class OrderVoucherApplicationConfiguration : IEntityTypeConfiguration<OrderVoucherApplication>
{
    public void Configure(EntityTypeBuilder<OrderVoucherApplication> builder)
    {
        builder.ToTable("OrderVoucherApplications");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.OrderId).HasColumnType("uuid").IsRequired();
        builder.HasOne<Order>().WithMany().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.VoucherId).HasColumnType("uuid").IsRequired();
        builder.HasOne<Voucher>().WithMany().HasForeignKey(x => x.VoucherId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.DiscountAmount).HasColumnType("numeric(18,2)").IsRequired();
        builder.HasIndex(x => new { x.OrderId, x.VoucherId }).IsUnique();
    }
}
