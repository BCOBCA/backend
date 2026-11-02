using BCOBCA.DAL.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BCOBCA.DAL.Configurations;

public sealed class BrandMemberConfiguration : IEntityTypeConfiguration<BrandMember>
{
    public void Configure(EntityTypeBuilder<BrandMember> builder)
    {
        builder.ToTable("BrandMembers");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.BrandId).HasColumnType("uuid").IsRequired();
        builder.HasOne<Brand>().WithMany().HasForeignKey(x => x.BrandId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.AccountId).HasColumnType("uuid").IsRequired();
        builder.HasOne<Account>().WithMany().HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.Role).HasColumnType("varchar(30)").IsRequired();
        builder.Property(x => x.Status).HasColumnType("smallint").IsRequired();
        builder.Property(x => x.JoinedAt).HasColumnType("timestamptz").IsRequired();
        builder.HasIndex(x => new { x.BrandId, x.AccountId }).IsUnique();
    }
}
