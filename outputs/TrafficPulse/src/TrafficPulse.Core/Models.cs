namespace TrafficPulse.Core;

public sealed record User(Guid Id, string Email, string Name, string PasswordHash, string Role, bool Enabled);
public sealed record Segment(Guid Id, string Name, double Latitude, double Longitude, bool Enabled, int Version = 1);
public sealed record Reading(Guid Id, Guid SegmentId, DateTimeOffset ObservedAt, double Speed,
    double FreeFlowSpeed, double Confidence, bool RoadClosed, string Source);
public sealed record Anomaly(Guid Id, Guid ReadingId, Guid SegmentId, DateTimeOffset CreatedAt,
    string Reason, double? Average, double? Deviation, int Samples, string Status, int Version = 1);
public sealed record Audit(Guid Id, Guid ActorId, string Action, string Target, DateTimeOffset At);
public sealed record Session(string TokenHash, Guid UserId, string CsrfToken, DateTimeOffset ExpiresAt);
public sealed record Favorite(Guid UserId, Guid SegmentId);
public sealed record Detection(string? Reason, double? Average, double? Deviation, int Samples);
public sealed record SegmentSummary(Segment Segment, Reading? Latest, bool Stale, bool Favorite);
public sealed record PublicUser(Guid Id, string Email, string Name, string Role, bool Enabled)
{
    public static PublicUser From(User user) => new(user.Id, user.Email, user.Name, user.Role, user.Enabled);
}
public sealed record Rules(int MinimumSamples = 10, double Sigma = 3, double MinimumDrop = 10,
    double MinimumConfidence = 0.6, int WindowMinutes = 60, int HistoryDays = 28);

public sealed class DomainException(string code, string message, int status = 400) : Exception(message)
{
    public string Code { get; } = code;
    public int Status { get; } = status;
}
