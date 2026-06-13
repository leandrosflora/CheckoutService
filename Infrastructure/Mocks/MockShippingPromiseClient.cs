using CheckoutService.Application;
using CheckoutService.Contracts;

namespace CheckoutService.Infrastructure.Mocks;

public sealed class MockShippingPromiseClient : IShippingPromiseClient
{
    public Task<ShippingPromiseResponse> GetPromiseAsync(
        ShippingPromiseRequest request,
        CancellationToken cancellationToken)
    {
        var itemsTotal = request.Items.Sum(x => x.Quantity * x.UnitPrice);
        var cost = itemsTotal >= 200 ? 0 : 19.90m;
        var zipCodeSuffix = request.Destination.ZipCode.Length >= 3
            ? request.Destination.ZipCode[^3..]
            : request.Destination.ZipCode.PadLeft(3, '0');

        var response = new ShippingPromiseResponse(
            Available: true,
            PromiseId: $"mock-promise-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}-{zipCodeSuffix}",
            Mode: cost == 0 ? "Standard Free" : "Standard",
            Carrier: "Mock Logistics",
            EstimatedDeliveryDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5)),
            Cost: cost,
            UnavailableReason: null);

        return Task.FromResult(response);
    }
}
