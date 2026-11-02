using BCOBCA.DAL.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BCOBCA.DAL.Configurations;

public sealed class ShipmentConfiguration : IEntityTypeConfiguration<Shipment>
{
    public void Configure(EntityTypeBuilder<Shipment> builder)
    {
        builder.ToTable("Shipments");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.OrderId, x.Carrier, x.TrackingCode }).IsUnique();
        builder.Property(x => x.OrderId).HasColumnType("uuid").IsRequired();
        builder.HasOne<Order>().WithMany().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.Carrier).HasColumnType("varchar(100)").IsRequired();
        builder.Property(x => x.TrackingCode).HasColumnType("varchar(100)").IsRequired();
        builder.Property(x => x.Status).HasColumnType("varchar(30)").IsRequired();
        builder.Property(x => x.ShippedAt).HasColumnType("timestamptz");
        builder.Property(x => x.DeliveredAt).HasColumnType("timestamptz");
    }
}
