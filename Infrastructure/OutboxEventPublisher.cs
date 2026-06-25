using System.Text.Json;
using CheckoutService.Application;
using CheckoutService.Infrastructure.Database;
using Dapper;

namespace CheckoutService.Infrastructure;

public sealed class OutboxEventPublisher : IEventPublisher
{
    private readonly IDatabaseContext _databaseContext;

    public OutboxEventPublisher(IDatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    public async Task AddToOutboxAsync(
        string eventType,
        object payload,
        CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(payload);
        var message = new OutboxMessage(eventType, json);

        await _databaseContext.EnsureTransactionAsync(cancellationToken);

        const string sql = @"
            insert into outbox_messages (
                message_id,
                event_type,
                payload,
                created_at,
                processed_at
            ) values (
                @Id,
                @EventType,
                @Payload,
                @CreatedAt,
                @ProcessedAt)";

        await _databaseContext.Connection.ExecuteAsync(
            sql,
            new
            {
                message.Id,
                message.EventType,
                message.Payload,
                message.CreatedAt,
                ProcessedAt = (DateTimeOffset?)null
            },
            _databaseContext.Transaction);
    }
}
