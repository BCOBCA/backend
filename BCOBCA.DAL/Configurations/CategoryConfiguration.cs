using BCOBCA.DAL.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BCOBCA.DAL.Configurations;

public sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("Categories");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ParentId).HasColumnType("uuid");
        builder.HasOne<Category>().WithMany().HasForeignKey(x => x.ParentId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.Name).HasColumnType("varchar(150)").IsRequired();
        builder.Property(x => x.Slug).HasColumnType("varchar(180)").IsRequired();
        builder.Property(x => x.ImageUrl).HasColumnType("text").IsRequired();
        builder.Property(x => x.AttributeDefinitions).HasColumnType("jsonb").HasDefaultValue("[]").IsRequired();
        builder.ToTable(t => t.HasCheckConstraint("CK_Categories_AttributeDefinitions_Array", "jsonb_typeof(\"AttributeDefinitions\") = 'array'"));
        builder.Property(x => x.SortOrder).HasColumnType("int").IsRequired();
        builder.Property(x => x.IsActive).HasColumnType("boolean").IsRequired();
    }
}
