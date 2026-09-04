namespace backend.api.Hubs.Systems;

public sealed class HistoryOptions
{
    public const string SectionName = "History";

    public string ConnectionString { get; set; } = string.Empty;

    public string Key { get; set; } = "tankx:events";

    public int MaxEntries { get; set; } = 100;

    public int TtlSeconds { get; set; } = 86400;
}
