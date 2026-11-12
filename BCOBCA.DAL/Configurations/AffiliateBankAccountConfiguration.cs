using BCOBCA.DAL.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BCOBCA.DAL.Configurations;

public sealed class AffiliateBankAccountConfiguration : IEntityTypeConfiguration<AffiliateBankAccount>
{
    public void Configure(EntityTypeBuilder<AffiliateBankAccount> builder)
    {
        builder.ToTable("AffiliateBankAccounts");
        builder.HasKey(x => x.Id);
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.UserId, x.CreatedAt });
        builder.Property(x => x.BankName).HasMaxLength(150).IsRequired();
        builder.Property(x => x.MaskedAccountNumber).HasMaxLength(50).IsRequired();
        builder.Property(x => x.DetailsEncrypted).HasColumnType("text").IsRequired();
    }
}
