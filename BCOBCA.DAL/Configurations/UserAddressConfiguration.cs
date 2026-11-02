using BCOBCA.DAL.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BCOBCA.DAL.Configurations;

public sealed class UserAddressConfiguration : IEntityTypeConfiguration<UserAddress>
{
    public void Configure(EntityTypeBuilder<UserAddress> builder)
    {
        builder.ToTable("UserAddresses");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.UserId).HasColumnType("uuid").IsRequired();
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.RecipientName).HasColumnType("varchar(150)").IsRequired();
        builder.Property(x => x.Phone).HasColumnType("varchar(20)").IsRequired();
        builder.Property(x => x.ProvinceCode).HasColumnType("varchar(20)").IsRequired();
        builder.Property(x => x.WardCode).HasColumnType("varchar(20)").IsRequired();
        builder.Property(x => x.AddressLine).HasColumnType("text").IsRequired();
        builder.Property(x => x.AddressType).HasColumnType("smallint").IsRequired();
        builder.Property(x => x.IsDefault).HasColumnType("boolean").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnType("timestamptz").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnType("timestamptz").IsRequired();
    }
}
