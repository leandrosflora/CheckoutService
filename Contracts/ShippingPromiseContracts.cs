namespace CheckoutService.Contracts;

public sealed record ShippingPromiseRequest(
    Guid BuyerId,
    Guid SellerId,
    AddressDto Destination,
    IReadOnlyList<ShippingPromiseItemDto> Items);

public sealed record ShippingPromiseItemDto(
    Guid SkuId,
    int Quantity,
    decimal UnitPrice);

public sealed record ShippingPromiseResponse(
    bool Available,
    string PromiseId,
    string Mode,
    string Carrier,
    DateOnly EstimatedDeliveryDate,
    decimal Cost,
    string? UnavailableReason);
