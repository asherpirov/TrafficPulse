namespace TrafficShared.Models;
public class KafkaSettings
{
    public string BootstrapServers { get; set; } = "";
    public string Topic { get; set; } = "traffic-readings";
    public string DeadLetterTopic { get; set; } = "traffic-readings-invalid";
    public string GroupId { get; set; } = "traffic-processor";
}
