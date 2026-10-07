using TrafficShared.Models;
namespace TrafficShared.Services;

// כל החישוב כאן. אין כאן SQL, HTTP או Kafka ולכן קל לבדוק את המחלקה.
public class AnomalyService
{
    private readonly AnalysisSettings _settings;
    private readonly TimeZoneInfo _timeZone;
    public int HistoryDays => _settings.HistoryDays;
    public AnomalyService(AnalysisSettings settings)
    {
        if (settings.MinimumSamples < 2 || settings.HistoryDays is < 7 or > 90 || settings.WindowMinutes is < 1 or > 180 ||
            !double.IsFinite(settings.MinimumConfidence) || settings.MinimumConfidence is < 0 or > 1 ||
            !double.IsFinite(settings.MinimumDropKmh) || settings.MinimumDropKmh <= 0)
            throw new InvalidOperationException("Invalid Analysis configuration.");
        _settings = settings;
        _timeZone = TimeZoneInfo.FindSystemTimeZoneById(settings.TimeZone);
    }

    public TrafficAlert? Analyze(TrafficReading current, IEnumerable<TrafficReading> history, double sigma)
    {
        if (current.Confidence < _settings.MinimumConfidence) return null;
        if (current.RoadClosed) return CreateAlert(current, "הספק מדווח על סגירת כביש");

        DateTime localTime = TimeZoneInfo.ConvertTimeFromUtc(current.CollectedAtUtc, _timeZone);
        var speeds = new List<double>();
        foreach (TrafficReading past in history)
        {
            if (past.RoadId != current.RoadId || past.Source != current.Source || past.RoadClosed || past.Confidence < _settings.MinimumConfidence ||
                past.CollectedAtUtc >= current.CollectedAtUtc || past.CollectedAtUtc < current.CollectedAtUtc.AddDays(-_settings.HistoryDays)) continue;
            DateTime pastLocal = TimeZoneInfo.ConvertTimeFromUtc(past.CollectedAtUtc, _timeZone);
            double minutes = Math.Abs((pastLocal.TimeOfDay - localTime.TimeOfDay).TotalMinutes);
            // משווים לאותו יום בשבוע ולחלון של שעה, ורק לימים קודמים.
            if (pastLocal.Date < localTime.Date && pastLocal.DayOfWeek == localTime.DayOfWeek && Math.Min(minutes, 1440 - minutes) <= _settings.WindowMinutes)
                speeds.Add(past.CurrentSpeed);
        }
        if (speeds.Count < _settings.MinimumSamples) return null;

        double average = speeds.Average();
        double variance = speeds.Sum(speed => Math.Pow(speed - average, 2)) / (speeds.Count - 1);
        double deviation = Math.Sqrt(variance);
        // ירידה מינימלית של 10 קמ״ש מטפלת גם בסטיית תקן אפסית.
        double requiredDrop = Math.Max(_settings.MinimumDropKmh, sigma * deviation);
        if (current.CurrentSpeed >= average - requiredDrop) return null;

        TrafficAlert alert = CreateAlert(current, $"ירידה של {average - current.CurrentSpeed:F1} קמ״ש; סף החריגה {requiredDrop:F1} קמ״ש");
        alert.HistoricalAverage = average;
        alert.StandardDeviation = deviation;
        alert.SampleCount = speeds.Count;
        return alert;
    }

    private static TrafficAlert CreateAlert(TrafficReading reading, string reason)
    {
        return new TrafficAlert { ReadingId = reading.Id, RoadId = reading.RoadId, CreatedAtUtc = reading.CollectedAtUtc, Reason = reason };
    }
}
