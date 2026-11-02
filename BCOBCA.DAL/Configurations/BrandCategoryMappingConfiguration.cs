using BCOBCA.DAL.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BCOBCA.DAL.Configurations;

public sealed class BrandCategoryMappingConfiguration : IEntityTypeConfiguration<BrandCategoryMapping>
{
    public void Configure(EntityTypeBuilder<BrandCategoryMapping> builder)
    {
        builder.ToTable("BrandCategoryMappings");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.BrandId).HasColumnType("uuid").IsRequired();
        builder.HasOne<Brand>().WithMany().HasForeignKey(x => x.BrandId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.BrandCategoryId).HasColumnType("uuid").IsRequired();
        builder.HasOne<BrandCategory>().WithMany().HasForeignKey(x => x.BrandCategoryId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.BrandId, x.BrandCategoryId }).IsUnique();
    }
}
