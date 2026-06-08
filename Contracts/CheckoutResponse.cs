namespace CheckoutService.Contracts;

public sealed record CheckoutResponse(
    Guid CheckoutId,
    string Status,
    decimal ItemsTotal,
    decimal ShippingCost,
    decimal TotalAmount,
    ShippingOptionDto ShippingOption,
    DateTimeOffset ExpiresAt);

public sealed record ShippingOptionDto(
    string PromiseId,
    string Mode,
    string Carrier,
    DateOnly EstimatedDeliveryDate,
    decimal Cost);
