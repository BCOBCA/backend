using BCOBCA.DAL.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BCOBCA.DAL.Configurations;

public sealed class ProductFavoriteConfiguration : IEntityTypeConfiguration<ProductFavorite>
{
    public void Configure(EntityTypeBuilder<ProductFavorite> builder)
    {
        builder.ToTable("ProductFavorites");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.UserId).HasColumnType("uuid").IsRequired();
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.ProductId).HasColumnType("uuid").IsRequired();
        builder.HasOne<Product>().WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.CreatedAt).HasColumnType("timestamptz").IsRequired();
        builder.HasIndex(x => new { x.UserId, x.ProductId }).IsUnique();
    }
}
