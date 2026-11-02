using BCOBCA.DAL.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BCOBCA.DAL.Configurations;

public sealed class DeviceTokenConfiguration : IEntityTypeConfiguration<DeviceToken>
{
    public void Configure(EntityTypeBuilder<DeviceToken> builder)
    {
        builder.ToTable("DeviceTokens");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.AccountId).HasColumnType("uuid").IsRequired();
        builder.HasOne<Account>().WithMany().HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.TokenHash).HasColumnType("text").IsRequired();
        builder.HasIndex(x => x.TokenHash).IsUnique();
        builder.Property(x => x.Platform).HasColumnType("varchar(20)").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnType("timestamptz").IsRequired();
    }
}
