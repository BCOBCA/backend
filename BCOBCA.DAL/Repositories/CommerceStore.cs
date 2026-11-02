using System.Data;
using BCOBCA.DAL.Context;
using BCOBCA.DAL.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace BCOBCA.DAL.Repositories;

public sealed class CommerceStore(ApplicationDbContext db) : ICommerceStore
{
    public bool HasTransaction => db.Database.CurrentTransaction is not null;
    public DbSet<T> Set<T>() where T : class => db.Set<T>();
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => db.SaveChangesAsync(cancellationToken);
    public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken)
        => db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
}
