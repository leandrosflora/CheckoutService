using System.Text.Json.Serialization;

namespace CheckoutService.Contracts;

public sealed record KafkaEventEnvelope<TPayload>(
    [property: JsonPropertyName("eventId")] Guid EventId,
    [property: JsonPropertyName("eventType")] string EventType,
    [property: JsonPropertyName("schemaVersion")] string SchemaVersion,
    [property: JsonPropertyName("occurredAt")] DateTimeOffset OccurredAt,
    [property: JsonPropertyName("correlationId")] string CorrelationId,
    [property: JsonPropertyName("producer")] string Producer,
    [property: JsonPropertyName("payload")] TPayload Payload);

public sealed record ShippingQuoteRequestedPayload(
    [property: JsonPropertyName("checkoutId")] Guid CheckoutId,
    [property: JsonPropertyName("buyerId")] Guid BuyerId,
    [property: JsonPropertyName("sellerId")] Guid SellerId,
    [property: JsonPropertyName("destination")] object Destination,
    [property: JsonPropertyName("items")] object Items);

public sealed record ShippingPromiseCalculatedPayload(
    [property: JsonPropertyName("checkoutId")] Guid CheckoutId,
    [property: JsonPropertyName("promiseId")] string PromiseId,
    [property: JsonPropertyName("mode")] string Mode,
    [property: JsonPropertyName("carrier")] string Carrier,
    [property: JsonPropertyName("estimatedDeliveryDate")] DateOnly EstimatedDeliveryDate,
    [property: JsonPropertyName("cost")] decimal Cost);
