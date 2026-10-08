using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TrafficShared.Models;
using TrafficShared.Repositories;
using TrafficShared.Services;

namespace TrafficConsumer.Services;

public class ConsumerService : IConsumerService
{
    private readonly IConsumer<string, string> _consumer;
    private readonly IProducer<string, string> _invalidProducer;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly KafkaSettings _settings;
    private readonly ILogger<ConsumerService> _logger;
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    public ConsumerService(IConsumer<string, string> consumer, IProducer<string, string> invalidProducer,
        IServiceScopeFactory scopeFactory, KafkaSettings settings, ILogger<ConsumerService> logger)
    {
        _consumer = consumer;
        _invalidProducer = invalidProducer;
        _scopeFactory = scopeFactory;
        _settings = settings;
        _logger = logger;
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        _consumer.Subscribe(_settings.Topic);
        _logger.LogInformation("Subscribed to {Topic}", _settings.Topic);
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                ConsumeResult<string, string>? result = null;
                try
                {
                    result = _consumer.Consume(cancellationToken);
                    await ProcessAsync(result, cancellationToken);
                    // Save הצליח, או שהודעה פסולה נכתבה בהצלחה ל-DLQ.
                    _consumer.Commit(result);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { break; }
                catch (Exception ex)
                {
                    _logger.LogError("Processing failed; no commit. Error: {Type}", ex.GetType().Name);
                    if (result != null) RetryFrom(result);
                    await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        finally { _consumer.Close(); }
    }

    private async Task ProcessAsync(ConsumeResult<string, string> result, CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(result.Message.Value)) throw new BusinessException("Empty message");
            var reading = JsonSerializer.Deserialize<TrafficReading>(result.Message.Value, _jsonOptions)
                ?? throw new BusinessException("Missing reading");
            TrafficValidator.Validate(reading);
            using var scope = _scopeFactory.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<ITrafficRepository>();
            bool inserted = await repository.SaveReadingAsync(reading);
            _logger.LogInformation("Reading {Id}: {Result}", reading.Id, inserted ? "saved" : "duplicate skipped");
        }
        catch (Exception ex) when (ex is JsonException or BusinessException)
        {
            var invalid = new
            {
                original = result.Message.Value, reason = ex is BusinessException ? ex.Message : "Invalid JSON",
                topic = result.Topic, partition = result.Partition.Value, offset = result.Offset.Value,
                rejectedAtUtc = DateTime.UtcNow
            };
            await _invalidProducer.ProduceAsync(_settings.DeadLetterTopic,
                new Message<string, string> { Key = result.Message.Key, Value = JsonSerializer.Serialize(invalid) }, cancellationToken);
            _logger.LogWarning("Invalid message delivered to DLQ. Offset: {Offset}", result.Offset.Value);
        }
        // כשל מסד אינו נתפס כאן: הוא מגיע ללולאה, ללא Commit.
    }

    private void RetryFrom(ConsumeResult<string, string> result)
    {
        try { _consumer.Seek(result.TopicPartitionOffset); }
        catch (KafkaException)
        {
            _consumer.Unsubscribe();
            _consumer.Subscribe(_settings.Topic);
        }
    }
}
