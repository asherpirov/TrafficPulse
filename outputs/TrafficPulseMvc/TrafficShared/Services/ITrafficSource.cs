using TrafficShared.Models;
namespace TrafficShared.Services;
public interface ITrafficSource
{
    Task<TrafficReading> GetReadingAsync(Road road, CancellationToken cancellationToken);
}
