using CheckoutService.Contracts;
using System.Text.Json;
using CheckoutService.Application.Ports;
using Confluent.Kafka;
using Microsoft.Extensions.Options;

namespace CheckoutService.Infrastructure.Messaging;

public sealed class ShippingPromiseCalculatedConsumer : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly KafkaOptions _options;
    private readonly ILogger<ShippingPromiseCalculatedConsumer> _logger;

    public ShippingPromiseCalculatedConsumer(IServiceScopeFactory scopeFactory, IOptions<KafkaOptions> options, ILogger<ShippingPromiseCalculatedConsumer> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();
        using var consumer = new ConsumerBuilder<string, string>(new ConsumerConfig
        {
            BootstrapServers = _options.BootstrapServers,
            GroupId = _options.ConsumerGroupId,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false
        }).Build();
        consumer.Subscribe(_options.Topics.ShippingPromiseCalculated);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = consumer.Consume(stoppingToken);
                var envelope = JsonSerializer.Deserialize<KafkaEventEnvelope<ShippingPromiseCalculatedPayload>>(result.Message.Value, JsonOptions);
                if (envelope is null || envelope.EventType != "shipping.promise.calculated")
                {
                    _logger.LogWarning("Ignoring invalid Kafka message topic={Topic} key={MessageKey}", result.Topic, result.Message.Key);
                    consumer.Commit(result);
                    continue;
                }

                if (envelope.Payload.CheckoutId == Guid.Empty)
                {
                    _logger.LogError(
                        "Ignoring shipping.promise.calculated without valid checkoutId topic={Topic} key={MessageKey} eventId={EventId} correlationId={CorrelationId}",
                        result.Topic,
                        result.Message.Key,
                        envelope.EventId,
                        envelope.CorrelationId);
                    consumer.Commit(result);
                    continue;
                }

                await using var scope = _scopeFactory.CreateAsyncScope();
                var repository = scope.ServiceProvider.GetRequiredService<IShippingPromiseProjectionRepository>();
                if (await repository.HasProcessedAsync(envelope.EventId, envelope.CorrelationId, envelope.Payload.CheckoutId, stoppingToken))
                {
                    _logger.LogInformation("Kafka duplicate ignored topic={Topic} key={MessageKey} eventType={EventType} correlationId={CorrelationId}", result.Topic, result.Message.Key, envelope.EventType, envelope.CorrelationId);
                    consumer.Commit(result);
                    continue;
                }

                await repository.RecordAsync(
                    envelope.EventId,
                    envelope.CorrelationId,
                    envelope.Payload.CheckoutId,
                    envelope.Payload.PromiseId,
                    envelope.Payload.Mode,
                    envelope.Payload.Carrier,
                    envelope.Payload.EstimatedDeliveryDate,
                    envelope.Payload.Cost,
                    stoppingToken);
                _logger.LogInformation("Kafka message consumed topic={Topic} key={MessageKey} eventType={EventType} correlationId={CorrelationId}", result.Topic, result.Message.Key, envelope.EventType, envelope.CorrelationId);
                consumer.Commit(result);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Kafka consume failed topic={Topic}", _options.Topics.ShippingPromiseCalculated);
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }
}
