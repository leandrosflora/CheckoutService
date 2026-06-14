using CheckoutService.Contracts;
using CheckoutService.Application;
using Microsoft.Extensions.Options;

namespace CheckoutService.Infrastructure.Messaging;

public sealed class KafkaEventPublisher : IEventPublisher
{
    private readonly IKafkaProducer _producer;
    private readonly KafkaOptions _options;
    private readonly ILogger<KafkaEventPublisher> _logger;

    public KafkaEventPublisher(IKafkaProducer producer, IOptions<KafkaOptions> options, ILogger<KafkaEventPublisher> logger)
    {
        _producer = producer;
        _options = options.Value;
        _logger = logger;
    }

    public async Task AddToOutboxAsync(string eventType, object payload, CancellationToken cancellationToken)
    {
        if (payload is not KafkaEventEnvelope<ShippingQuoteRequestedPayload> envelope || eventType != "checkout.shipping.quote.requested")
        {
            _logger.LogDebug("Ignoring non-Kafka event type {EventType} in direct Kafka publisher", eventType);
            return;
        }

        try
        {
            await _producer.ProduceAsync(_options.Topics.ShippingQuoteRequested, envelope.Payload.CheckoutId.ToString(), envelope, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Kafka publish failed topic={Topic} key={MessageKey} eventType={EventType} correlationId={CorrelationId}", _options.Topics.ShippingQuoteRequested, envelope.Payload.CheckoutId, envelope.EventType, envelope.CorrelationId);
        }
    }
}
