using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace backend.api.Services;

public sealed class RedisCacheService : IDisposable
{
    private readonly CacheOptions options;
    private readonly ILogger<RedisCacheService> logger;
    private readonly Lazy<ConnectionMultiplexer> connection;

    public RedisCacheService(
        IOptions<CacheOptions> options,
        ILogger<RedisCacheService> logger)
    {
        this.options = options.Value;
        this.logger = logger;
        connection = new Lazy<ConnectionMultiplexer>(CreateConnection);
    }

    private ConnectionMultiplexer CreateConnection()
    {
        var configuration = ConfigurationOptions.Parse(options.ConnectionString);
        configuration.AbortOnConnectFail = false;
        configuration.ConnectTimeout = 10000;
        configuration.SyncTimeout = 10000;
        return ConnectionMultiplexer.Connect(configuration);
    }

    private ConnectionMultiplexer Connection => connection.Value;

    public void Dispose()
    {
        if (connection.IsValueCreated)
        {
            Connection.Dispose();
        }
    }

    // --- Connected players (SET) ---

    public async Task AddOnlinePlayerAsync(Guid playerId)
    {
        var database = Connection.GetDatabase();
        var key = new RedisKey(options.OnlinePlayersKey);
        await database.SetAddAsync(key, playerId.ToString());
        await database.KeyExpireAsync(key, TimeSpan.FromSeconds(options.OnlineTtlSeconds));
    }

    public async Task RemoveOnlinePlayerAsync(Guid playerId)
    {
        var database = Connection.GetDatabase();
        await database.SetRemoveAsync(options.OnlinePlayersKey, playerId.ToString());
    }

    public async Task<IReadOnlyList<string>> GetOnlinePlayersAsync()
    {
        var database = Connection.GetDatabase();
        var members = await database.SetMembersAsync(options.OnlinePlayersKey);
        return members.Select(member => member.ToString()).ToArray();
    }

    public async Task<long> GetOnlinePlayerCountAsync()
    {
        var database = Connection.GetDatabase();
        return await database.SetLengthAsync(options.OnlinePlayersKey);
    }

    // --- Global leaderboard (Sorted Set: score -> profileId) ---

    public async Task AddScoreAsync(Guid profileId, int score)
    {
        var database = Connection.GetDatabase();
        await database.SortedSetIncrementAsync(options.LeaderboardKey, profileId.ToString(), score);
    }

    public async Task<IReadOnlyList<(string Member, double Score)>> GetTopScoresAsync(int count = 10)
    {
        var database = Connection.GetDatabase();
        var entries = await database.SortedSetRangeByRankWithScoresAsync(
            options.LeaderboardKey,
            start: 0,
            stop: count - 1,
            order: Order.Descending);

        return entries.Select(entry => (entry.Element.ToString(), entry.Score)).ToArray();
    }

    public async Task<long> GetLeaderboardSizeAsync()
    {
        var database = Connection.GetDatabase();
        return await database.SortedSetLengthAsync(options.LeaderboardKey);
    }

    public async Task ClearLeaderboardAsync()
    {
        var database = Connection.GetDatabase();
        await database.KeyDeleteAsync(options.LeaderboardKey);
    }

    // --- JWT sessions (String with TTL) ---

    public async Task StoreSessionAsync(Guid userId, string token, TimeSpan? ttl = null)
    {
        var database = Connection.GetDatabase();
        var key = new RedisKey(options.SessionPrefix + userId);
        await database.StringSetAsync(key, token, ttl ?? TimeSpan.FromSeconds(options.SessionTtlSeconds));
    }

    public async Task<bool> ValidateSessionAsync(Guid userId, string expectedToken)
    {
        var database = Connection.GetDatabase();
        var stored = await database.StringGetAsync(options.SessionPrefix + userId);
        if (!stored.HasValue)
        {
            return false;
        }

        return stored.ToString() == expectedToken;
    }

    public async Task RemoveSessionAsync(Guid userId)
    {
        var database = Connection.GetDatabase();
        await database.KeyDeleteAsync(options.SessionPrefix + userId);
    }

    public async Task<long> GetActiveSessionCountAsync()
    {
        var server = Connection.GetServer(Connection.GetEndPoints().First());
        var keys = await server.KeysAsync(pattern: options.SessionPrefix + "*", pageSize: 100).ToListAsync();
        return keys.Count;
    }
}