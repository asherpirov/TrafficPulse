namespace TrafficShared.Models;

public class Road
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public bool IsActive { get; set; } = true;
    public double SigmaThreshold { get; set; } = 3;
    public int Version { get; set; }
}
