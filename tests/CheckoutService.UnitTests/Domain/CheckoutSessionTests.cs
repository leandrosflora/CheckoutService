using CheckoutService.Domain;

namespace CheckoutService.UnitTests.Domain;

public sealed class CheckoutSessionTests
{
    [Fact]
    public void Create_WhenValuesAreValid_CreatesSessionWithCalculatedTotalsAndExpiration()
    {
        var before = DateTimeOffset.UtcNow;
        var buyerId = Guid.NewGuid();
        var sellerId = Guid.NewGuid();
        var deliveryDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2));
        var items = new[]
        {
            new CheckoutItem(Guid.NewGuid(), 2, 30m),
            new CheckoutItem(Guid.NewGuid(), 1, 15.50m)
        };

        var checkout = CheckoutSession.Create(
            buyerId,
            sellerId,
            items,
            "promise-123",
            "standard",
            "meli-logistics",
            deliveryDate,
            12.75m,
            "idem-create-1");

        var after = DateTimeOffset.UtcNow;
        Assert.NotEqual(Guid.Empty, checkout.Id);
        Assert.Equal(buyerId, checkout.BuyerId);
        Assert.Equal(sellerId, checkout.SellerId);
        Assert.Equal(CheckoutStatus.Created, checkout.Status);
        Assert.Equal(75.50m, checkout.ItemsTotal);
        Assert.Equal(12.75m, checkout.ShippingCost);
        Assert.Equal(88.25m, checkout.TotalAmount);
        Assert.Equal("promise-123", checkout.ShippingPromiseId);
        Assert.Equal("standard", checkout.ShippingMode);
        Assert.Equal("meli-logistics", checkout.Carrier);
        Assert.Equal(deliveryDate, checkout.EstimatedDeliveryDate);
        Assert.Equal("idem-create-1", checkout.IdempotencyKey);
        Assert.InRange(checkout.CreatedAt, before, after);
        Assert.InRange(checkout.ExpiresAt, before.AddMinutes(15), after.AddMinutes(15));
    }

    [Fact]
    public void Create_WhenShippingCostIsNegative_ThrowsArgumentException()
    {
        var exception = Assert.Throws<ArgumentException>(() => CheckoutSession.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new[] { new CheckoutItem(Guid.NewGuid(), 1, 10m) },
            "promise-123",
            "standard",
            "carrier",
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
            -0.01m,
            "idem"));

        Assert.Equal("shippingCost", exception.ParamName);
    }

    [Fact]
    public void Confirm_WhenSessionIsCreated_SetsConfirmedStatusAndPaymentData()
    {
        var checkout = ValidCheckout();

        checkout.Confirm("payment-intent-1", "idem-confirm-1");

        Assert.Equal(CheckoutStatus.Confirmed, checkout.Status);
        Assert.Equal("payment-intent-1", checkout.PaymentIntentId);
        Assert.Equal("idem-confirm-1", checkout.ConfirmationIdempotencyKey);
        Assert.NotNull(checkout.ConfirmedAt);
    }

    [Fact]
    public void Confirm_WhenAlreadyConfirmed_ThrowsInvalidOperationException()
    {
        var checkout = ValidCheckout();
        checkout.Confirm("payment-intent-1", "idem-confirm-1");

        var exception = Assert.Throws<InvalidOperationException>(() => checkout.Confirm("payment-intent-2", "idem-confirm-2"));

        Assert.Equal("Checkout cannot be confirmed", exception.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Confirm_WhenPaymentIntentIdIsBlank_ThrowsArgumentException(string paymentIntentId)
    {
        var checkout = ValidCheckout();

        var exception = Assert.Throws<ArgumentException>(() => checkout.Confirm(paymentIntentId, "idem-confirm"));

        Assert.Equal("paymentIntentId", exception.ParamName);
    }

    private static CheckoutSession ValidCheckout() => CheckoutSession.Create(
        Guid.NewGuid(),
        Guid.NewGuid(),
        new[] { new CheckoutItem(Guid.NewGuid(), 1, 10m) },
        "promise-123",
        "standard",
        "carrier",
        DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
        5m,
        "idem-create");
}
