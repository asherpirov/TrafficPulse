namespace TrafficConsumer.Services;
public interface IConsumerService
{
    Task RunAsync(CancellationToken cancellationToken);
}
