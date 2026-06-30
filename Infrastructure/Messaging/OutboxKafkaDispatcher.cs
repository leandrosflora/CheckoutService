using CheckoutService.Contracts;
using System.Text.Json;
using CheckoutService.Application.Ports;
using Confluent.Kafka;
using Dapper;
using Microsoft.Extensions.Options;
using CheckoutService.Infrastructure.Database;

namespace CheckoutService.Infrastructure.Messaging;

public sealed class OutboxKafkaDispatcher : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly KafkaOptions _options;
    private readonly ILogger<OutboxKafkaDispatcher> _logger;

    public OutboxKafkaDispatcher(IServiceScopeFactory scopeFactory, IOptions<KafkaOptions> options, ILogger<OutboxKafkaDispatcher> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await DispatchPendingAsync(stoppingToken); }
            catch (Exception ex) { _logger.LogWarning(ex, "Outbox Kafka dispatch cycle failed"); }
            await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
        }
    }

    private async Task DispatchPendingAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IDatabaseContext>();
        var producer = scope.ServiceProvider.GetRequiredService<IKafkaProducer>();

        const string selectSql = @"
            select
                message_id as Id,
                event_type as EventType,
                payload as Payload
            from outbox_messages
            where (event_type = 'checkout.shipping.quote.requested' or event_type = 'checkout.confirmed')
            order by created_at
            limit 20";

        await db.EnsureConnectionOpenAsync(cancellationToken);

        var messages = await db.Connection.QueryAsync<OutboxMessageRow>(selectSql);

        foreach (var message in messages)
        {
            try
            {
                if (message.EventType == "checkout.shipping.quote.requested")
                {
                    var envelope = JsonSerializer.Deserialize<KafkaEventEnvelope<ShippingQuoteRequestedPayload>>(message.Payload, JsonOptions);
                    if (envelope is null) continue;
                    await producer.ProduceAsync(_options.Topics.ShippingQuoteRequested, envelope.Payload.CheckoutId.ToString(), envelope, cancellationToken);
                }
                else if (message.EventType == "checkout.confirmed")
                {
                    var envelope = JsonSerializer.Deserialize<KafkaEventEnvelope<CheckoutConfirmedPayload>>(message.Payload, JsonOptions);
                    if (envelope is null) continue;
                    await producer.ProduceAsync(_options.Topics.CheckoutConfirmed, envelope.Payload.CheckoutId.ToString(), envelope, cancellationToken);
                }

                await db.EnsureTransactionAsync(cancellationToken);
                await db.Connection.ExecuteAsync(
                    "update outbox_messages set processed_at = @ProcessedAt where message_id = @Id",
                    new { ProcessedAt = DateTimeOffset.UtcNow, message.Id },
                    db.Transaction);
                await db.CommitAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to dispatch outbox message id={MessageId}", message.Id);
                await db.RollbackAsync();
            }
        }
    }

    private sealed class OutboxMessageRow
    {
        public Guid Id { get; set; }
        public string EventType { get; set; } = string.Empty;
        public string Payload { get; set; } = string.Empty;
    }
}
