using BCOBCA.DAL.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BCOBCA.DAL.Configurations;

public sealed class BrandConfiguration : IEntityTypeConfiguration<Brand>
{
    public void Configure(EntityTypeBuilder<Brand> builder)
    {
        builder.ToTable("Brands");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.OwnerAccountId).HasColumnType("uuid").IsRequired();
        builder.HasOne<Account>().WithMany().HasForeignKey(x => x.OwnerAccountId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.Name).HasColumnType("varchar(200)").IsRequired();
        builder.Property(x => x.Slug).HasColumnType("varchar(220)").IsRequired();
        builder.HasIndex(x => x.Slug).IsUnique();
        builder.Property(x => x.LogoUrl).HasColumnType("text").IsRequired();
        builder.Property(x => x.CoverUrl).HasColumnType("text").IsRequired();
        builder.Property(x => x.Description).HasColumnType("text").IsRequired();
        builder.Property(x => x.Website).HasColumnType("text").IsRequired();
        builder.Property(x => x.ContactEmail).HasColumnType("varchar(255)").IsRequired();
        builder.Property(x => x.Phone).HasColumnType("varchar(20)").IsRequired();
        builder.Property(x => x.Address).HasColumnType("text").IsRequired();
        builder.Property(x => x.Status).HasColumnType("smallint").IsRequired();
        builder.Property(x => x.IsVerified).HasColumnType("boolean").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnType("timestamptz").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnType("timestamptz").IsRequired();
        builder.Property(x => x.IsDeleted).HasColumnType("boolean").IsRequired();
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}
