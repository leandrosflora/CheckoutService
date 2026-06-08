using System.Net.Http.Json;
using CheckoutService.Application;
using CheckoutService.Contracts;

namespace CheckoutService.Infrastructure;

public sealed class ShippingPromiseClient : IShippingPromiseClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ShippingPromiseClient> _logger;

    public ShippingPromiseClient(
        HttpClient httpClient,
        ILogger<ShippingPromiseClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<ShippingPromiseResponse> GetPromiseAsync(
        ShippingPromiseRequest request,
        CancellationToken cancellationToken)
    {
        using var response = await _httpClient.PostAsJsonAsync(
            "/shipping-promises",
            request,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "Shipping Promise Service returned {StatusCode}",
                response.StatusCode);

            throw new InvalidOperationException("Shipping promise unavailable");
        }

        var promise = await response.Content.ReadFromJsonAsync<ShippingPromiseResponse>(
            cancellationToken);

        return promise ?? throw new InvalidOperationException("Invalid shipping promise response");
    }
}
