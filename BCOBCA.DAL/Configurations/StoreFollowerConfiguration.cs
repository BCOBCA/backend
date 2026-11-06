using BCOBCA.DAL.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BCOBCA.DAL.Configurations;

public sealed class StoreFollowerConfiguration : IEntityTypeConfiguration<StoreFollower>
{
    public void Configure(EntityTypeBuilder<StoreFollower> b)
    {
        b.ToTable("StoreFollowers");
        b.HasKey(x => new { x.StoreId, x.AccountId });
        b.HasOne<Store>().WithMany().HasForeignKey(x => x.StoreId).OnDelete(DeleteBehavior.ClientNoAction);
        b.HasOne<Account>().WithMany().HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Restrict);
    }
}
