using BCOBCA.DAL.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BCOBCA.DAL.Configurations;

public sealed class RefundConfiguration : IEntityTypeConfiguration<Refund>
{
    public void Configure(EntityTypeBuilder<Refund> builder)
    {
        builder.ToTable("Refunds");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.IdempotencyKey).HasMaxLength(100).IsRequired();
        builder.Property(x => x.RequestHash).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Version).IsRowVersion();
        builder.HasIndex(x => new { x.PaymentId, x.IdempotencyKey }).IsUnique();
        builder.HasIndex(x => x.PaymentId).IsUnique().HasFilter("\"Status\" IN ('PENDING','APPROVED','COMPLETED')");
        builder.HasIndex(x => x.ProviderRefundCode).IsUnique().HasFilter("\"ProviderRefundCode\" <> ''");
        builder.ToTable(t => t.HasCheckConstraint("CK_Refunds_Amount", "\"Amount\" > 0"));
        builder.Property(x => x.PaymentId).HasColumnType("uuid").IsRequired();
        builder.HasOne<Payment>().WithMany().HasForeignKey(x => x.PaymentId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.Amount).HasColumnType("numeric(18,2)").IsRequired();
        builder.Property(x => x.Reason).HasColumnType("text").IsRequired();
        builder.Property(x => x.Status).HasColumnType("varchar(30)").IsRequired();
        builder.Property(x => x.ProviderRefundCode).HasColumnType("varchar(150)").IsRequired();
        builder.Property(x => x.CompletedAt).HasColumnType("timestamptz");
        builder.Property(x => x.CreatedAt).HasColumnType("timestamptz").IsRequired();
    }
}
