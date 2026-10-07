using TrafficShared.Models;
namespace TrafficShared.Services;

public class DemoTrafficService : ITrafficSource
{
    public Task<TrafficReading> GetReadingAsync(Road road, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        double speed = 45 + Random.Shared.Next(0, 20);
        if (Random.Shared.Next(0, 5) == 0) speed = 15;
        return Task.FromResult(new TrafficReading { RoadId = road.Id, CurrentSpeed = speed, FreeFlowSpeed = 70, Confidence = 0.95, Source = "Demo" });
    }
}
