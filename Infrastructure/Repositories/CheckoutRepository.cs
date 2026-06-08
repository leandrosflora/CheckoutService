using CheckoutService.Application;
using CheckoutService.Domain;
using Microsoft.EntityFrameworkCore;

namespace CheckoutService.Infrastructure.Repositories;

public sealed class CheckoutRepository : ICheckoutRepository
{
    private readonly CheckoutDbContext _dbContext;

    public CheckoutRepository(CheckoutDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<CheckoutSession?> GetByIdAsync(
        Guid checkoutId,
        CancellationToken cancellationToken)
    {
        return _dbContext.Checkouts
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.Id == checkoutId, cancellationToken);
    }

    public Task<CheckoutSession?> FindByIdempotencyKeyAsync(
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        return _dbContext.Checkouts
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.IdempotencyKey == idempotencyKey, cancellationToken);
    }

    public Task<CheckoutSession?> FindConfirmedByIdempotencyKeyAsync(
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        return _dbContext.Checkouts
            .Include(x => x.Items)
            .FirstOrDefaultAsync(
                x => x.ConfirmationIdempotencyKey == idempotencyKey
                     && x.Status == CheckoutStatus.Confirmed,
                cancellationToken);
    }

    public async Task AddAsync(
        CheckoutSession checkout,
        CancellationToken cancellationToken)
    {
        await _dbContext.Checkouts.AddAsync(checkout, cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        return _dbContext.SaveChangesAsync(cancellationToken);
    }
}
