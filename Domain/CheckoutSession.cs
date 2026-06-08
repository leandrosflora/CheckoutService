namespace CheckoutService.Domain;

public sealed class CheckoutSession
{
    public Guid Id { get; private set; }
    public Guid BuyerId { get; private set; }
    public Guid SellerId { get; private set; }

    public CheckoutStatus Status { get; private set; }

    public decimal ItemsTotal { get; private set; }
    public decimal ShippingCost { get; private set; }
    public decimal TotalAmount { get; private set; }

    public string ShippingPromiseId { get; private set; } = default!;
    public string ShippingMode { get; private set; } = default!;
    public string Carrier { get; private set; } = default!;
    public DateOnly EstimatedDeliveryDate { get; private set; }

    public string IdempotencyKey { get; private set; } = default!;
    public string? ConfirmationIdempotencyKey { get; private set; }
    public string? PaymentIntentId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? ConfirmedAt { get; private set; }

    public List<CheckoutItem> Items { get; private set; } = [];

    private CheckoutSession()
    {
    }

    public static CheckoutSession Create(
        Guid buyerId,
        Guid sellerId,
        IEnumerable<CheckoutItem> items,
        string shippingPromiseId,
        string shippingMode,
        string carrier,
        DateOnly estimatedDeliveryDate,
        decimal shippingCost,
        string idempotencyKey)
    {
        var itemList = items.ToList();

        if (buyerId == Guid.Empty)
        {
            throw new ArgumentException("Invalid buyer", nameof(buyerId));
        }

        if (sellerId == Guid.Empty)
        {
            throw new ArgumentException("Invalid seller", nameof(sellerId));
        }

        if (itemList.Count == 0)
        {
            throw new ArgumentException("Checkout must have at least one item", nameof(items));
        }

        if (string.IsNullOrWhiteSpace(shippingPromiseId))
        {
            throw new ArgumentException("Shipping promise is required", nameof(shippingPromiseId));
        }

        if (shippingCost < 0)
        {
            throw new ArgumentException("Shipping cost cannot be negative", nameof(shippingCost));
        }

        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            throw new ArgumentException("Idempotency key is required", nameof(idempotencyKey));
        }

        var now = DateTimeOffset.UtcNow;
        var itemsTotal = itemList.Sum(x => x.Total);

        return new CheckoutSession
        {
            Id = Guid.NewGuid(),
            BuyerId = buyerId,
            SellerId = sellerId,
            Status = CheckoutStatus.Created,
            Items = itemList,
            ItemsTotal = itemsTotal,
            ShippingCost = shippingCost,
            TotalAmount = itemsTotal + shippingCost,
            ShippingPromiseId = shippingPromiseId,
            ShippingMode = shippingMode,
            Carrier = carrier,
            EstimatedDeliveryDate = estimatedDeliveryDate,
            IdempotencyKey = idempotencyKey,
            CreatedAt = now,
            ExpiresAt = now.AddMinutes(15)
        };
    }

    public void Confirm(string paymentIntentId, string idempotencyKey)
    {
        if (Status != CheckoutStatus.Created)
        {
            throw new InvalidOperationException("Checkout cannot be confirmed");
        }

        if (ExpiresAt < DateTimeOffset.UtcNow)
        {
            Status = CheckoutStatus.Expired;
            throw new InvalidOperationException("Checkout expired");
        }

        if (string.IsNullOrWhiteSpace(paymentIntentId))
        {
            throw new ArgumentException("PaymentIntentId is required", nameof(paymentIntentId));
        }

        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            throw new ArgumentException("Idempotency key is required", nameof(idempotencyKey));
        }

        Status = CheckoutStatus.Confirmed;
        PaymentIntentId = paymentIntentId;
        ConfirmationIdempotencyKey = idempotencyKey;
        ConfirmedAt = DateTimeOffset.UtcNow;
    }
}
