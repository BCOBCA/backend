using BCOBCA.DAL.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BCOBCA.DAL.Configurations;

public sealed class AccountActionTokenConfiguration : IEntityTypeConfiguration<AccountActionToken>
{
    public void Configure(EntityTypeBuilder<AccountActionToken> builder)
    {
        builder.ToTable("AccountActionTokens");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.TokenHash).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Action).HasMaxLength(30).IsRequired();
        builder.Property(x => x.Version).IsRowVersion();
        builder.HasIndex(x => x.TokenHash).IsUnique();
        builder.HasOne<Account>().WithMany().HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Restrict);
    }
}
