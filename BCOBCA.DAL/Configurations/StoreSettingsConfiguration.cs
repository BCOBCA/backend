using BCOBCA.DAL.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BCOBCA.DAL.Configurations;

public sealed class StoreSettingsConfiguration : IEntityTypeConfiguration<StoreSettings>
{
    public void Configure(EntityTypeBuilder<StoreSettings> b)
    {
        b.ToTable("StoreSettings");
        b.HasKey(x => x.StoreId);
        b.HasOne<Store>().WithOne().HasForeignKey<StoreSettings>(x => x.StoreId).OnDelete(DeleteBehavior.ClientNoAction);
    }
}
