using System.Data;
using System.Data.Common;
using Dapper;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace CheckoutService.Infrastructure.Database;

public sealed class DatabaseContext : IDatabaseContext, IDisposable
{
    private readonly NpgsqlConnection _connection;
    private DbTransaction? _transaction;

    public DatabaseContext(IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("CheckoutDb")
            ?? throw new InvalidOperationException("CheckoutDb connection string not configured");

        _connection = new NpgsqlConnection(connectionString);
    }

    public IDbConnection Connection => _connection;

    public IDbTransaction? Transaction => _transaction;

    public async Task EnsureConnectionOpenAsync(CancellationToken cancellationToken)
    {
        if (_connection.State != ConnectionState.Open)
        {
            await _connection.OpenAsync(cancellationToken);
        }
    }

    public async Task EnsureTransactionAsync(CancellationToken cancellationToken)
    {
        await EnsureConnectionOpenAsync(cancellationToken);

        if (_transaction is null)
        {
            _transaction = await _connection.BeginTransactionAsync(cancellationToken);
        }
    }

    public async Task CommitAsync(CancellationToken cancellationToken)
    {
        if (_transaction is null)
        {
            return;
        }

        await _transaction.CommitAsync(cancellationToken);
        await _transaction.DisposeAsync();
        _transaction = null;
    }

    public async Task RollbackAsync()
    {
        if (_transaction is null)
        {
            return;
        }

        await _transaction.RollbackAsync();
        await _transaction.DisposeAsync();
        _transaction = null;
    }

    public void Dispose()
    {
        if (_transaction is not null)
        {
            _transaction.Rollback();
            _transaction.Dispose();
            _transaction = null;
        }

        if (_connection.State != ConnectionState.Closed)
        {
            _connection.Close();
        }

        _connection.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        if (_transaction is not null)
        {
            await _transaction.RollbackAsync();
            await _transaction.DisposeAsync();
            _transaction = null;
        }

        if (_connection.State != ConnectionState.Closed)
        {
            await _connection.CloseAsync();
        }

        await _connection.DisposeAsync();
    }
}
