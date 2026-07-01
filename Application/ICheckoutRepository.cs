using CheckoutService.Domain;

namespace CheckoutService.Application;

public interface ICheckoutRepository
{
    Task<CheckoutSession?> GetByIdAsync(
        Guid checkoutId,
        CancellationToken cancellationToken);

    Task<CheckoutSession?> FindByIdempotencyKeyAsync(
        string idempotencyKey,
        CancellationToken cancellationToken);

    Task<CheckoutSession?> FindConfirmedByIdempotencyKeyAsync(
        string idempotencyKey,
        CancellationToken cancellationToken);

    Task AddAsync(
        CheckoutSession checkout,
        CancellationToken cancellationToken);

    Task UpdateAsync(
        CheckoutSession checkout,
        CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
