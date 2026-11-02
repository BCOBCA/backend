using BCOBCA.DAL.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BCOBCA.DAL.Configurations;

public sealed class AccountRoleConfiguration : IEntityTypeConfiguration<AccountRole>
{
    public void Configure(EntityTypeBuilder<AccountRole> builder)
    {
        builder.ToTable("AccountRoles");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.AccountId).HasColumnType("uuid").IsRequired();
        builder.HasOne<Account>().WithMany().HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.Role).HasColumnType("varchar(30)").IsRequired();
        builder.HasIndex(x => new { x.AccountId, x.Role }).IsUnique();
    }
}
