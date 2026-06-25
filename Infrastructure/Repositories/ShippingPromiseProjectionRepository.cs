using CheckoutService.Application.Ports;
using CheckoutService.Domain;
using CheckoutService.Infrastructure.Database;
using Dapper;

namespace CheckoutService.Infrastructure.Repositories;

public sealed class ShippingPromiseProjectionRepository : IShippingPromiseProjectionRepository
{
    private readonly IDatabaseContext _databaseContext;

    public ShippingPromiseProjectionRepository(IDatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    public async Task<bool> HasProcessedAsync(Guid eventId, string correlationId, Guid checkoutId, CancellationToken cancellationToken)
    {
        const string sql = @"
            select exists(
                select 1
                from shipping_promise_projections
                where event_id = @EventId
                   or (correlation_id = @CorrelationId and checkout_id = @CheckoutId))";

        await _databaseContext.EnsureConnectionOpenAsync(cancellationToken);

        return await _databaseContext.Connection.ExecuteScalarAsync<bool>(
            sql,
            new { EventId = eventId, CorrelationId = correlationId, CheckoutId = checkoutId });
    }

    public async Task RecordAsync(
        Guid eventId,
        string correlationId,
        Guid checkoutId,
        string promiseId,
        string mode,
        string carrier,
        DateOnly estimatedDeliveryDate,
        decimal cost,
        CancellationToken cancellationToken)
    {
        if (await HasProcessedAsync(eventId, correlationId, checkoutId, cancellationToken))
        {
            return;
        }

        await _databaseContext.EnsureTransactionAsync(cancellationToken);

        const string sql = @"
            insert into shipping_promise_projections (
                id,
                event_id,
                correlation_id,
                checkout_id,
                promise_id,
                mode,
                carrier,
                cost,
                estimated_delivery_date,
                created_at
            ) values (
                @Id,
                @EventId,
                @CorrelationId,
                @CheckoutId,
                @PromiseId,
                @Mode,
                @Carrier,
                @Cost,
                @EstimatedDeliveryDate,
                @CreatedAt)";

        await _databaseContext.Connection.ExecuteAsync(
            sql,
            new
            {
                Id = Guid.NewGuid(),
                EventId = eventId,
                CorrelationId = correlationId,
                CheckoutId = checkoutId,
                PromiseId = promiseId,
                Mode = mode,
                Carrier = carrier,
                Cost = cost,
                EstimatedDeliveryDate = estimatedDeliveryDate,
                CreatedAt = DateTimeOffset.UtcNow
            },
            _databaseContext.Transaction);

        await _databaseContext.CommitAsync(cancellationToken);
    }
}
