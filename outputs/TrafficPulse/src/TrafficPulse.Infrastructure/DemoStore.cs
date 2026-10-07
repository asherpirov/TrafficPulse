using System.Text.Json;
using TrafficPulse.Core;

namespace TrafficPulse.Infrastructure;

/// <summary>Single-process durable demo adapter. Real deployments use MySqlStore.</summary>
public sealed class DemoStore(string path) : IStore
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private State state = new();
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public sealed class State
    {
        public List<User> Users { get; set; } = [];
        public List<Segment> Segments { get; set; } = [];
        public List<Reading> Readings { get; set; } = [];
        public List<Anomaly> Anomalies { get; set; } = [];
        public List<Session> Sessions { get; set; } = [];
        public List<Favorite> Favorites { get; set; } = [];
        public List<Audit> Audits { get; set; } = [];
    }

    public async Task Initialize(CancellationToken ct)
    {
        await gate.WaitAsync(ct);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            if (File.Exists(path))
                state = JsonSerializer.Deserialize<State>(await File.ReadAllTextAsync(path, ct), Json)
                    ?? throw new InvalidDataException("Invalid demo database. Restore a backup; do not overwrite.");
        }
        finally { gate.Release(); }
    }

    private async Task<T> Read<T>(Func<State, T> query, CancellationToken ct)
    {
        await gate.WaitAsync(ct);
        try { return query(state); }
        finally { gate.Release(); }
    }

    private async Task<T> Write<T>(Func<State, T> action, CancellationToken ct)
    {
        await gate.WaitAsync(ct);
        try
        {
            // Work on a copy: a failed persistence operation must not change visible state.
            var copy = JsonSerializer.Deserialize<State>(JsonSerializer.Serialize(state, Json), Json)!;
            var result = action(copy);
            copy.Sessions.RemoveAll(s => s.ExpiresAt <= DateTimeOffset.UtcNow);
            var temporary = path + ".tmp";
            await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(copy, Json), ct);
            File.Move(temporary, path, true);
            state = copy;
            return result;
        }
        finally { gate.Release(); }
    }

    private static bool Log(State state, Guid actor, string action, string target)
    {
        state.Audits.Add(new(Guid.NewGuid(), actor, action, target, DateTimeOffset.UtcNow));
        return true;
    }

    public Task<User?> FindUser(string email, CancellationToken ct) => Read(s => s.Users.Find(u => u.Email == email), ct);
    public Task<User?> GetUser(Guid id, CancellationToken ct) => Read(s => s.Users.Find(u => u.Id == id), ct);
    public Task<IReadOnlyList<User>> Users(CancellationToken ct) => Read<IReadOnlyList<User>>(s => s.Users.OrderBy(u => u.Email).ToArray(), ct);
    public Task AddUser(User user, CancellationToken ct) => Write(s =>
    {
        if (s.Users.Any(u => u.Email == user.Email)) throw new DomainException("email_exists", "לא ניתן ליצור חשבון עם הפרטים האלה.", 409);
        s.Users.Add(user);
        return true;
    }, ct);
    public Task SetUserEnabled(Guid id, bool enabled, Guid actor, CancellationToken ct) => Write(s =>
    {
        var index = s.Users.FindIndex(u => u.Id == id);
        if (index < 0) throw new DomainException("not_found", "המשתמש אינו קיים.", 404);
        if (s.Users[index].Role == "Admin") throw new DomainException("protected_admin", "אין להשבית מנהל דרך ממשק זה.");
        s.Users[index] = s.Users[index] with { Enabled = enabled };
        if (!enabled) s.Sessions.RemoveAll(x => x.UserId == id);
        return Log(s, actor, enabled ? "user.enabled" : "user.disabled", id.ToString());
    }, ct);
    public Task SaveSession(Session session, CancellationToken ct) => Write(s => { s.Sessions.Add(session); return true; }, ct);
    public Task<Session?> GetSession(string hash, CancellationToken ct) => Read(s => s.Sessions.Find(x => x.TokenHash == hash && x.ExpiresAt > DateTimeOffset.UtcNow), ct);
    public Task DeleteSession(string hash, CancellationToken ct) => Write(s => s.Sessions.RemoveAll(x => x.TokenHash == hash), ct);
    public Task<IReadOnlyList<Segment>> Segments(CancellationToken ct) => Read<IReadOnlyList<Segment>>(s => s.Segments.OrderBy(x => x.Name).ToArray(), ct);
    public Task SaveSegment(Segment segment, Guid actor, CancellationToken ct) => Write(s =>
    {
        var index = s.Segments.FindIndex(x => x.Id == segment.Id);
        if (index < 0)
        {
            if (segment.Version != 0) throw new DomainException("not_found", "המקטע אינו קיים.", 404);
            s.Segments.Add(segment with { Version = 1 });
        }
        else
        {
            if (s.Segments[index].Version != segment.Version) throw new DomainException("conflict", "המקטע השתנה. רעננו ונסו שוב.", 409);
            s.Segments[index] = segment with { Version = segment.Version + 1 };
        }
        return Log(s, actor, "segment.saved", segment.Id.ToString());
    }, ct);
    public Task<IReadOnlyList<Reading>> Readings(Guid? id, DateTimeOffset from, DateTimeOffset to, int limit, CancellationToken ct) =>
        Read<IReadOnlyList<Reading>>(s => s.Readings.Where(r => (id == null || r.SegmentId == id) && r.ObservedAt >= from && r.ObservedAt <= to).OrderByDescending(r => r.ObservedAt).Take(limit).ToArray(), ct);
    public Task<IReadOnlyList<Reading>> LatestReadings(CancellationToken ct) => Read<IReadOnlyList<Reading>>(s => s.Readings.GroupBy(r => r.SegmentId).Select(g => g.MaxBy(r => r.ObservedAt)!).ToArray(), ct);
    public Task<bool> Ingest(Reading reading, Rules rules, CancellationToken ct) => Write(s =>
    {
        Validation.Reading(reading, DateTimeOffset.UtcNow);
        if (s.Readings.Any(r => r.Id == reading.Id)) return false;
        if (!s.Segments.Any(x => x.Id == reading.SegmentId)) throw new DomainException("unknown_segment", "מקטע לא מוכר.");
        var detection = AnomalyDetector.Analyze(reading, s.Readings, rules);
        s.Readings.Add(reading);
        if (detection.Reason is not null)
            s.Anomalies.Add(new(Guid.NewGuid(), reading.Id, reading.SegmentId, reading.ObservedAt,
                detection.Reason, detection.Average, detection.Deviation, detection.Samples, "Open"));
        return true;
    }, ct);
    public Task<IReadOnlyList<Anomaly>> Anomalies(Guid? id, string? status, int limit, CancellationToken ct) => Read<IReadOnlyList<Anomaly>>(s => s.Anomalies.Where(a => (id == null || a.SegmentId == id) && (status == null || a.Status == status)).OrderByDescending(a => a.CreatedAt).Take(limit).ToArray(), ct);
    public Task SetAnomalyStatus(Guid id, string status, int version, Guid actor, CancellationToken ct) => Write(s =>
    {
        if (status is not ("Open" or "Acknowledged" or "Resolved")) throw new DomainException("invalid_status", "מצב לא תקין.");
        var index = s.Anomalies.FindIndex(x => x.Id == id);
        if (index < 0) throw new DomainException("not_found", "האירוע אינו קיים.", 404);
        if (s.Anomalies[index].Version != version) throw new DomainException("conflict", "האירוע עודכן. רעננו ונסו שוב.", 409);
        s.Anomalies[index] = s.Anomalies[index] with { Status = status, Version = version + 1 };
        return Log(s, actor, "anomaly." + status, id.ToString());
    }, ct);
    public Task<IReadOnlyList<Guid>> Favorites(Guid id, CancellationToken ct) => Read<IReadOnlyList<Guid>>(s => s.Favorites.Where(f => f.UserId == id).Select(f => f.SegmentId).ToArray(), ct);
    public Task SetFavorite(Guid userId, Guid segmentId, bool enabled, CancellationToken ct) => Write(s =>
    {
        if (!s.Segments.Any(x => x.Id == segmentId)) throw new DomainException("not_found", "המקטע אינו קיים.", 404);
        s.Favorites.RemoveAll(f => f.UserId == userId && f.SegmentId == segmentId);
        if (enabled) s.Favorites.Add(new(userId, segmentId));
        return true;
    }, ct);
    public Task<IReadOnlyList<Audit>> Audits(CancellationToken ct) => Read<IReadOnlyList<Audit>>(s => s.Audits.OrderByDescending(a => a.At).Take(100).ToArray(), ct);
}
