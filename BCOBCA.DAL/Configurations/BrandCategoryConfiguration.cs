using BCOBCA.DAL.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BCOBCA.DAL.Configurations;

public sealed class BrandCategoryConfiguration : IEntityTypeConfiguration<BrandCategory>
{
    public void Configure(EntityTypeBuilder<BrandCategory> builder)
    {
        builder.ToTable("BrandCategories");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasColumnType("varchar(150)").IsRequired();
        builder.Property(x => x.Slug).HasColumnType("varchar(180)").IsRequired();
        builder.Property(x => x.IconUrl).HasColumnType("text").IsRequired();
        builder.Property(x => x.SortOrder).HasColumnType("int").IsRequired();
        builder.Property(x => x.IsActive).HasColumnType("boolean").IsRequired();
    }
}
