namespace CheckoutService.Infrastructure.Messaging;

public sealed class KafkaOptions
{
    public const string SectionName = "Kafka";

    public string BootstrapServers { get; init; } = "localhost:9092";
    public string ConsumerGroupId { get; init; } = "checkout-service";
    public KafkaTopicsOptions Topics { get; init; } = new();

    public bool IsConfigured => !string.IsNullOrWhiteSpace(BootstrapServers);
}

public sealed class KafkaTopicsOptions
{
    public string ShippingQuoteRequested { get; init; } = "checkout.shipping.quote.requested";
    public string ShippingPromiseCalculated { get; init; } = "shipping.promise.calculated";
    public string CheckoutConfirmed { get; init; } = "checkout.confirmed";
}
