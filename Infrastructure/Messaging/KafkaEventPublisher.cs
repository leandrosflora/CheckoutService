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
        try
        {
            if (payload is KafkaEventEnvelope<ShippingQuoteRequestedPayload> quoteEnvelope && eventType == "checkout.shipping.quote.requested")
            {
                await _producer.ProduceAsync(_options.Topics.ShippingQuoteRequested, quoteEnvelope.Payload.CheckoutId.ToString(), quoteEnvelope, cancellationToken);
                return;
            }

            if (payload is KafkaEventEnvelope<CheckoutConfirmedPayload> confirmedEnvelope && eventType == "checkout.confirmed")
            {
                await _producer.ProduceAsync(_options.Topics.CheckoutConfirmed, confirmedEnvelope.Payload.CheckoutId.ToString(), confirmedEnvelope, cancellationToken);
                return;
            }

            _logger.LogDebug("Ignoring non-Kafka event type {EventType} in direct Kafka publisher", eventType);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Kafka publish failed for eventType={EventType}", eventType);
        }
    }
}
