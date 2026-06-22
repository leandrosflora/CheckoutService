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
    [property: JsonPropertyName("destination")] AddressDto Destination,
    [property: JsonPropertyName("items")] IReadOnlyList<ShippingQuoteRequestedItemPayload> Items);

public sealed record ShippingQuoteRequestedItemPayload(
    [property: JsonPropertyName("skuId")] Guid SkuId,
    [property: JsonPropertyName("sellerId")] Guid SellerId,
    [property: JsonPropertyName("quantity")] int Quantity,
    [property: JsonPropertyName("unitPrice")] decimal UnitPrice);

public sealed record ShippingPromiseCalculatedPayload(
    [property: JsonPropertyName("checkoutId")] Guid CheckoutId,
    [property: JsonPropertyName("buyerId")] Guid BuyerId,
    [property: JsonPropertyName("sellerId")] Guid SellerId,
    [property: JsonPropertyName("promiseId")] string PromiseId,
    [property: JsonPropertyName("mode")] string Mode,
    [property: JsonPropertyName("carrier")] string Carrier,
    [property: JsonPropertyName("estimatedDeliveryDate")] DateOnly EstimatedDeliveryDate,
    [property: JsonPropertyName("cost")] decimal Cost,
    [property: JsonPropertyName("currency")] string Currency,
    [property: JsonPropertyName("source")] string Source);

public sealed record CheckoutConfirmedPayload(
    [property: JsonPropertyName("checkoutId")] Guid CheckoutId,
    [property: JsonPropertyName("buyerId")] Guid BuyerId,
    [property: JsonPropertyName("sellerId")] Guid SellerId,
    [property: JsonPropertyName("currency")] string Currency,
    [property: JsonPropertyName("shippingPrice")] decimal ShippingPrice,
    [property: JsonPropertyName("shippingPromiseId")] string ShippingPromiseId,
    [property: JsonPropertyName("paymentMethodToken")] string PaymentMethodToken,
    [property: JsonPropertyName("items")] IReadOnlyList<CheckoutConfirmedItemPayload> Items);

public sealed record CheckoutConfirmedItemPayload(
    [property: JsonPropertyName("skuId")] Guid SkuId,
    [property: JsonPropertyName("quantity")] int Quantity,
    [property: JsonPropertyName("unitPrice")] decimal UnitPrice);
