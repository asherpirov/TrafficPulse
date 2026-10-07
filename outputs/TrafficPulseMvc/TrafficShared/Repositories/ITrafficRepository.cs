using TrafficShared.Models;
namespace TrafficShared.Repositories;

public interface ITrafficRepository
{
    Task InitializeAsync();
    Task<List<Road>> GetRoadsAsync();
    Task<Road?> GetRoadAsync(Guid id);
    Task SaveRoadAsync(Road road);
    Task<List<TrafficReading>> GetLatestAsync();
    Task<List<TrafficReading>> GetHistoryAsync(Guid roadId, DateTime fromUtc, DateTime toUtc, int limit = 500);
    Task<List<TrafficAlert>> GetAlertsAsync(Guid? roadId = null, string? status = null);
    // המדידה וההתרעה נכתבות באותה טרנזקציה. false = אירוע כפול.
    Task<bool> SaveReadingAsync(TrafficReading reading);
    Task UpdateAlertAsync(Guid id, string status, int version, Guid actorId);
    Task<List<Guid>> GetFavoritesAsync(Guid userId);
    Task SetFavoriteAsync(Guid userId, Guid roadId, bool enabled);
}
