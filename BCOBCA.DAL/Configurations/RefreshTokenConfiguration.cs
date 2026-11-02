using BCOBCA.DAL.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BCOBCA.DAL.Configurations;

public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("RefreshTokens");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.AccountId).HasColumnType("uuid").IsRequired();
        builder.HasOne<Account>().WithMany().HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.TokenHash).HasColumnType("text").IsRequired();
        builder.Property(x => x.DeviceName).HasColumnType("varchar(150)").IsRequired();
        builder.Property(x => x.ExpiresAt).HasColumnType("timestamptz").IsRequired();
        builder.Property(x => x.RevokedAt).HasColumnType("timestamptz");
        builder.Property(x => x.CreatedAt).HasColumnType("timestamptz").IsRequired();
        builder.Property(x => x.FamilyId).HasColumnType("uuid").IsRequired();
        builder.Property(x => x.ReplacedByTokenId).HasColumnType("uuid");
        builder.Property(x => x.Version).IsRowVersion();
    }
}
