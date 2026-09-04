namespace backend.api.Hubs.Systems;

public sealed class MqttOptions
{
    public const string SectionName = "Mqtt";

    public string Host { get; set; } = string.Empty;

    public int Port { get; set; } = 8884;

    public string Username { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public string ClientId { get; set; } = "tankx-local";

    public string Topic { get; set; } = "tankx/telemetry";
}
