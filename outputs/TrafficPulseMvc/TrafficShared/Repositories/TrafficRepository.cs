using System.Data;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using TrafficShared.Data;
using TrafficShared.Models;
using TrafficShared.Services;

namespace TrafficShared.Repositories;

public class TrafficRepository : ITrafficRepository
{
    private readonly TrafficDbContext _context;
    private readonly AnomalyService _anomalyService;
    public TrafficRepository(TrafficDbContext context, AnomalyService anomalyService)
    {
        _context = context;
        _anomalyService = anomalyService;
    }
    // הסכימה נוצרת ומתעדכנת דרך Migrations של EF.
    public async Task InitializeAsync() => await _context.Database.MigrateAsync();
    public Task<List<Road>> GetRoadsAsync() => _context.Roads.AsNoTracking().OrderBy(r => r.Name).ToListAsync();
    public Task<Road?> GetRoadAsync(Guid id) => _context.Roads.AsNoTracking().SingleOrDefaultAsync(r => r.Id == id);

    public async Task SaveRoadAsync(Road road)
    {
        TrafficValidator.Validate(road);
        if (road.Version == 0)
        {
            road.Version = 1;
            _context.Roads.Add(road);
        }
        else
        {
            var existing = await _context.Roads.SingleOrDefaultAsync(r => r.Id == road.Id)
                ?? throw new BusinessException("המקטע לא נמצא.", 404);
            if (existing.Version != road.Version) throw new BusinessException("המקטע השתנה. רעננו לפני שמירה.", 409);
            if (existing.Latitude != road.Latitude || existing.Longitude != road.Longitude)
                throw new BusinessException("למיקום חדש יש ליצור מקטע חדש כדי לא לערבב היסטוריה.");
            existing.Name = road.Name;
            existing.IsActive = road.IsActive;
            existing.SigmaThreshold = road.SigmaThreshold;
            existing.Version++;
        }
        await SaveChangesAsync();
    }

    public Task<List<TrafficReading>> GetLatestAsync()
    {
        // GroupBy + First מתורגם על ידי EF לשאילתה שמחזירה רשומה אחרונה לכל מקטע.
        return _context.Readings.AsNoTracking().GroupBy(r => r.RoadId)
            .Select(group => group.OrderByDescending(r => r.CollectedAtUtc).ThenByDescending(r => r.Id).First()).ToListAsync();
    }
    public Task<List<TrafficReading>> GetHistoryAsync(Guid roadId, DateTime fromUtc, DateTime toUtc, int limit = 500)
    {
        return _context.Readings.AsNoTracking()
            .Where(r => r.RoadId == roadId && r.CollectedAtUtc >= fromUtc && r.CollectedAtUtc <= toUtc)
            .OrderByDescending(r => r.CollectedAtUtc).Take(Math.Clamp(limit, 1, 50000)).ToListAsync();
    }
    public Task<List<TrafficAlert>> GetAlertsAsync(Guid? roadId = null, string? status = null)
    {
        var query = _context.Alerts.AsNoTracking();
        if (roadId.HasValue) query = query.Where(a => a.RoadId == roadId.Value);
        if (status != null) query = query.Where(a => a.Status == status);
        return query.OrderByDescending(a => a.CreatedAtUtc).Take(200).ToListAsync();
    }

    public async Task<bool> SaveReadingAsync(TrafficReading reading)
    {
        TrafficValidator.Validate(reading);
        await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        if (await _context.Readings.AnyAsync(r => r.Id == reading.Id)) return false;
        var road = await _context.Roads.SingleOrDefaultAsync(r => r.Id == reading.RoadId)
            ?? throw new BusinessException("מקטע לא מוכר.");
        DateTime earliest = reading.CollectedAtUtc.AddDays(-_anomalyService.HistoryDays);
        var history = await _context.Readings.AsNoTracking()
            .Where(r => r.RoadId == reading.RoadId && r.CollectedAtUtc >= earliest && r.CollectedAtUtc < reading.CollectedAtUtc)
            .ToListAsync();
        var alert = _anomalyService.Analyze(reading, history, road.SigmaThreshold);
        _context.Readings.Add(reading);
        if (alert != null) _context.Alerts.Add(alert);
        try
        {
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            return true;
        }
        catch (DbUpdateException ex) when (ex.InnerException is MySqlException { Number: 1062 })
        {
            await transaction.RollbackAsync();
            _context.ChangeTracker.Clear();
            return false;
        }
    }
    public async Task UpdateAlertAsync(Guid id, string status, int version, Guid actorId)
    {
        TrafficValidator.ValidateStatus(status);
        var alert = await _context.Alerts.SingleOrDefaultAsync(a => a.Id == id) ?? throw new BusinessException("ההתרעה לא נמצאה.", 404);
        if (alert.Version != version) throw new BusinessException("ההתרעה השתנתה. רעננו לפני עדכון.", 409);
        alert.Status = status;
        alert.Version++;
        alert.UpdatedBy = actorId;
        alert.UpdatedAtUtc = DateTime.UtcNow;
        await SaveChangesAsync();
    }
    public Task<List<Guid>> GetFavoritesAsync(Guid userId) => _context.Favorites.AsNoTracking().Where(f => f.UserId == userId).Select(f => f.RoadId).ToListAsync();
    public async Task SetFavoriteAsync(Guid userId, Guid roadId, bool enabled)
    {
        if (!await _context.Roads.AnyAsync(r => r.Id == roadId)) throw new BusinessException("המקטע לא נמצא.", 404);
        var favorite = await _context.Favorites.SingleOrDefaultAsync(f => f.UserId == userId && f.RoadId == roadId);
        if (enabled && favorite == null) _context.Favorites.Add(new Favorite { UserId = userId, RoadId = roadId });
        if (!enabled && favorite != null) _context.Favorites.Remove(favorite);
        try { await _context.SaveChangesAsync(); }
        catch (DbUpdateException ex) when (ex.InnerException is MySqlException { Number: 1062 }) { _context.ChangeTracker.Clear(); }
    }
    private async Task SaveChangesAsync()
    {
        try { await _context.SaveChangesAsync(); }
        catch (DbUpdateConcurrencyException) { throw new BusinessException("המידע השתנה בבקשה אחרת. רעננו ונסו שוב.", 409); }
    }
}
