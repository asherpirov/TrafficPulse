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
var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("TrafficProducer");
using var stop = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; stop.Cancel(); };
host.Services.GetRequiredService<IHostApplicationLifetime>().ApplicationStopping.Register(stop.Cancel);
await host.StartAsync(stop.Token);

using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
string provider = builder.Configuration["Traffic:Provider"] ?? "TomTom";
ITrafficSource source = provider switch
{
    "Demo" => new DemoTrafficService(),
    "TomTom" => new TomTomTrafficService(http, builder.Configuration["Traffic:ApiKey"] ?? ""),
    _ => throw new InvalidOperationException("Traffic__Provider must be Demo or TomTom.")
};
int interval = builder.Configuration.GetValue<int>("Traffic:PollSeconds", 60);
if (interval < 30) throw new InvalidOperationException("Traffic__PollSeconds must be at least 30.");
using var producer = new ProducerBuilder<string, string>(new ProducerConfig
{
    BootstrapServers = kafka.BootstrapServers, EnableIdempotence = true, Acks = Acks.All, MessageTimeoutMs = 30000
}).Build();
logger.LogInformation("Producer started. Provider: {Provider}; interval: {Seconds}s", provider, interval);

try
{
    while (!stop.IsCancellationRequested)
    {
        try
        {
            using var scope = host.Services.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<ITrafficRepository>();
            foreach (Road road in (await repository.GetRoadsAsync()).Where(r => r.IsActive))
            {
                try
                {
                    TrafficReading reading = await source.GetReadingAsync(road, stop.Token);
                    var message = new Message<string, string> { Key = road.Id.ToString(), Value = JsonSerializer.Serialize(reading) };
                    // חוזרים על אותה הודעה עם אותו Id; אין יצירת מדידה חדשה בכל ניסיון.
                    bool delivered = false;
                    for (int attempt = 1; attempt <= 3 && !delivered; attempt++)
                    {
                        try { await producer.ProduceAsync(kafka.Topic, message, stop.Token); delivered = true; }
                        catch (ProduceException<string, string>) when (attempt < 3)
                        { await Task.Delay(TimeSpan.FromSeconds(2 * attempt), stop.Token); }
                    }
                    logger.LogInformation("Published reading {ReadingId} for road {RoadId}", reading.Id, road.Id);
                }
                catch (OperationCanceledException) when (stop.IsCancellationRequested) { throw; }
                catch (Exception ex)
                {
                    // אין כתובת URL בלוג: היא עלולה להכיל את מפתח הספק.
                    logger.LogWarning("Road {RoadId} was not collected/published. Error type: {Type}", road.Id, ex.GetType().Name);
                }
            }
        }
        catch (OperationCanceledException) when (stop.IsCancellationRequested) { throw; }
        catch (Exception ex) { logger.LogError("Collection cycle failed: {Type}", ex.GetType().Name); }
        await Task.Delay(TimeSpan.FromSeconds(interval), stop.Token);
    }
}
catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
finally { producer.Flush(TimeSpan.FromSeconds(5)); await host.StopAsync(); }
