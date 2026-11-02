using BCOBCA.DAL.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BCOBCA.DAL.Configurations;

public sealed class UserVoucherConfiguration : IEntityTypeConfiguration<UserVoucher>
{
    public void Configure(EntityTypeBuilder<UserVoucher> builder)
    {
        builder.ToTable("UserVouchers");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.UserId).HasColumnType("uuid").IsRequired();
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.VoucherId).HasColumnType("uuid").IsRequired();
        builder.HasOne<Voucher>().WithMany().HasForeignKey(x => x.VoucherId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.Status).HasColumnType("varchar(20)").IsRequired();
        builder.Property(x => x.ClaimedAt).HasColumnType("timestamptz").IsRequired();
        builder.Property(x => x.UsedAt).HasColumnType("timestamptz");
        builder.HasIndex(x => new { x.UserId, x.VoucherId }).IsUnique();
    }
}
