using BCOBCA.DAL.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BCOBCA.DAL.Configurations;

public sealed class StoreBranchConfiguration : IEntityTypeConfiguration<StoreBranch>
{
    public void Configure(EntityTypeBuilder<StoreBranch> b)
    {
        b.ToTable("StoreBranches", t =>
        {
            t.HasCheckConstraint("CK_StoreBranches_Latitude", "\"Latitude\" BETWEEN -90 AND 90");
            t.HasCheckConstraint("CK_StoreBranches_Longitude", "\"Longitude\" BETWEEN -180 AND 180");
            t.HasCheckConstraint("CK_StoreBranches_Pickup", "NOT \"IsPickup\" OR (\"IsActive\" AND \"Address\" <> '' AND \"Phone\" <> '' AND \"ProvinceName\" <> '' AND \"WardName\" <> '')");
            t.HasCheckConstraint("CK_StoreBranches_Return", "NOT \"IsReturn\" OR (\"IsActive\" AND \"Address\" <> '' AND \"Phone\" <> '' AND \"ProvinceName\" <> '' AND \"WardName\" <> '')");
        });
        b.HasKey(x => x.Id);
        b.HasOne<Store>().WithMany().HasForeignKey(x => x.StoreId).OnDelete(DeleteBehavior.ClientNoAction);
        b.HasOne<Account>().WithMany().HasForeignKey(x => x.ManagerAccountId).OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.Phone).HasMaxLength(20).IsRequired();
        b.Property(x => x.Address).HasColumnType("text").IsRequired();
        b.Property(x => x.ProvinceCode).HasMaxLength(20).IsRequired();
        b.Property(x => x.WardCode).HasMaxLength(20).IsRequired();
        b.Property(x => x.ProvinceName).HasMaxLength(150).IsRequired();
        b.Property(x => x.WardName).HasMaxLength(150).IsRequired();
        b.HasIndex(x => x.StoreId, "IX_StoreBranches_Pickup").IsUnique().HasFilter("\"IsPickup\"");
        b.HasIndex(x => x.StoreId, "IX_StoreBranches_Return").IsUnique().HasFilter("\"IsReturn\"");
        b.Property(x => x.Latitude).HasPrecision(10, 7);
        b.Property(x => x.Longitude).HasPrecision(10, 7);
        b.HasIndex(x => x.ManagerAccountId).IsUnique().HasFilter("\"ManagerAccountId\" IS NOT NULL");
        b.HasIndex(x => x.StoreId).IsUnique().HasFilter("\"IsMain\"").HasDatabaseName("IX_StoreBranches_Main");
        b.HasIndex(x => new { x.StoreId, x.IsActive, x.ProvinceCode });
    }
}
