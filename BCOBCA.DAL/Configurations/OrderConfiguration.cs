using BCOBCA.DAL.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BCOBCA.DAL.Configurations;

public sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("Orders");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.OrderCode).HasColumnType("varchar(40)").IsRequired();
        builder.HasIndex(x => x.OrderCode).IsUnique();
        builder.Property(x => x.UserId).HasColumnType("uuid").IsRequired();
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.Status).HasColumnType("varchar(30)").IsRequired();
        builder.Property(x => x.PaymentStatus).HasColumnType("varchar(30)").IsRequired();
        builder.Property(x => x.Subtotal).HasColumnType("numeric(18,2)").IsRequired();
        builder.Property(x => x.DiscountAmount).HasColumnType("numeric(18,2)").IsRequired();
        builder.Property(x => x.ShippingFee).HasColumnType("numeric(18,2)").IsRequired();
        builder.Property(x => x.ShippingDiscountAmount).HasColumnType("numeric(18,2)").HasDefaultValue(0m);
        builder.ToTable(t => t.HasCheckConstraint("CK_Orders_ShippingDiscount", "\"ShippingDiscountAmount\" >= 0 AND \"ShippingDiscountAmount\" <= \"ShippingFee\""));
        builder.Property(x => x.TotalAmount).HasColumnType("numeric(18,2)").IsRequired();
        builder.Property(x => x.RecipientName).HasColumnType("varchar(150)").IsRequired();
        builder.Property(x => x.RecipientPhone).HasColumnType("varchar(20)").IsRequired();
        builder.Property(x => x.ShippingAddress).HasColumnType("text").IsRequired();
        builder.Property(x => x.Note).HasColumnType("text").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnType("timestamptz").IsRequired();
        builder.Property(x => x.ConfirmedAt).HasColumnType("timestamptz");
        builder.Property(x => x.CompletedAt).HasColumnType("timestamptz");
        builder.Property(x => x.CancelledAt).HasColumnType("timestamptz");
        builder.Property(x => x.IdempotencyKey).HasColumnType("varchar(100)").IsRequired();
        builder.Property(x => x.RequestHash).HasColumnType("varchar(64)").IsRequired();
        builder.Property(x => x.Version).IsRowVersion();
        builder.HasIndex(x => new { x.UserId, x.IdempotencyKey }).IsUnique();
    }
}
