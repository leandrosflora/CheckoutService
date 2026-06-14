using System.Collections.Concurrent;
using CheckoutService.Application.Ports;
using CheckoutService.Domain;

namespace CheckoutService.Infrastructure.Mocks;

public sealed class InMemoryShippingPromiseProjectionRepository : IShippingPromiseProjectionRepository
{
    private readonly ConcurrentDictionary<Guid, ShippingPromiseProjection> _projectionsByEventId = new();
    private readonly ConcurrentDictionary<string, Guid> _eventIdsByCorrelationAndCheckout = new(StringComparer.OrdinalIgnoreCase);

    public Task<bool> HasProcessedAsync(Guid eventId, string correlationId, Guid checkoutId, CancellationToken cancellationToken)
    {
        var processed = _projectionsByEventId.ContainsKey(eventId)
            || _eventIdsByCorrelationAndCheckout.ContainsKey(BuildCorrelationCheckoutKey(correlationId, checkoutId));

        return Task.FromResult(processed);
    }

    public Task RecordAsync(
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
        if (eventId == Guid.Empty || checkoutId == Guid.Empty || string.IsNullOrWhiteSpace(correlationId))
        {
            return Task.CompletedTask;
        }

        var key = BuildCorrelationCheckoutKey(correlationId, checkoutId);
        if (_projectionsByEventId.ContainsKey(eventId) || _eventIdsByCorrelationAndCheckout.ContainsKey(key))
        {
            return Task.CompletedTask;
        }

        var projection = new ShippingPromiseProjection(
            eventId,
            correlationId,
            checkoutId,
            promiseId,
            mode,
            carrier,
            estimatedDeliveryDate,
            cost);

        if (_projectionsByEventId.TryAdd(eventId, projection))
        {
            _eventIdsByCorrelationAndCheckout.TryAdd(key, eventId);
        }

        return Task.CompletedTask;
    }

    private static string BuildCorrelationCheckoutKey(string correlationId, Guid checkoutId)
    {
        return $"{correlationId}:{checkoutId}";
    }
}
