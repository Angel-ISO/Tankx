using System.Text;
using System.Text.Json;
using backend.api.Hubs.Events;
using Microsoft.Extensions.Options;
using MQTTnet;
using MQTTnet.Protocol;

namespace backend.api.Hubs.Systems;

public sealed class MqttEventPublisher : IGameEventPublisher, IHostedService, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly MqttOptions options;
    private readonly ILogger<MqttEventPublisher> logger;
    private readonly IEventHistoryStore historyStore;
    private readonly MqttClientFactory factory = new();
    private IMqttClient? client;
    private CancellationTokenSource? reconnectCts;

    public MqttEventPublisher(
        IOptions<MqttOptions> options,
        ILogger<MqttEventPublisher> logger,
        IEventHistoryStore historyStore)
    {
        this.options = options.Value;
        this.logger = logger;
        this.historyStore = historyStore;
    }

    public bool IsConnected => client?.IsConnected == true;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        reconnectCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        client = factory.CreateMqttClient();
        _ = Task.Run(() => ConnectionLoopAsync(reconnectCts.Token), cancellationToken);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        reconnectCts?.Cancel();
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        reconnectCts?.Dispose();
        client?.Dispose();
    }

    public async Task PublishCollisionAsync(CollisionEvent collision, CancellationToken cancellationToken = default)
    {
        if (client is null || !client.IsConnected)
        {
            return;
        }

        var payload = JsonSerializer.Serialize(
            new
            {
                type = "collision",
                playerId = collision.PlayerId,
                playerName = collision.PlayerName,
                targetId = collision.TargetId,
                targetName = collision.TargetName,
                damage = collision.Damage,
                sentAt = collision.SentAt,
            },
            JsonOptions
        );

        var message = new MqttApplicationMessageBuilder()
            .WithTopic(options.Topic)
            .WithPayload(Encoding.UTF8.GetBytes(payload))
            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
            .Build();

        await client.PublishAsync(message, cancellationToken);
        await PersistHistoryAsync(payload);
    }

    public async Task PublishGameOverAsync(GameOverEvent gameOver, CancellationToken cancellationToken = default)
    {
        if (client is null || !client.IsConnected)
        {
            return;
        }

        var payload = JsonSerializer.Serialize(
            new
            {
                type = "gameOver",
                playerId = gameOver.PlayerId,
                playerName = gameOver.PlayerName,
                targetId = gameOver.TargetId,
                targetName = gameOver.TargetName,
                sentAt = gameOver.SentAt,
            },
            JsonOptions
        );

        // Los eventos críticos (finalización de partida) usan QoS 1 (al menos una vez):
        // la notificación no puede perderse, aunque se tolere un duplicado puntual.
        var message = new MqttApplicationMessageBuilder()
            .WithTopic(options.Topic)
            .WithPayload(Encoding.UTF8.GetBytes(payload))
            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
            .Build();

        await client.PublishAsync(message, cancellationToken);
        await PersistHistoryAsync(payload);
    }

    private async Task PersistHistoryAsync(string payload)
    {
        try
        {
            await historyStore.AppendAsync(payload);
        }
        catch (Exception ex)
        {
            
            logger.LogWarning("Could not persist event to history store: {Message}", ex.Message);
        }
    }

    private async Task ConnectionLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                if (client is not null && !client.IsConnected)
                {
                    await ConnectAsync(cancellationToken);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning("MQTT connection attempt failed: {Message}", ex.Message);
            }

            await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
        }
    }

    private Task<MqttClientConnectResult> ConnectAsync(CancellationToken cancellationToken)
    {
        var clientOptions = new MqttClientOptionsBuilder()
            .WithClientId(options.ClientId)
            .WithCredentials(options.Username, options.Password)
            .WithWebSocketServer(websocket => websocket.WithUri($"wss://{options.Host}:{options.Port}/mqtt"))
            .WithTlsOptions(tls => tls.UseTls())
            .Build();

        logger.LogInformation("Connecting to MQTT broker {Host}:{Port}", options.Host, options.Port);
        return client!.ConnectAsync(clientOptions, cancellationToken);
    }
}
