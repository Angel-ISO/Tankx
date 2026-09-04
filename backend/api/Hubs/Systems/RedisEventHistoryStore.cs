using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace backend.api.Hubs.Systems;


public sealed class RedisEventHistoryStore : IEventHistoryStore, IHostedService, IDisposable
{
    private readonly HistoryOptions options;
    private readonly ILogger<RedisEventHistoryStore> logger;
    private readonly Lazy<ConnectionMultiplexer> connection;

    public RedisEventHistoryStore(
        IOptions<HistoryOptions> options,
        ILogger<RedisEventHistoryStore> logger)
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

    public Task StartAsync(CancellationToken cancellationToken)
    {
        
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        if (connection.IsValueCreated)
        {
            return Connection.CloseAsync();
        }

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        if (connection.IsValueCreated)
        {
            Connection.Dispose();
        }
    }

    public async Task AppendAsync(string payloadJson, CancellationToken cancellationToken = default)
    {
        var database = Connection.GetDatabase();
        await database.ListRightPushAsync(options.Key, payloadJson);
      
        await database.ListTrimAsync(options.Key, -options.MaxEntries, -1);
        await database.KeyExpireAsync(options.Key, TimeSpan.FromSeconds(options.TtlSeconds));
    }

    public async Task<IReadOnlyList<string>> GetRecentAsync(int count, CancellationToken cancellationToken = default)
    {
        var database = Connection.GetDatabase();
        var take = Math.Min(count, options.MaxEntries);
        var range = await database.ListRangeAsync(options.Key, -take, -1);

        var events = new List<string>(range.Length);
        foreach (var value in range)
        {
            events.Add(value.ToString());
        }

        return events;
    }
}
