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
        builder.HasOne<Store>().WithMany().HasForeignKey(x => x.StoreId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.OrderId).HasColumnType("uuid").IsRequired();
        builder.HasOne<Order>().WithMany().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.BrandId).HasColumnType("uuid").IsRequired();
        builder.HasOne<Brand>().WithMany().HasForeignKey(x => x.BrandId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.Status).HasColumnType("varchar(30)").IsRequired();
        builder.Property(x => x.Subtotal).HasColumnType("numeric(18,2)").IsRequired();
        builder.Property(x => x.DiscountAmount).HasColumnType("numeric(18,2)").IsRequired();
        builder.Property(x => x.TotalAmount).HasColumnType("numeric(18,2)").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnType("timestamptz").IsRequired();
        builder.HasIndex(x => new { x.OrderId, x.BrandId }).IsUnique().HasFilter("\"StoreId\" IS NULL");
        builder.HasIndex(x => new { x.OrderId, x.StoreId, x.BrandId }).IsUnique().HasFilter("\"StoreId\" IS NOT NULL");
    }
}
