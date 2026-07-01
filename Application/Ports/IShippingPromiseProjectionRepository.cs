using CheckoutService.Domain;

namespace CheckoutService.Application.Ports;

public interface IShippingPromiseProjectionRepository
{
    Task<ShippingPromiseProjection?> GetByCheckoutIdAsync(Guid checkoutId, CancellationToken cancellationToken);

    Task<bool> HasProcessedAsync(Guid eventId, string correlationId, Guid checkoutId, CancellationToken cancellationToken);

    Task RecordAsync(
        Guid eventId,
        string correlationId,
        Guid checkoutId,
        string promiseId,
        string mode,
        string carrier,
        DateOnly estimatedDeliveryDate,
        decimal cost,
        CancellationToken cancellationToken);
}
