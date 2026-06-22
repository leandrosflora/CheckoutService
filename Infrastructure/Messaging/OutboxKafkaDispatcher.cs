using CheckoutService.Contracts;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

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
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CheckoutDbContext>();
        var producer = scope.ServiceProvider.GetRequiredService<IKafkaProducer>();

        var messages = await db.OutboxMessages
            .Where(x => x.ProcessedAt == null && (x.EventType == "checkout.shipping.quote.requested" || x.EventType == "checkout.confirmed"))
            .OrderBy(x => x.CreatedAt)
            .Take(20)
            .ToListAsync(cancellationToken);

        foreach (var message in messages)
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

            message.MarkAsProcessed();
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
