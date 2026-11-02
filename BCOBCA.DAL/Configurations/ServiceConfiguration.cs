using BCOBCA.DAL.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BCOBCA.DAL.Configurations;

public sealed class ServiceConfiguration : IEntityTypeConfiguration<Service>
{
    public void Configure(EntityTypeBuilder<Service> builder)
    {
        builder.ToTable("Services");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.BrandId).HasColumnType("uuid").IsRequired();
        builder.HasOne<Brand>().WithMany().HasForeignKey(x => x.BrandId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.Name).HasColumnType("varchar(250)").IsRequired();
        builder.Property(x => x.Slug).HasColumnType("varchar(280)").IsRequired();
        builder.Property(x => x.Description).HasColumnType("text").IsRequired();
        builder.Property(x => x.ThumbnailUrl).HasColumnType("text").IsRequired();
        builder.Property(x => x.PriceFrom).HasColumnType("numeric(18,2)");
        builder.Property(x => x.PricingType).HasColumnType("varchar(20)").IsRequired();
        builder.Property(x => x.Status).HasColumnType("varchar(20)").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnType("timestamptz").IsRequired();
        builder.Property(x => x.IsDeleted).HasColumnType("boolean").IsRequired();
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}
