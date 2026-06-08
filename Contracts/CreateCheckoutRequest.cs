namespace CheckoutService.Contracts;

public sealed record CreateCheckoutRequest(
    Guid BuyerId,
    Guid SellerId,
    AddressDto ShippingAddress,
    IReadOnlyList<CheckoutItemDto> Items);

public sealed record CheckoutItemDto(
    Guid SkuId,
    int Quantity,
    decimal UnitPrice);

public sealed record AddressDto(
    string ZipCode,
    string City,
    string State,
    string Country);

public sealed record ConfirmCheckoutRequest(
    string PaymentIntentId);
