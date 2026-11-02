using BCOBCA.DAL.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BCOBCA.DAL.Configurations;

public sealed class PointTransactionConfiguration : IEntityTypeConfiguration<PointTransaction>
{
    public void Configure(EntityTypeBuilder<PointTransaction> builder)
    {
        builder.ToTable("PointTransactions");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.MembershipId, x.Type, x.ReferenceType, x.ReferenceId }).IsUnique().HasFilter("\"ReferenceId\" IS NOT NULL");
        builder.Property(x => x.MembershipId).HasColumnType("uuid").IsRequired();
        builder.HasOne<UserMembership>().WithMany().HasForeignKey(x => x.MembershipId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.Type).HasColumnType("varchar(30)").IsRequired();
        builder.Property(x => x.Points).HasColumnType("bigint").IsRequired();
        builder.Property(x => x.BalanceAfter).HasColumnType("bigint").IsRequired();
        builder.Property(x => x.ReferenceType).HasColumnType("varchar(30)").IsRequired();
        builder.Property(x => x.ReferenceId).HasColumnType("uuid");
        builder.Property(x => x.Description).HasColumnType("text").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnType("timestamptz").IsRequired();
    }
}
