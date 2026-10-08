namespace TrafficProducer.Services;
public class ProducerSettings
{
    public string Provider { get; set; } = "TomTom";
    public int PollSeconds { get; set; } = 60;
}
