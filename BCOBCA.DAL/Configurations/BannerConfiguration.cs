using BCOBCA.DAL.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BCOBCA.DAL.Configurations;

public sealed class BannerConfiguration : IEntityTypeConfiguration<Banner>
{
    public void Configure(EntityTypeBuilder<Banner> builder)
    {
        builder.ToTable("Banners");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ImageUrl).HasColumnType("text").IsRequired();
        builder.Property(x => x.TargetUrl).HasColumnType("text").IsRequired();
        builder.Property(x => x.StartsAt).HasColumnType("timestamptz").IsRequired();
        builder.Property(x => x.EndsAt).HasColumnType("timestamptz").IsRequired();
        builder.Property(x => x.IsActive).HasColumnType("boolean").IsRequired();
    }
}
