using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TrafficShared.Models;
using TrafficShared.Repositories;
using TrafficShared.Services;

namespace TrafficProducer.Services;

public class ProducerService : IProducerService
{
    private readonly ITrafficSource _source;
    private readonly IProducer<string, string> _producer;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly KafkaSettings _kafka;
    private readonly ProducerSettings _settings;
    private readonly ILogger<ProducerService> _logger;

    public ProducerService(ITrafficSource source, IProducer<string, string> producer,
        IServiceScopeFactory scopeFactory, KafkaSettings kafka, ProducerSettings settings, ILogger<ProducerService> logger)
    {
        _source = source;
        _producer = producer;
        _scopeFactory = scopeFactory;
        _kafka = kafka;
        _settings = settings;
        _logger = logger;
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Producer started. Provider: {Provider}", _settings.Provider);
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try { await CollectOnceAsync(cancellationToken); }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
                catch (Exception ex) { _logger.LogError("Collection cycle failed: {Type}", ex.GetType().Name); }
                await Task.Delay(TimeSpan.FromSeconds(_settings.PollSeconds), cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        finally { _producer.Flush(TimeSpan.FromSeconds(5)); }
    }

    private async Task CollectOnceAsync(CancellationToken cancellationToken)
    {
        // Scope נותן DbContext חדש לכל מחזור. ה-Service עצמו חי לאורך התהליך.
        using var scope = _scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ITrafficRepository>();
        List<Road> roads = await repository.GetRoadsAsync();
        foreach (Road road in roads)
        {
            if (!road.IsActive) continue;
            try
            {
                TrafficReading reading = await _source.GetReadingAsync(road, cancellationToken);
                await PublishAsync(reading, cancellationToken);
                _logger.LogInformation("Published {ReadingId} for {RoadId}", reading.Id, road.Id);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                // לא מדפיסים URL או exception שמכיל מפתח API.
                _logger.LogWarning("Road {RoadId} failed: {Type}", road.Id, ex.GetType().Name);
            }
        }
    }

    private async Task PublishAsync(TrafficReading reading, CancellationToken cancellationToken)
    {
        var message = new Message<string, string>
        {
            Key = reading.RoadId.ToString(),
            Value = JsonSerializer.Serialize(reading)
        };
        for (int attempt = 1; attempt <= 3; attempt++)
        {
            try
            {
                await _producer.ProduceAsync(_kafka.Topic, message, cancellationToken);
                return;
            }
            catch (ProduceException<string, string>) when (attempt < 3)
            {
                await Task.Delay(TimeSpan.FromSeconds(2 * attempt), cancellationToken);
            }
        }
    }
}
