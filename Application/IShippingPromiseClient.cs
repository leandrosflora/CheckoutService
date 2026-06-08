using CheckoutService.Contracts;

namespace CheckoutService.Application;

public interface IShippingPromiseClient
{
    Task<ShippingPromiseResponse> GetPromiseAsync(
        ShippingPromiseRequest request,
        CancellationToken cancellationToken);
}
