from pathlib import Path
root = Path(__file__).resolve().parents[1] / 'outputs' / 'TrafficPulseMvc'
(root / 'TrafficProducer/Program.cs').write_text('''using Confluent.Kafka;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TrafficProducer.Services;
using TrafficShared.Data;
using TrafficShared.Models;
using TrafficShared.Repositories;
using TrafficShared.Services;

var builder = Host.CreateApplicationBuilder(args);
string connection = builder.Configuration.GetConnectionString("MySql") ?? throw new InvalidOperationException("Set ConnectionStrings__MySql.");
var kafka = builder.Configuration.GetSection("Kafka").Get<KafkaSettings>() ?? new KafkaSettings();
var settings = builder.Configuration.GetSection("Traffic").Get<ProducerSettings>() ?? new ProducerSettings();
if (string.IsNullOrWhiteSpace(kafka.BootstrapServers)) throw new InvalidOperationException("Set Kafka__BootstrapServers.");
if (settings.PollSeconds < 30) throw new InvalidOperationException("Traffic__PollSeconds must be at least 30.");
builder.Services.AddSingleton(kafka);
builder.Services.AddSingleton(settings);
builder.Services.AddDbContext<TrafficDbContext>(options => options.UseMySql(connection, ServerVersion.AutoDetect(connection)));
builder.Services.AddSingleton(builder.Configuration.GetSection("Analysis").Get<AnalysisSettings>() ?? new AnalysisSettings());
builder.Services.AddSingleton<AnomalyService>();
builder.Services.AddScoped<ITrafficRepository, TrafficRepository>();

if (settings.Provider == "Demo")
    builder.Services.AddSingleton<ITrafficSource, DemoTrafficService>();
else if (settings.Provider == "TomTom")
{
    string apiKey = Environment.GetEnvironmentVariable("TOMTOM_API_KEY") ?? throw new InvalidOperationException("Set TOMTOM_API_KEY.");
    builder.Services.AddSingleton(new HttpClient { Timeout = TimeSpan.FromSeconds(15) });
    builder.Services.AddSingleton<ITrafficSource>(provider => new TomTomTrafficService(provider.GetRequiredService<HttpClient>(), apiKey));
}
else throw new InvalidOperationException("Traffic__Provider must be TomTom or Demo.");

builder.Services.AddSingleton<IProducer<string, string>>(_ => new ProducerBuilder<string, string>(new ProducerConfig
{
    BootstrapServers = kafka.BootstrapServers, EnableIdempotence = true, Acks = Acks.All, MessageTimeoutMs = 30000
}).Build());
builder.Services.AddSingleton<IProducerService, ProducerService>();
using var host = builder.Build();
await host.StartAsync();
var lifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();
await host.Services.GetRequiredService<IProducerService>().RunAsync(lifetime.ApplicationStopping);
await host.StopAsync();
''', encoding='utf-8')
(root / 'TrafficConsumer/Program.cs').write_text('''using Confluent.Kafka;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TrafficConsumer.Services;
using TrafficShared.Data;
using TrafficShared.Models;
using TrafficShared.Repositories;
using TrafficShared.Services;

var builder = Host.CreateApplicationBuilder(args);
string connection = builder.Configuration.GetConnectionString("MySql") ?? throw new InvalidOperationException("Set ConnectionStrings__MySql.");
var kafka = builder.Configuration.GetSection("Kafka").Get<KafkaSettings>() ?? new KafkaSettings();
if (string.IsNullOrWhiteSpace(kafka.BootstrapServers)) throw new InvalidOperationException("Set Kafka__BootstrapServers.");
builder.Services.AddSingleton(kafka);
builder.Services.AddDbContext<TrafficDbContext>(options => options.UseMySql(connection, ServerVersion.AutoDetect(connection)));
builder.Services.AddSingleton(builder.Configuration.GetSection("Analysis").Get<AnalysisSettings>() ?? new AnalysisSettings());
builder.Services.AddSingleton<AnomalyService>();
builder.Services.AddScoped<ITrafficRepository, TrafficRepository>();
builder.Services.AddSingleton<IConsumer<string, string>>(_ => new ConsumerBuilder<string, string>(new ConsumerConfig
{
    BootstrapServers = kafka.BootstrapServers, GroupId = kafka.GroupId,
    AutoOffsetReset = AutoOffsetReset.Earliest, EnableAutoCommit = false, EnableAutoOffsetStore = false,
    MaxPollIntervalMs = 300000
}).Build());
builder.Services.AddSingleton<IProducer<string, string>>(_ => new ProducerBuilder<string, string>(new ProducerConfig
{
    BootstrapServers = kafka.BootstrapServers, EnableIdempotence = true, Acks = Acks.All, MessageTimeoutMs = 30000
}).Build());
builder.Services.AddSingleton<IConsumerService, ConsumerService>();
using var host = builder.Build();
await host.StartAsync();
var lifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();
await host.Services.GetRequiredService<IConsumerService>().RunAsync(lifetime.ApplicationStopping);
await host.StopAsync();
''', encoding='utf-8')
