using Confluent.Kafka;
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
