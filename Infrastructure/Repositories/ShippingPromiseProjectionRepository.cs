using CheckoutService.Application.Ports;
using CheckoutService.Domain;
using Microsoft.EntityFrameworkCore;

namespace CheckoutService.Infrastructure.Repositories;

public sealed class ShippingPromiseProjectionRepository : IShippingPromiseProjectionRepository
{
    private readonly CheckoutDbContext _dbContext;

    public ShippingPromiseProjectionRepository(CheckoutDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<bool> HasProcessedAsync(Guid eventId, string correlationId, Guid checkoutId, CancellationToken cancellationToken)
    {
        return _dbContext.ShippingPromiseProjections.AnyAsync(
            x => x.EventId == eventId ||
                 (x.CorrelationId == correlationId && x.CheckoutId == checkoutId),
            cancellationToken);
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

        await _dbContext.ShippingPromiseProjections.AddAsync(
            new ShippingPromiseProjection(eventId, correlationId, checkoutId, promiseId, mode, carrier, estimatedDeliveryDate, cost),
            cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
