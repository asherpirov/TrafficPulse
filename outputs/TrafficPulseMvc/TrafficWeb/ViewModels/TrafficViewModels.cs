using System.ComponentModel.DataAnnotations;
using TrafficShared.Models;
namespace TrafficWeb.ViewModels;

public class DashboardViewModel
{
    public List<Road> Roads { get; set; } = [];
    public List<TrafficReading> Readings { get; set; } = [];
    public List<Guid> Favorites { get; set; } = [];
    public int OpenAlerts { get; set; }
    public bool FavoritesOnly { get; set; }
    public Road? MapRoad { get; set; }
    public bool MapConfigured { get; set; }
}
public class HistoryViewModel
{
    public Road Road { get; set; } = new();
    public List<TrafficReading> Readings { get; set; } = [];
    public DateTime From { get; set; }
    public DateTime To { get; set; }
}
public class AlertsViewModel
{
    public List<TrafficAlert> Alerts { get; set; } = [];
    public List<Road> Roads { get; set; } = [];
    public string? Status { get; set; }
    public Guid? RoadId { get; set; }
}
public class RoadEditViewModel
{
    public Guid Id { get; set; }
    [Required, StringLength(80, MinimumLength = 2)] public string Name { get; set; } = "";
    [Range(-90, 90)] public double Latitude { get; set; }
    [Range(-180, 180)] public double Longitude { get; set; }
    [Range(1, 6)] public double SigmaThreshold { get; set; } = 3;
    public bool IsActive { get; set; } = true;
    public int Version { get; set; }
}
