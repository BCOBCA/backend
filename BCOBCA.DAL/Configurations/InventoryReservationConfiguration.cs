using BCOBCA.DAL.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BCOBCA.DAL.Configurations;

public sealed class InventoryReservationConfiguration : IEntityTypeConfiguration<InventoryReservation>
{
    public void Configure(EntityTypeBuilder<InventoryReservation> builder)
    {
        builder.ToTable("InventoryReservations");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.VariantId).HasColumnType("uuid").IsRequired();
        builder.HasOne<ProductVariant>().WithMany().HasForeignKey(x => x.VariantId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.OrderId).HasColumnType("uuid").IsRequired();
        builder.HasOne<Order>().WithMany().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.Quantity).HasColumnType("int").IsRequired();
        builder.Property(x => x.Status).HasColumnType("varchar(30)").IsRequired();
        builder.Property(x => x.ExpiresAt).HasColumnType("timestamptz").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnType("timestamptz").IsRequired();
        builder.ToTable(t => t.HasCheckConstraint("CK_InventoryReservations_Valid", "\"Quantity\" > 0"));
    }
}
