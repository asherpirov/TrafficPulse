using System.Text.Json;
using TrafficShared.Models;
using TrafficShared.Services;

namespace TrafficShared.Repositories;

// מתאם הדגמה בלבד: שומר JSON מקומי במקום MySQL, באותו ממשק בדיוק.
// הוא מיועד לתהליך אתר יחיד. Producer/Consumer משתמשים תמיד ב-MySQL.
public class DemoRepository : ITrafficRepository, IUserRepository
{
    private readonly string _path;
    private readonly AnomalyService _anomalyService;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private DemoData _data = new();

    public DemoRepository(string path, AnomalyService anomalyService)
    {
        _path = Path.GetFullPath(path);
        _anomalyService = anomalyService;
    }

    public class DemoData
    {
        public List<AppUser> Users { get; set; } = [];
        public List<Road> Roads { get; set; } = [];
        public List<TrafficReading> Readings { get; set; } = [];
        public List<TrafficAlert> Alerts { get; set; } = [];
        public List<Favorite> Favorites { get; set; } = [];
    }

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        if (File.Exists(_path))
            _data = JsonSerializer.Deserialize<DemoData>(await File.ReadAllTextAsync(_path))
                ?? throw new InvalidDataException("Invalid demo file; restore a backup.");
    }

    private async Task<T> ReadAsync<T>(Func<DemoData, T> query)
    {
        await _lock.WaitAsync();
        try
        {
            // מחזירים עותק כדי שקונטרולר לא יוכל לשנות נתונים בלי Save.
            var copy = JsonSerializer.Deserialize<DemoData>(JsonSerializer.Serialize(_data))!;
            return query(copy);
        }
        finally { _lock.Release(); }
    }

    private async Task WriteAsync(Action<DemoData> change)
    {
        await _lock.WaitAsync();
        try
        {
            var copy = JsonSerializer.Deserialize<DemoData>(JsonSerializer.Serialize(_data))!;
            change(copy);
            // החלפת קובץ לאחר כתיבה מלאה. כישלון לא משנה את העותק שבזיכרון.
            await File.WriteAllTextAsync(_path + ".tmp", JsonSerializer.Serialize(copy));
            File.Move(_path + ".tmp", _path, true);
            _data = copy;
        }
        finally { _lock.Release(); }
    }

    public Task<AppUser?> GetByEmailAsync(string email) => ReadAsync(d => d.Users.Find(u => u.Email == email));
    public Task<AppUser?> GetUserAsync(Guid id) => ReadAsync(d => d.Users.Find(u => u.Id == id));
    public Task<List<AppUser>> GetUsersAsync() => ReadAsync(d => d.Users.OrderBy(u => u.Name).ToList());
    public Task AddUserAsync(AppUser user) => WriteAsync(d =>
    {
        if (d.Users.Any(u => u.Email == user.Email)) throw new BusinessException("לא ניתן להירשם עם כתובת זו.", 409);
        d.Users.Add(user);
    });
    public Task RecordLoginAsync(Guid id, bool success) => WriteAsync(d =>
    {
        var user = d.Users.Single(u => u.Id == id);
        if (success) { user.FailedAttempts = 0; user.LockedUntilUtc = null; }
        else
        {
            if (user.LockedUntilUtc <= DateTime.UtcNow) user.FailedAttempts = 0;
            user.FailedAttempts++;
            if (user.FailedAttempts >= 5) user.LockedUntilUtc = DateTime.UtcNow.AddMinutes(10);
        }
    });
    public Task SetUserActiveAsync(Guid id, bool active) => WriteAsync(d =>
    {
        var user = d.Users.Find(u => u.Id == id) ?? throw new BusinessException("המשתמש לא נמצא.", 404);
        if (user.Role == "Admin") throw new BusinessException("לא ניתן להשבית מנהל במסך זה.");
        user.IsActive = active;
        user.SecurityStamp = Guid.NewGuid().ToString();
    });
    public Task<List<Road>> GetRoadsAsync() => ReadAsync(d => d.Roads.OrderBy(r => r.Name).ToList());
    public Task<Road?> GetRoadAsync(Guid id) => ReadAsync(d => d.Roads.Find(r => r.Id == id));
    public Task SaveRoadAsync(Road road) => WriteAsync(d =>
    {
        TrafficValidator.Validate(road);
        Road? existing = d.Roads.Find(r => r.Id == road.Id);
        if (existing == null)
        {
            if (road.Version != 0) throw new BusinessException("המקטע לא נמצא.", 404);
            road.Version = 1;
            d.Roads.Add(road);
            return;
        }
        if (existing.Version != road.Version) throw new BusinessException("המקטע השתנה. טענו מחדש לפני שמירה.", 409);
        if (existing.Latitude != road.Latitude || existing.Longitude != road.Longitude)
            throw new BusinessException("למיקום חדש יש ליצור מקטע חדש כדי לשמור על ההיסטוריה.");
        existing.Name = road.Name;
        existing.IsActive = road.IsActive;
        existing.SigmaThreshold = road.SigmaThreshold;
        existing.Version++;
    });
    public Task<List<TrafficReading>> GetLatestAsync() => ReadAsync(d => d.Readings.GroupBy(r => r.RoadId).Select(g => g.OrderByDescending(r => r.CollectedAtUtc).First()).ToList());
    public Task<List<TrafficReading>> GetHistoryAsync(Guid id, DateTime from, DateTime to, int limit = 500) =>
        ReadAsync(d => d.Readings.Where(r => r.RoadId == id && r.CollectedAtUtc >= from && r.CollectedAtUtc <= to).OrderByDescending(r => r.CollectedAtUtc).Take(limit).ToList());
    public Task<List<TrafficAlert>> GetAlertsAsync(Guid? id = null, string? status = null) =>
        ReadAsync(d => d.Alerts.Where(a => (id == null || a.RoadId == id) && (status == null || a.Status == status)).OrderByDescending(a => a.CreatedAtUtc).Take(200).ToList());
    public async Task<bool> SaveReadingAsync(TrafficReading reading)
    {
        bool inserted = false;
        await WriteAsync(d =>
        {
            TrafficValidator.Validate(reading);
            if (d.Readings.Any(r => r.Id == reading.Id)) return;
            var road = d.Roads.Find(r => r.Id == reading.RoadId) ?? throw new BusinessException("מקטע לא מוכר.");
            var alert = _anomalyService.Analyze(reading, d.Readings, road.SigmaThreshold);
            d.Readings.Add(reading);
            if (alert != null) d.Alerts.Add(alert);
            inserted = true;
        });
        return inserted;
    }
    public Task UpdateAlertAsync(Guid id, string status, int version, Guid actorId) => WriteAsync(d =>
    {
        TrafficValidator.ValidateStatus(status);
        var alert = d.Alerts.Find(a => a.Id == id) ?? throw new BusinessException("ההתרעה לא נמצאה.", 404);
        if (alert.Version != version) throw new BusinessException("ההתרעה השתנתה. רעננו ונסו שוב.", 409);
        alert.Status = status;
        alert.Version++;
        alert.UpdatedBy = actorId;
        alert.UpdatedAtUtc = DateTime.UtcNow;
    });
    public Task<List<Guid>> GetFavoritesAsync(Guid id) => ReadAsync(d => d.Favorites.Where(f => f.UserId == id).Select(f => f.RoadId).ToList());
    public Task SetFavoriteAsync(Guid userId, Guid roadId, bool enabled) => WriteAsync(d =>
    {
        if (!d.Roads.Any(r => r.Id == roadId)) throw new BusinessException("המקטע לא נמצא.", 404);
        d.Favorites.RemoveAll(f => f.UserId == userId && f.RoadId == roadId);
        if (enabled) d.Favorites.Add(new Favorite { UserId = userId, RoadId = roadId });
    });
}
