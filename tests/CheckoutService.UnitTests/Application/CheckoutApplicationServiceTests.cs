using CheckoutService.Application;
using CheckoutService.Contracts;
using CheckoutService.Domain;

namespace CheckoutService.UnitTests.Application;

public sealed class CheckoutApplicationServiceTests
{
    [Fact]
    public async Task CreateCheckoutAsync_WhenRequestIsValid_CreatesCheckoutAndEnqueuesContractedKafkaEnvelope()
    {
        var repository = new FakeCheckoutRepository();
        var shippingClient = new FakeShippingPromiseClient
        {
            Response = new ShippingPromiseResponse(
                true,
                "promise-123",
                "standard",
                "meli-logistics",
                DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2)),
                15m,
                null)
        };
        var eventPublisher = new FakeEventPublisher();
        var service = new CheckoutApplicationService(repository, shippingClient, eventPublisher);
        var request = ValidRequest();

        var response = await service.CreateCheckoutAsync(request, "idem-create", "corr-123", CancellationToken.None);

        Assert.Equal("Created", response.Status);
        Assert.Equal(40m, response.ItemsTotal);
        Assert.Equal(15m, response.ShippingCost);
        Assert.Equal(55m, response.TotalAmount);
        Assert.Equal("promise-123", response.ShippingOption.PromiseId);
        Assert.Single(repository.Added);
        Assert.Equal(1, repository.SaveChangesCalls);
        Assert.NotNull(shippingClient.LastRequest);
        Assert.Equal(request.BuyerId, shippingClient.LastRequest.BuyerId);
        Assert.Equal(request.SellerId, shippingClient.LastRequest.SellerId);
        Assert.Equal(request.ShippingAddress, shippingClient.LastRequest.Destination);
        var published = Assert.Single(eventPublisher.Published);
        Assert.Equal("checkout.shipping.quote.requested", published.EventType);
        var envelope = Assert.IsType<KafkaEventEnvelope<ShippingQuoteRequestedPayload>>(published.Payload);
        Assert.Equal("checkout.shipping.quote.requested", envelope.EventType);
        Assert.Equal("1.0", envelope.SchemaVersion);
        Assert.Equal("corr-123", envelope.CorrelationId);
        Assert.Equal("checkout-service", envelope.Producer);
        Assert.Equal(response.CheckoutId, envelope.Payload.CheckoutId);
        Assert.Equal(request.ShippingAddress, envelope.Payload.Destination);
        Assert.All(envelope.Payload.Items, item => Assert.Equal(request.SellerId, item.SellerId));
    }

    [Fact]
    public async Task CreateCheckoutAsync_WhenIdempotencyKeyAlreadyExists_ReturnsExistingCheckoutWithoutDownstreamCalls()
    {
        var existing = ValidCheckout("idem-create");
        var repository = new FakeCheckoutRepository { ExistingByCreateKey = existing };
        var shippingClient = new FakeShippingPromiseClient();
        var eventPublisher = new FakeEventPublisher();
        var service = new CheckoutApplicationService(repository, shippingClient, eventPublisher);

        var response = await service.CreateCheckoutAsync(ValidRequest(), "idem-create", "corr-123", CancellationToken.None);

        Assert.Equal(existing.Id, response.CheckoutId);
        Assert.Null(shippingClient.LastRequest);
        Assert.Empty(repository.Added);
        Assert.Empty(eventPublisher.Published);
        Assert.Equal(0, repository.SaveChangesCalls);
    }

    [Fact]
    public async Task CreateCheckoutAsync_WhenZipCodeIsBlank_ThrowsBeforeCallingShippingPromise()
    {
        var repository = new FakeCheckoutRepository();
        var shippingClient = new FakeShippingPromiseClient();
        var service = new CheckoutApplicationService(repository, shippingClient, new FakeEventPublisher());
        var request = ValidRequest() with { ShippingAddress = new AddressDto(" ", "Sao Paulo", "SP", "BR") };

        var exception = await Assert.ThrowsAsync<ArgumentException>(() => service.CreateCheckoutAsync(request, "idem", "corr", CancellationToken.None));

        Assert.Equal("request", exception.ParamName);
        Assert.Null(shippingClient.LastRequest);
    }

    [Fact]
    public async Task CreateCheckoutAsync_WhenShippingIsUnavailable_ThrowsAndDoesNotPersist()
    {
        var repository = new FakeCheckoutRepository();
        var shippingClient = new FakeShippingPromiseClient
        {
            Response = new ShippingPromiseResponse(false, "", "", "", default, 0m, "zip not covered")
        };
        var service = new CheckoutApplicationService(repository, shippingClient, new FakeEventPublisher());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateCheckoutAsync(ValidRequest(), "idem", "corr", CancellationToken.None));

        Assert.Equal("Shipping unavailable: zip not covered", exception.Message);
        Assert.Empty(repository.Added);
        Assert.Equal(0, repository.SaveChangesCalls);
    }

    [Fact]
    public async Task GetCheckoutAsync_WhenCheckoutExists_ReturnsMappedResponse()
    {
        var checkout = ValidCheckout("idem-create");
        var repository = new FakeCheckoutRepository { CheckoutById = checkout };
        var service = new CheckoutApplicationService(repository, new FakeShippingPromiseClient(), new FakeEventPublisher());

        var response = await service.GetCheckoutAsync(checkout.Id, CancellationToken.None);

        Assert.NotNull(response);
        Assert.Equal(checkout.Id, response.CheckoutId);
        Assert.Equal("Created", response.Status);
        Assert.Equal(checkout.TotalAmount, response.TotalAmount);
    }

    [Fact]
    public async Task ConfirmCheckoutAsync_WhenValid_ConfirmsCheckoutAndEnqueuesEvent()
    {
        var checkout = ValidCheckout("idem-create");
        var repository = new FakeCheckoutRepository { CheckoutById = checkout };
        var eventPublisher = new FakeEventPublisher();
        var service = new CheckoutApplicationService(repository, new FakeShippingPromiseClient(), eventPublisher);

        var response = await service.ConfirmCheckoutAsync(checkout.Id, new ConfirmCheckoutRequest("payment-1"), "idem-confirm", CancellationToken.None);

        Assert.Equal("Confirmed", response.Status);
        Assert.Equal("payment-1", checkout.PaymentIntentId);
        Assert.Equal("idem-confirm", checkout.ConfirmationIdempotencyKey);
        Assert.Equal(1, repository.SaveChangesCalls);
        var published = Assert.Single(eventPublisher.Published);
        Assert.Equal("CheckoutConfirmed", published.EventType);
    }

    [Fact]
    public async Task ConfirmCheckoutAsync_WhenConfirmationIdempotencyKeyAlreadyExists_ReturnsExistingCheckoutWithoutPublishing()
    {
        var existing = ValidCheckout("idem-create");
        existing.Confirm("payment-1", "idem-confirm");
        var repository = new FakeCheckoutRepository { ExistingByConfirmKey = existing };
        var eventPublisher = new FakeEventPublisher();
        var service = new CheckoutApplicationService(repository, new FakeShippingPromiseClient(), eventPublisher);

        var response = await service.ConfirmCheckoutAsync(Guid.NewGuid(), new ConfirmCheckoutRequest("payment-2"), "idem-confirm", CancellationToken.None);

        Assert.Equal(existing.Id, response.CheckoutId);
        Assert.Equal("Confirmed", response.Status);
        Assert.Empty(eventPublisher.Published);
        Assert.Equal(0, repository.SaveChangesCalls);
    }

    [Fact]
    public async Task ConfirmCheckoutAsync_WhenCheckoutDoesNotExist_ThrowsInvalidOperationException()
    {
        var service = new CheckoutApplicationService(new FakeCheckoutRepository(), new FakeShippingPromiseClient(), new FakeEventPublisher());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.ConfirmCheckoutAsync(Guid.NewGuid(), new ConfirmCheckoutRequest("payment-1"), "idem-confirm", CancellationToken.None));

        Assert.Equal("Checkout not found", exception.Message);
    }

    private static CreateCheckoutRequest ValidRequest() => new(
        Guid.NewGuid(),
        Guid.NewGuid(),
        new AddressDto("01001-000", "Sao Paulo", "SP", "BR"),
        new[] { new CheckoutItemDto(Guid.NewGuid(), 2, 20m) });

    private static CheckoutSession ValidCheckout(string idempotencyKey) => CheckoutSession.Create(
        Guid.NewGuid(),
        Guid.NewGuid(),
        new[] { new CheckoutItem(Guid.NewGuid(), 2, 20m) },
        "promise-123",
        "standard",
        "meli-logistics",
        DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2)),
        15m,
        idempotencyKey);

    private sealed class FakeCheckoutRepository : ICheckoutRepository
    {
        public CheckoutSession? CheckoutById { get; init; }
        public CheckoutSession? ExistingByCreateKey { get; init; }
        public CheckoutSession? ExistingByConfirmKey { get; init; }
        public List<CheckoutSession> Added { get; } = [];
        public int SaveChangesCalls { get; private set; }

        public Task<CheckoutSession?> GetByIdAsync(Guid checkoutId, CancellationToken cancellationToken) =>
            Task.FromResult(CheckoutById is not null && CheckoutById.Id == checkoutId ? CheckoutById : null);

        public Task<CheckoutSession?> FindByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken) =>
            Task.FromResult(ExistingByCreateKey is not null && ExistingByCreateKey.IdempotencyKey == idempotencyKey ? ExistingByCreateKey : null);

        public Task<CheckoutSession?> FindConfirmedByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken) =>
            Task.FromResult(ExistingByConfirmKey is not null && ExistingByConfirmKey.ConfirmationIdempotencyKey == idempotencyKey ? ExistingByConfirmKey : null);

        public Task AddAsync(CheckoutSession checkout, CancellationToken cancellationToken)
        {
            Added.Add(checkout);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            SaveChangesCalls++;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeShippingPromiseClient : IShippingPromiseClient
    {
        public ShippingPromiseRequest? LastRequest { get; private set; }
        public ShippingPromiseResponse Response { get; init; } = new(
            true,
            "promise-default",
            "standard",
            "carrier",
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
            0m,
            null);

        public Task<ShippingPromiseResponse> GetPromiseAsync(ShippingPromiseRequest request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(Response);
        }
    }

    private sealed class FakeEventPublisher : IEventPublisher
    {
        public List<(string EventType, object Payload)> Published { get; } = [];

        public Task AddToOutboxAsync(string eventType, object payload, CancellationToken cancellationToken)
        {
            Published.Add((eventType, payload));
            return Task.CompletedTask;
        }
    }
}
