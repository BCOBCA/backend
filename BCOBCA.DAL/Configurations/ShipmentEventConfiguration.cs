using BCOBCA.DAL.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace BCOBCA.DAL.Configurations;
public sealed class ShipmentEventConfiguration : IEntityTypeConfiguration<ShipmentEvent>
{
    public void Configure(EntityTypeBuilder<ShipmentEvent> b)
    {
        b.ToTable("ShipmentEvents"); b.HasKey(x => x.Id);
        b.HasOne<Shipment>().WithMany().HasForeignKey(x => x.ShipmentId).OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.Type).HasMaxLength(40); b.Property(x => x.Status).HasMaxLength(30);
        b.HasIndex(x => new { x.ShipmentId, x.Type, x.OccurredAt }).IsUnique();
    }
}
