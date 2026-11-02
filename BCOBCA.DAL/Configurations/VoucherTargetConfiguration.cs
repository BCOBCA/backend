using BCOBCA.DAL.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BCOBCA.DAL.Configurations;

public sealed class VoucherTargetConfiguration : IEntityTypeConfiguration<VoucherTarget>
{
    public void Configure(EntityTypeBuilder<VoucherTarget> builder)
    {
        builder.ToTable("VoucherTargets");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.VoucherId).HasColumnType("uuid").IsRequired();
        builder.HasOne<Voucher>().WithMany().HasForeignKey(x => x.VoucherId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.BrandId).HasColumnType("uuid");
        builder.HasOne<Brand>().WithMany().HasForeignKey(x => x.BrandId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.CategoryId).HasColumnType("uuid");
        builder.HasOne<Category>().WithMany().HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.ProductId).HasColumnType("uuid");
        builder.HasOne<Product>().WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
    }
}
