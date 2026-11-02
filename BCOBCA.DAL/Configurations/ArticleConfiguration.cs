using BCOBCA.DAL.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BCOBCA.DAL.Configurations;

public sealed class ArticleConfiguration : IEntityTypeConfiguration<Article>
{
    public void Configure(EntityTypeBuilder<Article> builder)
    {
        builder.ToTable("Articles");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.BrandId).HasColumnType("uuid");
        builder.HasOne<Brand>().WithMany().HasForeignKey(x => x.BrandId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.Title).HasColumnType("varchar(250)").IsRequired();
        builder.Property(x => x.Slug).HasColumnType("varchar(280)").IsRequired();
        builder.Property(x => x.ThumbnailUrl).HasColumnType("text").IsRequired();
        builder.Property(x => x.Summary).HasColumnType("text").IsRequired();
        builder.Property(x => x.Content).HasColumnType("text").IsRequired();
        builder.Property(x => x.Status).HasColumnType("varchar(20)").IsRequired();
        builder.Property(x => x.PublishedAt).HasColumnType("timestamptz");
    }
}
