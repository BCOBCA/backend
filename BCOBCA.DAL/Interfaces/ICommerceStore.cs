using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace BCOBCA.DAL.Interfaces;

// EF queries stay composable without a generic CRUD repository abstraction.
public interface ICommerceStore
{
    bool HasTransaction { get; }
    DbSet<T> Set<T>() where T : class;
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken);
}
