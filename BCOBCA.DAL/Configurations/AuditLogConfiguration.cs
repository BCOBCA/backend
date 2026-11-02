using BCOBCA.DAL.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BCOBCA.DAL.Configurations;

public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("AuditLogs");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.AccountId).HasColumnType("uuid");
        builder.HasOne<Account>().WithMany().HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.Action).HasColumnType("varchar(100)").IsRequired();
        builder.Property(x => x.EntityType).HasColumnType("varchar(100)").IsRequired();
        builder.Property(x => x.EntityId).HasColumnType("uuid");
        builder.Property(x => x.CreatedAt).HasColumnType("timestamptz").IsRequired();
    }
}
