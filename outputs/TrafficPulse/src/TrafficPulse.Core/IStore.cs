namespace TrafficPulse.Core;

public interface IStore
{
    Task Initialize(CancellationToken ct);
    Task<User?> FindUser(string email, CancellationToken ct);
    Task<User?> GetUser(Guid id, CancellationToken ct);
    Task<IReadOnlyList<User>> Users(CancellationToken ct);
    Task AddUser(User user, CancellationToken ct);
    Task SetUserEnabled(Guid id, bool enabled, Guid actor, CancellationToken ct);
    Task SaveSession(Session session, CancellationToken ct);
    Task<Session?> GetSession(string tokenHash, CancellationToken ct);
    Task DeleteSession(string tokenHash, CancellationToken ct);
    Task<IReadOnlyList<Segment>> Segments(CancellationToken ct);
    Task SaveSegment(Segment segment, Guid actor, CancellationToken ct);
    Task<IReadOnlyList<Reading>> Readings(Guid? segmentId, DateTimeOffset from, DateTimeOffset to, int limit, CancellationToken ct);
    Task<IReadOnlyList<Reading>> LatestReadings(CancellationToken ct);
    Task<bool> Ingest(Reading reading, Rules rules, CancellationToken ct);
    Task<IReadOnlyList<Anomaly>> Anomalies(Guid? segmentId, string? status, int limit, CancellationToken ct);
    Task SetAnomalyStatus(Guid id, string status, int version, Guid actor, CancellationToken ct);
    Task<IReadOnlyList<Guid>> Favorites(Guid userId, CancellationToken ct);
    Task SetFavorite(Guid userId, Guid segmentId, bool enabled, CancellationToken ct);
    Task<IReadOnlyList<Audit>> Audits(CancellationToken ct);
}
