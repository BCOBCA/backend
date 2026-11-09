using BCOBCA.DAL.Entities;
using BCOBCA.DAL.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BCOBCA.BLL.Common;

internal static class ProductCatalogScope
{
    public static IQueryable<Product> Available(ICommerceStore store) => store.Set<Product>().AsNoTracking()
        .Where(p => p.Status == 1
            && store.Set<Brand>().Any(b => b.Id == p.BrandId && b.Status == 1)
            && store.Set<Store>().Any(s => s.Id == p.StoreId && s.StoreType == 1 && s.Status == 1 && s.DeletedAt == null));
}
