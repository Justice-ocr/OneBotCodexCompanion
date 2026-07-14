namespace OneBotCodexCompanion;

public sealed class Recipient
{
    public string TargetType { get; set; } = "private";
    public string TargetId { get; set; } = "";
}

public sealed class AppSettings
{
    public string BaseUrl { get; set; } = "http://59.110.13.83:3000";
    public string EncryptedToken { get; set; } = "";
    public string MessageFormat { get; set; } = "array";
    public Recipient DefaultRecipient { get; set; } = new();
    public Dictionary<string, Recipient> ThreadRoutes { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public bool MonitorEnabled { get; set; }
    public string CdpEndpoint { get; set; } = "http://127.0.0.1:9229";
}
