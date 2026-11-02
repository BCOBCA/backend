using BCOBCA.DAL.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BCOBCA.DAL.Configurations;

public sealed class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> builder)
    {
        builder.ToTable("Accounts");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Email).HasColumnType("varchar(255)").IsRequired();
        builder.HasIndex(x => x.Email).IsUnique();
        builder.Property(x => x.Phone).HasColumnType("varchar(20)").IsRequired();
        builder.HasIndex(x => x.Phone).IsUnique();
        builder.Property(x => x.PasswordHash).HasColumnType("text").IsRequired();
        builder.Property(x => x.Role).HasColumnType("varchar(30)").IsRequired();
        builder.Property(x => x.Status).HasColumnType("smallint").IsRequired();
        builder.Property(x => x.EmailVerified).HasColumnType("boolean").IsRequired();
        builder.Property(x => x.PhoneVerified).HasColumnType("boolean").IsRequired();
        builder.Property(x => x.LastLoginAt).HasColumnType("timestamptz");
        builder.Property(x => x.CreatedAt).HasColumnType("timestamptz").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnType("timestamptz").IsRequired();
        builder.Property(x => x.IsDeleted).HasColumnType("boolean").IsRequired();
        builder.HasIndex(x => x.Phone).IsUnique().HasFilter("\"Phone\" <> ''");
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}
