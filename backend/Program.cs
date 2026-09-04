using AspNetCoreRateLimit;
using backend.api.Extensions;
using backend.api.Hubs;
using backend.api.Hubs.Runtime;
using backend.api.Hubs.Systems;
using backend.api.Services;
using backend.persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using StackExchange.Profiling;
using StackExchange.Profiling.Storage;
using System.Text.Json;
using System.Text.Json.Serialization;

DotNetEnv.Env.Load();

var builder = WebApplication.CreateBuilder(args);
var supabaseUrl = builder.Configuration["Supabase:Url"]
    ?? throw new InvalidOperationException("Supabase:Url configuration is required.");

builder.Services.AddControllers().AddJsonOptions(options =>
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)));
builder.Services.AddOpenApi();
builder.Services.AddSignalR(options => options.MaximumReceiveMessageSize = 16 * 1024)
    .AddJsonProtocol(options =>
        options.PayloadSerializerOptions.Converters.Add(
            new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)));
builder.Services.AddSingleton<GameSessionManager>();
builder.Services.AddScoped<MatchPersistenceService>();
builder.Services.AddSwaggerDocumentation();
builder.Services.AddDbContext<TankxContext>((serviceProvider, options) =>
{
    var configuration = serviceProvider.GetRequiredService<IConfiguration>();
    var connectionString = configuration.GetConnectionString("ConexDb");

    if (string.IsNullOrWhiteSpace(connectionString))
    {
        throw new InvalidOperationException(
            "ConnectionStrings:ConexDb is required for database-backed endpoints.");
    }

    var npgsql = new NpgsqlConnectionStringBuilder(connectionString)
    {
        Pooling = true,
        MaxPoolSize = configuration.GetValue("Database:MaximumPoolSize", 100),
        ConnectionIdleLifetime = configuration.GetValue("Database:ConnectionIdleLifetime", 300),
        ConnectionPruningInterval = 30
    };

    options.UseNpgsql(npgsql.ConnectionString);
});

builder.Services.Configure<HistoryOptions>(history =>
{
    var redisUrl = builder.Configuration["UPSTASH_REDIS_REST_URL"]
        ?? throw new InvalidOperationException("UPSTASH_REDIS_REST_URL configuration is required.");
    var redisToken = builder.Configuration["UPSTASH_REDIS_REST_TOKEN"]
        ?? throw new InvalidOperationException("UPSTASH_REDIS_REST_TOKEN configuration is required.");

    var host = new Uri(redisUrl).Host;
    history.ConnectionString = $"{host}:6379,password={redisToken},ssl=true";
    history.Key = builder.Configuration["HISTORY_KEY"] ?? "tankx:events";
    history.MaxEntries = builder.Configuration.GetValue("HISTORY_MAX_ENTRIES", 100);
    history.TtlSeconds = builder.Configuration.GetValue("HISTORY_TTL_SECONDS", 86400);
});
builder.Services.Configure<MqttOptions>(mqtt =>
{
    mqtt.Host = builder.Configuration["MQTT_HOST"]
        ?? throw new InvalidOperationException("MQTT_HOST configuration is required.");
    mqtt.Port = builder.Configuration.GetValue("MQTT_PORT", 8884);
    mqtt.Username = builder.Configuration["MQTT_USERNAME"] ?? builder.Configuration["USER"] ?? string.Empty;
    mqtt.Password = builder.Configuration["MQTT_PASSWORD"] ?? builder.Configuration["PASSWORD"] ?? string.Empty;
    mqtt.ClientId = builder.Configuration["MQTT_CLIENT_ID"] ?? builder.Configuration["CLIENT_ID"] ?? "tankx-local";
    mqtt.Topic = builder.Configuration["MQTT_TOPIC"] ?? builder.Configuration["TOPIC_TANKX"] ?? "tankx/telemetry";
});
builder.Services.AddSingleton<MqttEventPublisher>();
builder.Services.AddSingleton<IGameEventPublisher>(sp => sp.GetRequiredService<MqttEventPublisher>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<MqttEventPublisher>());
builder.Services.AddSingleton<RedisEventHistoryStore>();
builder.Services.AddSingleton<IEventHistoryStore>(sp => sp.GetRequiredService<RedisEventHistoryStore>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<RedisEventHistoryStore>());
builder.Services.Configure<CacheOptions>(cache =>
{
    var redisUrl = builder.Configuration["UPSTASH_REDIS_REST_URL"]
        ?? throw new InvalidOperationException("UPSTASH_REDIS_REST_URL configuration is required.");
    var redisToken = builder.Configuration["UPSTASH_REDIS_REST_TOKEN"]
        ?? throw new InvalidOperationException("UPSTASH_REDIS_REST_TOKEN configuration is required.");

    var host = new Uri(redisUrl).Host;
    cache.ConnectionString = $"{host}:6379,password={redisToken},ssl=true";
    cache.OnlinePlayersKey = builder.Configuration["CACHE_ONLINE_KEY"] ?? "tankx:online";
    cache.LeaderboardKey = builder.Configuration["CACHE_LEADERBOARD_KEY"] ?? "tankx:leaderboard";
    cache.SessionPrefix = builder.Configuration["CACHE_SESSION_PREFIX"] ?? "tankx:session:";
    cache.SessionTtlSeconds = builder.Configuration.GetValue("CACHE_SESSION_TTL_SECONDS", 3600);
});
builder.Services.AddSingleton<RedisCacheService>();

builder.Services.ConfigureSupabaseAuth(supabaseUrl);
builder.Services.AddAplicacionServices();
builder.Services.ConfigureCors();
builder.Services.ConfigurationRatelimiting();

builder.Services.AddMiniProfiler(options =>
{
    options.RouteBasePath = "/profiler";
}).AddEntityFramework();

var app = builder.Build();

app.UseSwaggerDocumentation();

app.UseHttpsRedirection();
app.UseCors("AngularClient");
app.UseMiniProfiler();
app.UseIpRateLimiting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapGet("/api/auth/me", (HttpContext context) => new
{
    UserId = context.User.FindFirst("sub")?.Value,
    Email = context.User.FindFirst("email")?.Value
}).RequireAuthorization();
app.MapHub<GameHub>("/hubs/game");

app.Run();
