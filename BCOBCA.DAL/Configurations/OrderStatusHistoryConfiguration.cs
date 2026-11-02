using BCOBCA.DAL.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BCOBCA.DAL.Configurations;

public sealed class OrderStatusHistoryConfiguration : IEntityTypeConfiguration<OrderStatusHistory>
{
    public void Configure(EntityTypeBuilder<OrderStatusHistory> builder)
    {
        builder.ToTable("OrderStatusHistories");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.OrderId).HasColumnType("uuid").IsRequired();
        builder.HasOne<Order>().WithMany().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.OldStatus).HasColumnType("varchar(30)").IsRequired();
        builder.Property(x => x.NewStatus).HasColumnType("varchar(30)").IsRequired();
        builder.Property(x => x.ChangedByAccountId).HasColumnType("uuid");
        builder.HasOne<Account>().WithMany().HasForeignKey(x => x.ChangedByAccountId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.Note).HasColumnType("text").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnType("timestamptz").IsRequired();
    }
}
