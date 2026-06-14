namespace CheckoutService.Domain;

public sealed class ShippingPromiseProjection
{
    public Guid Id { get; private set; }
    public Guid EventId { get; private set; }
    public string CorrelationId { get; private set; } = default!;
    public Guid CheckoutId { get; private set; }
    public string PromiseId { get; private set; } = default!;
    public string Mode { get; private set; } = default!;
    public string Carrier { get; private set; } = default!;
    public DateOnly EstimatedDeliveryDate { get; private set; }
    public decimal Cost { get; private set; }
    public DateTimeOffset ProcessedAt { get; private set; }

    private ShippingPromiseProjection()
    {
    }

    public ShippingPromiseProjection(
        Guid eventId,
        string correlationId,
        Guid checkoutId,
        string promiseId,
        string mode,
        string carrier,
        DateOnly estimatedDeliveryDate,
        decimal cost)
    {
        if (eventId == Guid.Empty) throw new ArgumentException("EventId is required", nameof(eventId));
        if (checkoutId == Guid.Empty) throw new ArgumentException("CheckoutId is required", nameof(checkoutId));
        if (string.IsNullOrWhiteSpace(correlationId)) throw new ArgumentException("CorrelationId is required", nameof(correlationId));

        Id = Guid.NewGuid();
        EventId = eventId;
        CorrelationId = correlationId;
        CheckoutId = checkoutId;
        PromiseId = promiseId;
        Mode = mode;
        Carrier = carrier;
        EstimatedDeliveryDate = estimatedDeliveryDate;
        Cost = cost;
        ProcessedAt = DateTimeOffset.UtcNow;
    }
}
