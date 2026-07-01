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

    public async Task<ShippingPromiseProjection?> GetByCheckoutIdAsync(Guid checkoutId, CancellationToken cancellationToken)
    {
        const string sql = @"
            select
                id as Id,
                event_id as EventId,
                correlation_id as CorrelationId,
                checkout_id as CheckoutId,
                promise_id as PromiseId,
                mode as Mode,
                carrier as Carrier,
                estimated_delivery_date as EstimatedDeliveryDate,
                cost as Cost,
                created_at as ProcessedAt
            from shipping_promise_projections
            where checkout_id = @CheckoutId
            order by created_at desc
            limit 1";

        await _databaseContext.EnsureConnectionOpenAsync(cancellationToken);

        return await _databaseContext.Connection.QueryFirstOrDefaultAsync<ShippingPromiseProjection>(
            sql,
            new { CheckoutId = checkoutId });
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
