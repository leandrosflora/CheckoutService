using System.Collections.Concurrent;
using CheckoutService.Application;
using CheckoutService.Domain;

namespace CheckoutService.Infrastructure.Mocks;

public sealed class MockCheckoutRepository : ICheckoutRepository
{
    //private static readonly Guid CreatedCheckoutId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid ConfirmedCheckoutId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid CreatedCheckoutId = Guid.Parse("00000000-0000-0000-0000-000000000000"); 

    private static readonly ConcurrentDictionary<Guid, CheckoutSession> CheckoutsById = new();
    private static readonly ConcurrentDictionary<string, Guid> CheckoutsByIdempotencyKey = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, Guid> ConfirmedCheckoutsByIdempotencyKey = new(StringComparer.OrdinalIgnoreCase);

    static MockCheckoutRepository()
    {
        SeedFakeCheckouts();
    }

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

    private static void SeedFakeCheckouts()
    {
        var created = CreateFakeCheckout(
            CreatedCheckoutId,
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
            [
                new CheckoutItem(Guid.Parse("10000000-0000-0000-0000-000000000001"), 1, 2499.90m),
                new CheckoutItem(Guid.Parse("10000000-0000-0000-0000-000000000002"), 2, 159.99m)
            ],
            "fake-promise-standard-sp",
            "standard",
            "Meli Logistics",
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3)),
            29.90m,
            "fake-create-created");

        var confirmed = CreateFakeCheckout(
            ConfirmedCheckoutId,
            Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
            Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"),
            [
                new CheckoutItem(Guid.Parse("20000000-0000-0000-0000-000000000001"), 1, 899.00m),
                new CheckoutItem(Guid.Parse("20000000-0000-0000-0000-000000000002"), 3, 79.90m)
            ],
            "fake-promise-express-rj",
            "express",
            "Mercado Envios",
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
            19.90m,
            "fake-create-confirmed");

        confirmed.Confirm("fake-payment-intent-001", "fake-confirm-confirmed");

        AddSeed(created);
        AddSeed(confirmed);
    }

    private static CheckoutSession CreateFakeCheckout(
        Guid checkoutId,
        Guid buyerId,
        Guid sellerId,
        IEnumerable<CheckoutItem> items,
        string shippingPromiseId,
        string shippingMode,
        string carrier,
        DateOnly estimatedDeliveryDate,
        decimal shippingCost,
        string idempotencyKey)
    {
        var checkout = CheckoutSession.Create(
            buyerId,
            sellerId,
            items,
            shippingPromiseId,
            shippingMode,
            carrier,
            estimatedDeliveryDate,
            shippingCost,
            idempotencyKey);

        typeof(CheckoutSession)
            .GetProperty(nameof(CheckoutSession.Id))!
            .SetValue(checkout, checkoutId);

        return checkout;
    }

    private static void AddSeed(CheckoutSession checkout)
    {
        CheckoutsById[checkout.Id] = checkout;
        CheckoutsByIdempotencyKey[checkout.IdempotencyKey] = checkout.Id;

        if (checkout.ConfirmationIdempotencyKey is not null)
        {
            ConfirmedCheckoutsByIdempotencyKey[checkout.ConfirmationIdempotencyKey] = checkout.Id;
        }
    }
}
