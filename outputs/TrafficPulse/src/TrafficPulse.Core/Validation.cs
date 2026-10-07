using System.Net.Mail;

namespace TrafficPulse.Core;

public static class Validation
{
    public static string Email(string? value)
    {
        var email = (value ?? "").Trim().ToLowerInvariant();
        if (email.Length > 254 || !MailAddress.TryCreate(email, out var parsed) || parsed.Address != email)
            throw new DomainException("invalid_email", "כתובת הדואר אינה תקינה.");
        return email;
    }

    public static string Name(string? value, int maximum = 80)
    {
        var name = (value ?? "").Trim();
        if (name.Length < 2 || name.Length > maximum || name.Any(char.IsControl))
            throw new DomainException("invalid_name", $"השם חייב להכיל 2–{maximum} תווים ללא תווי בקרה.");
        return name;
    }

    public static void Password(string? value)
    {
        if (value is null || value.Length < 12 || value.Length > 128 || !value.Any(char.IsLetter) || !value.Any(char.IsDigit))
            throw new DomainException("weak_password", "הסיסמה חייבת להכיל 12–128 תווים, כולל אות ומספר.");
    }

    public static void Coordinates(double latitude, double longitude)
    {
        if (!double.IsFinite(latitude) || !double.IsFinite(longitude) || Math.Abs(latitude) > 90 || Math.Abs(longitude) > 180)
            throw new DomainException("invalid_coordinates", "קואורדינטות אינן תקינות.");
    }

    public static void Reading(Reading reading, DateTimeOffset now)
    {
        if (reading.Id == Guid.Empty || reading.SegmentId == Guid.Empty ||
            !double.IsFinite(reading.Speed) || reading.Speed is < 0 or > 300 ||
            !double.IsFinite(reading.FreeFlowSpeed) || reading.FreeFlowSpeed is <= 0 or > 300 ||
            !double.IsFinite(reading.Confidence) || reading.Confidence is < 0 or > 1 ||
            reading.ObservedAt > now.AddMinutes(2) || reading.ObservedAt < now.AddDays(-90) ||
            reading.Source is not ("TomTom" or "Demo"))
            throw new DomainException("invalid_reading", "מדידת התנועה אינה תקינה.");
    }
}
