namespace TrafficShared.Models;

public class AnalysisSettings
{
    public int MinimumSamples { get; set; } = 10;
    public int HistoryDays { get; set; } = 28;
    public int WindowMinutes { get; set; } = 60;
    public double MinimumConfidence { get; set; } = 0.6;
    public double MinimumDropKmh { get; set; } = 10;
    public string TimeZone { get; set; } = "Asia/Jerusalem";
}
