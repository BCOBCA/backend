using BCOBCA.DAL.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BCOBCA.DAL.Configurations;

public sealed class SellerOrderConfiguration : IEntityTypeConfiguration<SellerOrder>
{
    public void Configure(EntityTypeBuilder<SellerOrder> builder)
    {
        builder.ToTable("SellerOrders");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.OrderId).HasColumnType("uuid").IsRequired();
        builder.HasOne<Order>().WithMany().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.BrandId).HasColumnType("uuid").IsRequired();
        builder.HasOne<Brand>().WithMany().HasForeignKey(x => x.BrandId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.Status).HasColumnType("varchar(30)").IsRequired();
        builder.Property(x => x.Subtotal).HasColumnType("numeric(18,2)").IsRequired();
        builder.Property(x => x.DiscountAmount).HasColumnType("numeric(18,2)").IsRequired();
        builder.Property(x => x.TotalAmount).HasColumnType("numeric(18,2)").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnType("timestamptz").IsRequired();
        builder.HasIndex(x => new { x.OrderId, x.BrandId }).IsUnique();
    }
}
