using System.Text.Json;
using CheckoutService.Contracts;
using Confluent.Kafka;
using Microsoft.Extensions.Options;

namespace CheckoutService.Infrastructure.Messaging;

public interface IKafkaProducer
{
    Task ProduceAsync<TPayload>(string topic, string key, KafkaEventEnvelope<TPayload> envelope, CancellationToken cancellationToken);
}

public sealed class KafkaProducer : IKafkaProducer, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IProducer<string, string> _producer;
    private readonly ILogger<KafkaProducer> _logger;

    public KafkaProducer(IOptions<KafkaOptions> options, ILogger<KafkaProducer> logger)
    {
        _logger = logger;
        _producer = new ProducerBuilder<string, string>(new ProducerConfig
        {
            BootstrapServers = options.Value.BootstrapServers,
            Acks = Acks.All,
            EnableIdempotence = true,
            MessageTimeoutMs = 5000
        }).Build();
    }

    public async Task ProduceAsync<TPayload>(string topic, string key, KafkaEventEnvelope<TPayload> envelope, CancellationToken cancellationToken)
    {
        var value = JsonSerializer.Serialize(envelope, JsonOptions);
        var result = await _producer.ProduceAsync(topic, new Message<string, string> { Key = key, Value = value }, cancellationToken);
        _logger.LogInformation("Kafka message produced topic={Topic} key={MessageKey} eventType={EventType} correlationId={CorrelationId} partition={Partition} offset={Offset}", topic, key, envelope.EventType, envelope.CorrelationId, result.Partition.Value, result.Offset.Value);
    }

    public void Dispose() => _producer.Dispose();
}
