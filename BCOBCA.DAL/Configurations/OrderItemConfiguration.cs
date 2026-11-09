using BCOBCA.DAL.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BCOBCA.DAL.Configurations;

public sealed class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> builder)
    {
        builder.ToTable("OrderItems");
        builder.HasKey(x => x.Id);
        builder.HasOne<Store>().WithMany().HasForeignKey(x => x.StoreId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.OrderId).HasColumnType("uuid").IsRequired();
        builder.HasOne<Order>().WithMany().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.ProductId).HasColumnType("uuid").IsRequired();
        builder.HasOne<Product>().WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.VariantId).HasColumnType("uuid");
        builder.HasOne<ProductVariant>().WithMany().HasForeignKey(x => x.VariantId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.BrandId).HasColumnType("uuid").IsRequired();
        builder.HasOne<Brand>().WithMany().HasForeignKey(x => x.BrandId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.ProductName).HasColumnType("varchar(250)").IsRequired();
        builder.Property(x => x.SKU).HasColumnType("varchar(100)").IsRequired();
        builder.Property(x => x.Quantity).HasColumnType("int").IsRequired();
        builder.Property(x => x.UnitPrice).HasColumnType("numeric(18,2)").IsRequired();
        builder.Property(x => x.DiscountAmount).HasColumnType("numeric(18,2)").IsRequired();
        builder.Property(x => x.TotalPrice).HasColumnType("numeric(18,2)").IsRequired();
        builder.ToTable(t => t.HasCheckConstraint("CK_OrderItems_Valid", "\"Quantity\" > 0 AND \"UnitPrice\" >= 0 AND \"TotalPrice\" >= 0"));
    }
}
