using System.Text.Json.Serialization;
namespace TrafficShared.Models;

public class TrafficReading
{
    // אותו מזהה נשמר בכל ניסיון חוזר כדי למנוע שמירה כפולה.
    [JsonRequired] public Guid Id { get; set; } = Guid.NewGuid();
    [JsonRequired] public Guid RoadId { get; set; }
    [JsonRequired] public DateTime CollectedAtUtc { get; set; } = DateTime.UtcNow;
    [JsonRequired] public double CurrentSpeed { get; set; }
    [JsonRequired] public double FreeFlowSpeed { get; set; }
    [JsonRequired] public double Confidence { get; set; }
    [JsonRequired] public bool RoadClosed { get; set; }
    [JsonRequired] public string Source { get; set; } = "TomTom";
}
