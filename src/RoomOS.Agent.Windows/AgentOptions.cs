namespace RoomOS.Agent.Windows;

public sealed class AgentOptions
{
    public const string SectionName = "Core";

    public string Url { get; set; } = "http://192.168.1.30:8080";
    public string AgentToken { get; set; } = string.Empty;
    public string PcId { get; set; } = "gaming-pc";
}

public sealed class TelemetryOptions
{
    public const string SectionName = "Telemetry";

    public int IntervalMs { get; set; } = 2000;
}
