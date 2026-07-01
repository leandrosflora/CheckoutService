using CheckoutService.Application.Ports;
using CheckoutService.Contracts;
using CheckoutService.Domain;

namespace CheckoutService.Application;

public sealed class CheckoutApplicationService
{
    private readonly ICheckoutRepository _repository;
    private readonly IShippingPromiseClient _shippingPromiseClient;
    private readonly IEventPublisher _eventPublisher;
    private readonly IShippingPromiseProjectionRepository _shippingPromiseProjectionRepository;

    public CheckoutApplicationService(
        ICheckoutRepository repository,
        IShippingPromiseClient shippingPromiseClient,
        IEventPublisher eventPublisher,
        IShippingPromiseProjectionRepository shippingPromiseProjectionRepository)
    {
        _repository = repository;
        _shippingPromiseClient = shippingPromiseClient;
        _eventPublisher = eventPublisher;
        _shippingPromiseProjectionRepository = shippingPromiseProjectionRepository;
    }

    public async Task<CheckoutResponse> CreateCheckoutAsync(
        CreateCheckoutRequest request,
        string idempotencyKey,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var existing = await _repository.FindByIdempotencyKeyAsync(
            idempotencyKey,
            cancellationToken);

        if (existing is not null)
        {
            return MapToResponse(existing);
        }

        ValidateRequest(request);

        var shippingPromise = await _shippingPromiseClient.GetPromiseAsync(
            new ShippingPromiseRequest(
                request.BuyerId,
                request.SellerId,
                request.ShippingAddress,
                request.Items.Select(x => new ShippingPromiseItemDto(
                    x.SkuId,
                    x.Quantity,
                    x.UnitPrice)).ToList()),
            cancellationToken);

        if (!shippingPromise.Available)
        {
            throw new InvalidOperationException(
                $"Shipping unavailable: {shippingPromise.UnavailableReason}");
        }

        var items = request.Items
            .Select(x => new CheckoutItem(x.SkuId, x.Quantity, x.UnitPrice))
            .ToList();

        var checkout = CheckoutSession.Create(
            request.BuyerId,
            request.SellerId,
            items,
            shippingPromise.PromiseId,
            shippingPromise.Mode,
            shippingPromise.Carrier,
            shippingPromise.EstimatedDeliveryDate,
            shippingPromise.Cost,
            idempotencyKey);

        await _repository.AddAsync(checkout, cancellationToken);

        var quoteRequested = new KafkaEventEnvelope<ShippingQuoteRequestedPayload>(
            Guid.NewGuid(),
            "checkout.shipping.quote.requested",
            "1.0",
            DateTimeOffset.UtcNow,
            correlationId,
            "checkout-service",
            new ShippingQuoteRequestedPayload(
                checkout.Id,
                checkout.BuyerId,
                checkout.SellerId,
                request.ShippingAddress,
                request.Items.Select(x => new ShippingQuoteRequestedItemPayload(
                    x.SkuId,
                    checkout.SellerId,
                    x.Quantity,
                    x.UnitPrice)).ToList()));

        await _eventPublisher.AddToOutboxAsync(
            "checkout.shipping.quote.requested",
            quoteRequested,
            cancellationToken);

        await _repository.SaveChangesAsync(cancellationToken);

        return MapToResponse(checkout);
    }

    public async Task<CheckoutResponse?> GetCheckoutAsync(
        Guid checkoutId,
        CancellationToken cancellationToken)
    {
        var checkout = await _repository.GetByIdAsync(checkoutId, cancellationToken);

        return checkout is null
            ? null
            : MapToResponse(checkout);
    }


    public async Task<ShippingOptionsResponse?> GetShippingOptionsAsync(
        Guid checkoutId,
        CancellationToken cancellationToken)
    {
        var checkout = await _repository.GetByIdAsync(checkoutId, cancellationToken);

        if (checkout is null)
        {
            return null;
        }

        var projection = await _shippingPromiseProjectionRepository.GetByCheckoutIdAsync(
            checkoutId,
            cancellationToken);

        if (projection is null)
        {
            return new ShippingOptionsResponse("Pending", []);
        }

        return new ShippingOptionsResponse(
            "Calculated",
            [new ShippingOptionDto(
                projection.PromiseId,
                projection.Mode,
                projection.Carrier,
                projection.EstimatedDeliveryDate,
                projection.Cost)]);
    }

    public async Task<CheckoutResponse> ConfirmCheckoutAsync(
        Guid checkoutId,
        ConfirmCheckoutRequest request,
        string idempotencyKey,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var existing = await _repository.FindConfirmedByIdempotencyKeyAsync(
            idempotencyKey,
            cancellationToken);

        if (existing is not null)
        {
            return MapToResponse(existing);
        }

        var checkout = await _repository.GetByIdAsync(checkoutId, cancellationToken);

        if (checkout is null)
        {
            throw new InvalidOperationException("Checkout not found");
        }

        checkout.Confirm(request.PaymentIntentId, idempotencyKey);

        await _repository.UpdateAsync(checkout, cancellationToken);

        var confirmedEnvelope = new KafkaEventEnvelope<CheckoutConfirmedPayload>(
            Guid.NewGuid(),
            "checkout.confirmed",
            "1.0",
            DateTimeOffset.UtcNow,
            correlationId,
            "checkout-service",
            new CheckoutConfirmedPayload(
                checkout.Id,
                checkout.BuyerId,
                checkout.SellerId,
                "BRL",
                checkout.ShippingCost,
                checkout.ShippingPromiseId,
                request.PaymentIntentId,
                checkout.Items
                    .Select(i => new CheckoutConfirmedItemPayload(i.SkuId, i.Quantity, i.UnitPrice))
                    .ToList()));

        await _eventPublisher.AddToOutboxAsync(
            "checkout.confirmed",
            confirmedEnvelope,
            cancellationToken);

        await _repository.SaveChangesAsync(cancellationToken);

        return MapToResponse(checkout);
    }

    private static void ValidateRequest(CreateCheckoutRequest request)
    {
        if (request.BuyerId == Guid.Empty)
        {
            throw new ArgumentException("BuyerId is required", nameof(request));
        }

        if (request.SellerId == Guid.Empty)
        {
            throw new ArgumentException("SellerId is required", nameof(request));
        }

        if (request.Items.Count == 0)
        {
            throw new ArgumentException("Checkout must have items", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.ShippingAddress.ZipCode))
        {
            throw new ArgumentException("ZipCode is required", nameof(request));
        }
    }

    private static CheckoutResponse MapToResponse(CheckoutSession checkout)
    {
        return new CheckoutResponse(
            checkout.Id,
            checkout.Status.ToString(),
            checkout.ItemsTotal,
            checkout.ShippingCost,
            checkout.TotalAmount,
            new ShippingOptionDto(
                checkout.ShippingPromiseId,
                checkout.ShippingMode,
                checkout.Carrier,
                checkout.EstimatedDeliveryDate,
                checkout.ShippingCost),
            checkout.ExpiresAt);
    }
}
