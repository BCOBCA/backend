using BCOBCA.DAL.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BCOBCA.DAL.Configurations;

public sealed class ProductImageConfiguration : IEntityTypeConfiguration<ProductImage>
{
    public void Configure(EntityTypeBuilder<ProductImage> builder)
    {
        builder.ToTable("ProductImages");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ProductId).HasColumnType("uuid").IsRequired();
        builder.HasOne<Product>().WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.ImageUrl).HasColumnType("text").IsRequired();
        builder.Property(x => x.IsPrimary).HasColumnType("boolean").IsRequired();
        builder.Property(x => x.SortOrder).HasColumnType("int").IsRequired();
    }
}
