namespace TrafficProducer.Services;
public interface IProducerService
{
    Task RunAsync(CancellationToken cancellationToken);
}
