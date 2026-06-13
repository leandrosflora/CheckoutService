using System.Collections.Concurrent;
using CheckoutService.Application;
using CheckoutService.Domain;

namespace CheckoutService.Infrastructure.Mocks;

public sealed class MockCheckoutRepository : ICheckoutRepository
{
    private static readonly ConcurrentDictionary<Guid, CheckoutSession> CheckoutsById = new();
    private static readonly ConcurrentDictionary<string, Guid> CheckoutsByIdempotencyKey = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, Guid> ConfirmedCheckoutsByIdempotencyKey = new(StringComparer.OrdinalIgnoreCase);

    public Task<CheckoutSession?> GetByIdAsync(
        Guid checkoutId,
        CancellationToken cancellationToken)
    {
        CheckoutsById.TryGetValue(checkoutId, out var checkout);

        return Task.FromResult(checkout);
    }

    public Task<CheckoutSession?> FindByIdempotencyKeyAsync(
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var checkout = CheckoutsByIdempotencyKey.TryGetValue(idempotencyKey, out var checkoutId)
            && CheckoutsById.TryGetValue(checkoutId, out var existing)
                ? existing
                : null;

        return Task.FromResult(checkout);
    }

    public Task<CheckoutSession?> FindConfirmedByIdempotencyKeyAsync(
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var checkout = ConfirmedCheckoutsByIdempotencyKey.TryGetValue(idempotencyKey, out var checkoutId)
            && CheckoutsById.TryGetValue(checkoutId, out var existing)
                ? existing
                : null;

        return Task.FromResult(checkout);
    }

    public Task AddAsync(
        CheckoutSession checkout,
        CancellationToken cancellationToken)
    {
        CheckoutsById[checkout.Id] = checkout;
        CheckoutsByIdempotencyKey[checkout.IdempotencyKey] = checkout.Id;

        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        foreach (var checkout in CheckoutsById.Values.Where(x => x.ConfirmationIdempotencyKey is not null))
        {
            ConfirmedCheckoutsByIdempotencyKey[checkout.ConfirmationIdempotencyKey!] = checkout.Id;
        }

        return Task.CompletedTask;
    }
}
