namespace TrafficShared.Models;

public class TrafficAlert
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ReadingId { get; set; }
    public Guid RoadId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public string Reason { get; set; } = "";
    public double? HistoricalAverage { get; set; }
    public double? StandardDeviation { get; set; }
    public int SampleCount { get; set; }
    public string Status { get; set; } = "Open";
    public int Version { get; set; } = 1;
    public Guid? UpdatedBy { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
}
