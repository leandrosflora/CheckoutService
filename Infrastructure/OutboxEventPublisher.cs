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

        var envelope = JsonDocument.Parse(json).RootElement;
        var eventId = envelope.GetProperty("eventId").GetGuid();
        var schemaVersion = envelope.GetProperty("schemaVersion").GetString()!;
        var producer = envelope.GetProperty("producer").GetString()!;
        var correlationId = Guid.TryParse(envelope.GetProperty("correlationId").GetString(), out var parsedCorrelationId)
            ? parsedCorrelationId
            : Guid.NewGuid();

        await _databaseContext.EnsureTransactionAsync(cancellationToken);

        const string sql = @"
            insert into checkout.outbox_messages (
                message_id,
                event_id,
                topic,
                event_type,
                schema_version,
                correlation_id,
                producer,
                payload,
                created_at
            ) values (
                @Id,
                @EventId,
                @Topic,
                @EventType,
                @SchemaVersion,
                @CorrelationId,
                @Producer,
                cast(@Payload as jsonb),
                @CreatedAt)";

        await _databaseContext.Connection.ExecuteAsync(
            sql,
            new
            {
                message.Id,
                EventId = eventId,
                Topic = eventType,
                message.EventType,
                SchemaVersion = schemaVersion,
                CorrelationId = correlationId,
                Producer = producer,
                message.Payload,
                message.CreatedAt
            },
            _databaseContext.Transaction);
    }
}
