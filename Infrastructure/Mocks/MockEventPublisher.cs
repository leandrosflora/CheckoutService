using CheckoutService.Application;

namespace CheckoutService.Infrastructure.Mocks;

public sealed class MockEventPublisher : IEventPublisher
{
    private readonly ILogger<MockEventPublisher> _logger;

    public MockEventPublisher(ILogger<MockEventPublisher> logger)
    {
        _logger = logger;
    }

    public Task AddToOutboxAsync(
        string eventType,
        object payload,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Mock outbox received event {EventType} with payload {@Payload}",
            eventType,
            payload);

        return Task.CompletedTask;
    }
}
