namespace TrafficPulse.Core;

/// <summary>Pure calculation: no database, HTTP, clock or Kafka dependencies.</summary>
public static class AnomalyDetector
{
    private static readonly TimeZoneInfo LocalZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Jerusalem");

    public static Detection Analyze(Reading current, IEnumerable<Reading> history, Rules rules)
    {
        if (current.Confidence < rules.MinimumConfidence)
            return new(null, null, null, 0);
        if (current.RoadClosed)
            return new("הספק מדווח על סגירת כביש", null, null, 0);

        var currentLocal = TimeZoneInfo.ConvertTime(current.ObservedAt, LocalZone);
        var samples = history.Where(r => r.SegmentId == current.SegmentId && r.Source == current.Source &&
            r.ObservedAt < current.ObservedAt && r.ObservedAt >= current.ObservedAt.AddDays(-rules.HistoryDays) &&
            r.Confidence >= rules.MinimumConfidence && !r.RoadClosed)
            .Where(r =>
            {
                var local = TimeZoneInfo.ConvertTime(r.ObservedAt, LocalZone);
                var difference = Math.Abs((local.TimeOfDay - currentLocal.TimeOfDay).TotalMinutes);
                return local.Date < currentLocal.Date && local.DayOfWeek == currentLocal.DayOfWeek &&
                    Math.Min(difference, 1440 - difference) <= rules.WindowMinutes;
            }).Select(r => r.Speed).ToArray();

        if (samples.Length < rules.MinimumSamples)
            return new(null, null, null, samples.Length);

        var average = samples.Average();
        var deviation = Math.Sqrt(samples.Sum(x => Math.Pow(x - average, 2)) / (samples.Length - 1));
        // The absolute drop floor handles zero/tiny variance without division by zero.
        var drop = Math.Max(rules.MinimumDrop, rules.Sigma * deviation);
        var reason = current.Speed < average - drop
            ? $"המהירות ירדה ב־{average - current.Speed:F1} קמ״ש ביחס לשעות דומות; סף הירידה הוא {drop:F1} קמ״ש"
            : null;
        return new(reason, average, deviation, samples.Length);
    }
}
