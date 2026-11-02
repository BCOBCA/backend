using BCOBCA.DAL.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BCOBCA.DAL.Configurations;

public sealed class CartItemConfiguration : IEntityTypeConfiguration<CartItem>
{
    public void Configure(EntityTypeBuilder<CartItem> builder)
    {
        builder.ToTable("CartItems");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.CartId).HasColumnType("uuid").IsRequired();
        builder.HasOne<Cart>().WithMany().HasForeignKey(x => x.CartId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.ProductId).HasColumnType("uuid").IsRequired();
        builder.HasOne<Product>().WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.VariantId).HasColumnType("uuid");
        builder.HasOne<ProductVariant>().WithMany().HasForeignKey(x => x.VariantId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.Quantity).HasColumnType("int").IsRequired();
        builder.Property(x => x.IsSelected).HasColumnType("boolean").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnType("timestamptz").IsRequired();
        builder.ToTable(t => t.HasCheckConstraint("CK_CartItems_Valid", "\"Quantity\" > 0"));
    }
}
