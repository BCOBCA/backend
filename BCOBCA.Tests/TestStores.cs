using BCOBCA.DAL.Context;
using BCOBCA.DAL.Entities;

namespace BCOBCA.Tests;

internal static class TestStores
{
    public static Guid Own(ApplicationDbContext db, Brand brand)
    {
        var slug = "test-store-" + brand.Id.ToString("N");
        var existing = db.Stores.Local.SingleOrDefault(s => s.Slug == slug);
        if (existing is not null) return existing.Id;
        var store = new Store { OwnerAccountId = brand.OwnerAccountId, StoreType = 1, Status = 1, Name = "Test store", Slug = slug };
        db.Stores.Add(store);
        return store.Id;
    }
}
