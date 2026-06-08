using System.Text.Json;
using CheckoutService.Application;

namespace CheckoutService.Infrastructure;

public sealed class OutboxEventPublisher : IEventPublisher
{
    private readonly CheckoutDbContext _dbContext;

    public OutboxEventPublisher(CheckoutDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddToOutboxAsync(
        string eventType,
        object payload,
        CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(payload);
        var message = new OutboxMessage(eventType, json);

        await _dbContext.OutboxMessages.AddAsync(message, cancellationToken);
    }
}
