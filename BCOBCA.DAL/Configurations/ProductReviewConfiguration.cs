using BCOBCA.DAL.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BCOBCA.DAL.Configurations;

public sealed class ProductReviewConfiguration : IEntityTypeConfiguration<ProductReview>
{
    public void Configure(EntityTypeBuilder<ProductReview> builder)
    {
        builder.ToTable("ProductReviews");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ProductId).HasColumnType("uuid").IsRequired();
        builder.HasOne<Product>().WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.UserId).HasColumnType("uuid").IsRequired();
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.OrderItemId).HasColumnType("uuid").IsRequired();
        builder.HasOne<OrderItem>().WithMany().HasForeignKey(x => x.OrderItemId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.Rating).HasColumnType("smallint").IsRequired();
        builder.Property(x => x.Content).HasColumnType("text").IsRequired();
        builder.Property(x => x.Status).HasColumnType("smallint").IsRequired();
        builder.Property(x => x.Revision).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.Version).IsRowVersion();
        builder.Property(x => x.CreatedAt).HasColumnType("timestamptz").IsRequired();
        builder.HasIndex(x => new { x.UserId, x.OrderItemId }).IsUnique();
        builder.ToTable(t => t.HasCheckConstraint("CK_ProductReviews_Valid", "\"Rating\" BETWEEN 1 AND 5"));
    }
}
