using TrafficShared.Models;
namespace TrafficShared.Services;

public static class TrafficValidator
{
    public static void Validate(TrafficReading reading)
    {
        if (reading.Id == Guid.Empty || reading.RoadId == Guid.Empty ||
            !double.IsFinite(reading.CurrentSpeed) || reading.CurrentSpeed is < 0 or > 300 ||
            !double.IsFinite(reading.FreeFlowSpeed) || reading.FreeFlowSpeed is <= 0 or > 300 ||
            !double.IsFinite(reading.Confidence) || reading.Confidence is < 0 or > 1 ||
            reading.CollectedAtUtc.Kind != DateTimeKind.Utc ||
            reading.CollectedAtUtc > DateTime.UtcNow.AddMinutes(2) || reading.CollectedAtUtc < DateTime.UtcNow.AddDays(-90) ||
            reading.Source is not ("Demo" or "TomTom"))
            throw new BusinessException("המדידה אינה תקינה: בדקו מזהים, מהירות, תאריך ומקור.");
    }

    public static void Validate(Road road)
    {
        road.Name = road.Name.Trim();
        if (road.Id == Guid.Empty || road.Name.Length is < 2 or > 80 || road.Name.Any(char.IsControl) ||
            !double.IsFinite(road.Latitude) || Math.Abs(road.Latitude) > 90 ||
            !double.IsFinite(road.Longitude) || Math.Abs(road.Longitude) > 180 ||
            !double.IsFinite(road.SigmaThreshold) || road.SigmaThreshold is < 1 or > 6)
            throw new BusinessException("פרטי המקטע אינם תקינים.");
    }

    public static void ValidateStatus(string status)
    {
        if (status is not ("Open" or "Acknowledged" or "Resolved"))
            throw new BusinessException("מצב ההתרעה אינו תקין.");
    }
}
