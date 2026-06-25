using System.Data;

namespace CheckoutService.Infrastructure.Database;

public interface IDatabaseContext : IDisposable, IAsyncDisposable
{
    IDbConnection Connection { get; }

    IDbTransaction? Transaction { get; }

    Task EnsureConnectionOpenAsync(CancellationToken cancellationToken);

    Task EnsureTransactionAsync(CancellationToken cancellationToken);

    Task CommitAsync(CancellationToken cancellationToken);

    Task RollbackAsync();
}
