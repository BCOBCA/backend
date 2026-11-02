using BCOBCA.DAL.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BCOBCA.DAL.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.AccountId).HasColumnType("uuid").IsRequired();
        builder.HasIndex(x => x.AccountId).IsUnique();
        builder.HasOne<Account>().WithMany().HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.FullName).HasColumnType("varchar(150)").IsRequired();
        builder.Property(x => x.Nickname).HasColumnType("varchar(100)").IsRequired();
        builder.Property(x => x.AvatarUrl).HasColumnType("text").IsRequired();
        builder.Property(x => x.CoverUrl).HasColumnType("text").IsRequired();
        builder.Property(x => x.Gender).HasColumnType("smallint").IsRequired();
        builder.Property(x => x.DateOfBirth).HasColumnType("date").IsRequired();
        builder.Property(x => x.ReferralCode).HasColumnType("varchar(30)").IsRequired();
        builder.HasIndex(x => x.ReferralCode).IsUnique();
        builder.Property(x => x.ReferredByUserId).HasColumnType("uuid");
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.ReferredByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.CreatedAt).HasColumnType("timestamptz").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnType("timestamptz").IsRequired();
        builder.Property(x => x.IsDeleted).HasColumnType("boolean").IsRequired();
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}
