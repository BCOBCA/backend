using BCOBCA.DAL.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BCOBCA.DAL.Configurations;

public sealed class ProductReviewImageConfiguration : IEntityTypeConfiguration<ProductReviewImage>
{
    public void Configure(EntityTypeBuilder<ProductReviewImage> builder)
    {
        builder.ToTable("ProductReviewImages");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ProductReviewId).HasColumnType("uuid").IsRequired();
        builder.HasOne<ProductReview>().WithMany().HasForeignKey(x => x.ProductReviewId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.ImageUrl).HasColumnType("text").IsRequired();
        builder.Property(x => x.SortOrder).HasColumnType("int").IsRequired();
    }
}
