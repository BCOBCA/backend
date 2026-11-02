using BCOBCA.DAL.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BCOBCA.DAL.Configurations;

public sealed class ProductVariantConfiguration : IEntityTypeConfiguration<ProductVariant>
{
    public void Configure(EntityTypeBuilder<ProductVariant> builder)
    {
        builder.ToTable("ProductVariants");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ProductId).HasColumnType("uuid").IsRequired();
        builder.HasOne<Product>().WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.SKU).HasColumnType("varchar(100)").IsRequired();
        builder.HasIndex(x => x.SKU).IsUnique();
        builder.Property(x => x.VariantName).HasColumnType("varchar(150)").IsRequired();
        builder.Property(x => x.Attributes).HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.Price).HasColumnType("numeric(18,2)").IsRequired();
        builder.Property(x => x.SalePrice).HasColumnType("numeric(18,2)");
        builder.Property(x => x.StockQuantity).HasColumnType("int").IsRequired();
        builder.Property(x => x.ReservedQuantity).HasColumnType("int").IsRequired();
        builder.Property(x => x.IsActive).HasColumnType("boolean").IsRequired();
        builder.Property(x => x.Version).IsRowVersion();
        builder.ToTable(t => t.HasCheckConstraint("CK_ProductVariants_Valid", "\"StockQuantity\" >= 0 AND \"ReservedQuantity\" >= 0 AND \"ReservedQuantity\" <= \"StockQuantity\" AND \"Price\" >= 0"));
    }
}
