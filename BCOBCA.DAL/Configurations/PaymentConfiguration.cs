using BCOBCA.DAL.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BCOBCA.DAL.Configurations;

public sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("Payments");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.OrderId).HasColumnType("uuid").IsRequired();
        builder.HasOne<Order>().WithMany().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.PaymentMethod).HasColumnType("varchar(30)").IsRequired();
        builder.Property(x => x.Amount).HasColumnType("numeric(18,2)").IsRequired();
        builder.Property(x => x.Currency).HasColumnType("char(3)").IsRequired();
        builder.Property(x => x.Status).HasColumnType("varchar(30)").IsRequired();
        builder.Property(x => x.ProviderReference).HasColumnType("varchar(150)").IsRequired();
        builder.Property(x => x.PaidAt).HasColumnType("timestamptz");
        builder.Property(x => x.CreatedAt).HasColumnType("timestamptz").IsRequired();
        builder.Property(x => x.Version).IsRowVersion();
    }
}
