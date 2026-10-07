using System.Text.Json;
using Confluent.Kafka;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TrafficShared.Data;
using TrafficShared.Models;
using TrafficShared.Repositories;
using TrafficShared.Services;

var builder = Host.CreateApplicationBuilder(args);
string connection = builder.Configuration.GetConnectionString("MySql") ?? throw new InvalidOperationException("Set ConnectionStrings__MySql.");
var kafka = builder.Configuration.GetSection("Kafka").Get<KafkaSettings>() ?? throw new InvalidOperationException("Set Kafka configuration.");
if (string.IsNullOrWhiteSpace(kafka.BootstrapServers)) throw new InvalidOperationException("Set Kafka__BootstrapServers.");
builder.Services.AddDbContext<TrafficDbContext>(options => options.UseMySql(connection, ServerVersion.AutoDetect(connection)));
builder.Services.AddSingleton(builder.Configuration.GetSection("Analysis").Get<AnalysisSettings>() ?? new AnalysisSettings());
builder.Services.AddSingleton<AnomalyService>();
builder.Services.AddScoped<ITrafficRepository, TrafficRepository>();
using var host = builder.Build();
var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("TrafficConsumer");
using var stop = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; stop.Cancel(); };
host.Services.GetRequiredService<IHostApplicationLifetime>().ApplicationStopping.Register(stop.Cancel);
await host.StartAsync(stop.Token);
using var consumer = new ConsumerBuilder<string, string>(new ConsumerConfig
{
    BootstrapServers = kafka.BootstrapServers, GroupId = kafka.GroupId,
    AutoOffsetReset = AutoOffsetReset.Earliest, EnableAutoCommit = false, EnableAutoOffsetStore = false,
    MaxPollIntervalMs = 300000
}).Build();
using var invalidProducer = new ProducerBuilder<string, string>(new ProducerConfig
{
    BootstrapServers = kafka.BootstrapServers, EnableIdempotence = true, Acks = Acks.All, MessageTimeoutMs = 30000
}).Build();
consumer.Subscribe(kafka.Topic);
var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

try
{
    while (!stop.IsCancellationRequested)
    {
        ConsumeResult<string, string>? result = null;
        try
        {
            result = consumer.Consume(stop.Token);
            TrafficReading? reading = null;
            string? invalidReason = null;
            try
            {
                if (string.IsNullOrWhiteSpace(result.Message.Value)) throw new BusinessException("Empty message");
                reading = JsonSerializer.Deserialize<TrafficReading>(result.Message.Value, jsonOptions) ?? throw new BusinessException("Missing reading");
                TrafficValidator.Validate(reading);
            }
            catch (Exception ex) when (ex is JsonException or BusinessException)
            { invalidReason = ex.GetType().Name; }

            if (invalidReason == null)
            {
                using var scope = host.Services.CreateScope();
                var repository = scope.ServiceProvider.GetRequiredService<ITrafficRepository>();
                try
                {
                    bool inserted = await repository.SaveReadingAsync(reading!);
                    logger.LogInformation("Reading {Id}: {Result}", reading!.Id, inserted ? "saved" : "duplicate skipped");
                }
                catch (BusinessException ex) { invalidReason = ex.Message; }
            }
            if (invalidReason != null)
            {
                var deadLetter = new
                {
                    original = result.Message.Value, reason = invalidReason,
                    topic = result.Topic, partition = result.Partition.Value, offset = result.Offset.Value,
                    rejectedAtUtc = DateTime.UtcNow
                };
                await invalidProducer.ProduceAsync(kafka.DeadLetterTopic,
                    new Message<string, string> { Key = result.Message.Key, Value = JsonSerializer.Serialize(deadLetter) }, stop.Token);
                logger.LogWarning("Invalid record sent to dead-letter topic. Offset {Offset}", result.Offset.Value);
            }
            // מאשרים רק אחרי Commit למסד או אחרי אישור כתיבה ל-DLQ.
            consumer.Commit(result);
        }
        catch (OperationCanceledException) when (stop.IsCancellationRequested) { break; }
        catch (Exception ex)
        {
            logger.LogError("Processing failed; offset not acknowledged. Error type: {Type}", ex.GetType().Name);
            if (result != null)
            {
                try { consumer.Seek(result.TopicPartitionOffset); }
                catch (KafkaException) { consumer.Unsubscribe(); consumer.Subscribe(kafka.Topic); }
            }
            await Task.Delay(TimeSpan.FromSeconds(5), stop.Token);
        }
    }
}
catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
finally { consumer.Close(); await host.StopAsync(); }
