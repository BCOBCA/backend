using BCOBCA.DAL.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BCOBCA.DAL.Configurations;

public sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("Products");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.BrandId).HasColumnType("uuid").IsRequired();
        builder.HasOne<Brand>().WithMany().HasForeignKey(x => x.BrandId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.CategoryId).HasColumnType("uuid").IsRequired();
        builder.HasOne<Category>().WithMany().HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.Name).HasColumnType("varchar(250)").IsRequired();
        builder.Property(x => x.Slug).HasColumnType("varchar(280)").IsRequired();
        builder.Property(x => x.SKU).HasColumnType("varchar(100)").IsRequired();
        builder.Property(x => x.ShortDescription).HasColumnType("text").IsRequired();
        builder.Property(x => x.Description).HasColumnType("text").IsRequired();
        builder.Property(x => x.BasePrice).HasColumnType("numeric(18,2)").IsRequired();
        builder.Property(x => x.SalePrice).HasColumnType("numeric(18,2)");
        builder.Property(x => x.ProductType).HasColumnType("smallint").IsRequired();
        builder.Property(x => x.Status).HasColumnType("smallint").IsRequired();
        builder.Property(x => x.IsFeatured).HasColumnType("boolean").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnType("timestamptz").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnType("timestamptz").IsRequired();
        builder.Property(x => x.IsDeleted).HasColumnType("boolean").IsRequired();
        builder.HasQueryFilter(x => !x.IsDeleted);
        builder.Property(x => x.AffiliateEnabled).HasDefaultValue(true);
        builder.Property(x => x.AffiliateMaxRate).HasColumnType("numeric(7,4)");
        builder.Property(x => x.AffiliateBudget).HasColumnType("numeric(18,2)");
        builder.ToTable(t => t.HasCheckConstraint("CK_Products_AffiliatePolicy", "(\"AffiliateMaxRate\" IS NULL OR \"AffiliateMaxRate\" BETWEEN 0 AND 1) AND (\"AffiliateBudget\" IS NULL OR \"AffiliateBudget\" >= 0)"));
    }
}
