namespace backend.api.Services;

public sealed class CacheOptions
{
    public const string SectionName = "Cache";

    public string ConnectionString { get; set; } = string.Empty;

    public string OnlinePlayersKey { get; set; } = "tankx:online";

    public string LeaderboardKey { get; set; } = "tankx:leaderboard";

    public string SessionPrefix { get; set; } = "tankx:session:";

    public int SessionTtlSeconds { get; set; } = 3600;

    public int OnlineTtlSeconds { get; set; } = 300;
}