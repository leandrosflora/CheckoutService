namespace CheckoutService.Application;

public interface IEventPublisher
{
    Task AddToOutboxAsync(
        string eventType,
        object payload,
        CancellationToken cancellationToken);
}
