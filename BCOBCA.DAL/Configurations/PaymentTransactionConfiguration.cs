using BCOBCA.DAL.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BCOBCA.DAL.Configurations;

public sealed class PaymentTransactionConfiguration : IEntityTypeConfiguration<PaymentTransaction>
{
    public void Configure(EntityTypeBuilder<PaymentTransaction> builder)
    {
        builder.ToTable("PaymentTransactions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.PaymentId).HasColumnType("uuid").IsRequired();
        builder.HasOne<Payment>().WithMany().HasForeignKey(x => x.PaymentId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.Provider).HasColumnType("varchar(30)").IsRequired();
        builder.Property(x => x.TransactionCode).HasColumnType("varchar(150)").IsRequired();
        builder.Property(x => x.EventId).HasColumnType("varchar(150)").IsRequired();
        builder.Property(x => x.Amount).HasColumnType("numeric(18,2)").IsRequired();
        builder.Property(x => x.Status).HasColumnType("varchar(30)").IsRequired();
        builder.Property(x => x.ResponseData).HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnType("timestamptz").IsRequired();
        builder.HasIndex(x => new { x.Provider, x.EventId }).IsUnique();
    }
}
