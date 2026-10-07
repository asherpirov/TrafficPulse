using TrafficShared.Repositories;
using TrafficShared.Services;
namespace TrafficWeb.Services;

public class DemoRefreshService : BackgroundService
{
    private readonly ITrafficRepository _repository;
    private readonly ILogger<DemoRefreshService> _logger;
    public DemoRefreshService(ITrafficRepository repository, ILogger<DemoRefreshService> logger)
    {
        _repository = repository;
        _logger = logger;
    }
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var source = new DemoTrafficService();
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                foreach (var road in (await _repository.GetRoadsAsync()).Where(r => r.IsActive))
                    await _repository.SaveReadingAsync(await source.GetReadingAsync(road, stoppingToken));
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception ex) { _logger.LogError(ex, "Demo refresh failed"); }
        }
    }
}
