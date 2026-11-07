using BCOBCA.DAL.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BCOBCA.DAL.Configurations;

public sealed class StoreConfiguration : IEntityTypeConfiguration<Store>
{
    public void Configure(EntityTypeBuilder<Store> b)
    {
        b.ToTable("Stores", t =>
        {
            t.HasCheckConstraint("CK_Stores_Type", "\"StoreType\" IN (0, 1)");
            t.HasCheckConstraint("CK_Stores_Status", "\"Status\" BETWEEN 0 AND 3");
        });
        b.HasKey(x => x.Id);
        b.HasOne<Account>().WithMany().HasForeignKey(x => x.OwnerAccountId).OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.Slug).HasMaxLength(220).IsRequired();
        b.Property(x => x.Email).HasMaxLength(255).IsRequired();
        b.Property(x => x.Phone).HasMaxLength(20).IsRequired();
        b.Property(x => x.Website).HasMaxLength(255).IsRequired();
        b.Property(x => x.LogoUrl).HasColumnType("text").IsRequired();
        b.Property(x => x.CoverUrl).HasColumnType("text").IsRequired();
        b.Property(x => x.Description).HasColumnType("text").IsRequired();
        b.Property(x => x.Provinces).HasColumnType("text");
        b.HasIndex(x => x.Slug).IsUnique();
        b.HasIndex(x => new { x.StoreType, x.Status, x.CreatedAt });
        b.HasQueryFilter(x => x.DeletedAt == null);
    }
}
