using System.Threading.Channels;
using backend.api.Hubs.DTOs;
using backend.domain.entities;

namespace backend.api.Hubs.Runtime;

internal abstract record GameCommand(Guid PlayerId);
internal sealed record MoveCommand(Guid PlayerId, string Direction) : GameCommand(PlayerId);
internal sealed record ShootCommand(Guid PlayerId) : GameCommand(PlayerId);
internal sealed record DisconnectExpiredCommand(Guid PlayerId) : GameCommand(PlayerId);

internal sealed class GameRoom : IAsyncDisposable
{
    private readonly object stateLock = new();
    private readonly GameEngine engine = new();
    private readonly Channel<GameCommand> commands = Channel.CreateBounded<GameCommand>(
        new BoundedChannelOptions(256)
        {
            FullMode = BoundedChannelFullMode.DropWrite,
            SingleReader = true,
            SingleWriter = false
        });
    private readonly CancellationTokenSource cancellation = new();
    private readonly Func<GameRoom, string, object, Task> broadcast;
    private readonly Func<GameRoom, Guid, Task> finish;
    private readonly Task gameLoop;
    private bool finishStarted;
    private bool startReserved;

    public GameRoom(
        string roomCode,
        Guid hostPlayerId,
        int maxPlayers,
        string mapName,
        string? passwordHash,
        Region region,
        Func<GameRoom, string, object, Task> broadcast,
        Func<GameRoom, Guid, Task> finish)
    {
        RoomCode = roomCode;
        HostPlayerId = hostPlayerId;
        MaxPlayers = maxPlayers;
        MapName = mapName;
        PasswordHash = passwordHash;
        Region = region;
        this.broadcast = broadcast;
        this.finish = finish;
        gameLoop = RunAsync(cancellation.Token);
    }

    public string RoomCode { get; }
    public Guid HostPlayerId { get; private set; }
    public int MaxPlayers { get; }
    public string MapName { get; }
    public string? PasswordHash { get; }
    public Region Region { get; }
    public Guid? MatchId { get; private set; }
    public RoomStatus Status { get; private set; } = RoomStatus.Waiting;

    public RoomSummary Summary
    {
        get
        {
            lock (stateLock)
            {
                var host = engine.FindPlayer(HostPlayerId);
                return new RoomSummary(
                    RoomCode,
                    HostPlayerId,
                    host?.Name ?? string.Empty,
                    engine.Players.Count,
                    MaxPlayers,
                    MapName,
                    PasswordHash is not null,
                    Status);
            }
        }
    }

    public IReadOnlyCollection<Guid> PlayerIds
    {
        get
        {
            lock (stateLock)
                return engine.Players.Select(player => player.Id).ToArray();
        }
    }

    public bool Contains(Guid playerId)
    {
        lock (stateLock)
            return engine.FindPlayer(playerId) is not null;
    }

    public GameStateSnapshot Join(Guid playerId, string name, string tankType, string connectionId)
    {
        lock (stateLock)
        {
            var existing = engine.FindPlayer(playerId);
            if (existing is null && (Status != RoomStatus.Waiting || startReserved))
                throw new InvalidOperationException("The match has already started.");
            if (existing is null && engine.Players.Count >= MaxPlayers)
                throw new InvalidOperationException("The room is full.");

            engine.AddPlayer(playerId, name, tankType, connectionId);
            return CreateSnapshot();
        }
    }

    public (GameStateSnapshot? Snapshot, bool Removed, Guid? NewHost) Leave(Guid playerId)
    {
        lock (stateLock)
        {
            if (Status == RoomStatus.InProgress || startReserved || !engine.RemovePlayer(playerId))
                return (null, false, null);

            Guid? newHost = null;
            if (HostPlayerId == playerId && engine.Players.Count > 0)
            {
                HostPlayerId = engine.Players.First().Id;
                newHost = HostPlayerId;
            }
            return (CreateSnapshot(), true, newHost);
        }
    }

    public GameStateSnapshot Start(Guid requesterId, Guid matchId)
    {
        lock (stateLock)
        {
            if (requesterId != HostPlayerId)
                throw new InvalidOperationException("Only the host can start the match.");
            if (Status != RoomStatus.Waiting)
                throw new InvalidOperationException("The room is not waiting for players.");
            if (engine.Players.Count != MaxPlayers)
                throw new InvalidOperationException("Every configured player slot must be filled before starting.");
            if (!startReserved)
                throw new InvalidOperationException("The match start was not reserved.");

            MatchId = matchId;
            startReserved = false;
            Status = RoomStatus.InProgress;
            engine.Start();
            return CreateSnapshot();
        }
    }

    public void ReserveStart(Guid requesterId)
    {
        lock (stateLock)
        {
            if (requesterId != HostPlayerId)
                throw new InvalidOperationException("Only the host can start the match.");
            if (Status != RoomStatus.Waiting || startReserved)
                throw new InvalidOperationException("The match has already started.");
            if (engine.Players.Count != MaxPlayers)
                throw new InvalidOperationException("Every configured player slot must be filled before starting.");
            startReserved = true;
        }
    }

    public void CancelStart()
    {
        lock (stateLock)
            startReserved = false;
    }

    public bool TryEnqueue(GameCommand command) =>
        Status == RoomStatus.InProgress && commands.Writer.TryWrite(command);

    public GameStateSnapshot? MarkDisconnected(Guid playerId)
    {
        lock (stateLock)
        {
            var player = engine.FindPlayer(playerId);
            if (player is null)
                return null;
            player.IsConnected = false;
            return CreateSnapshot();
        }
    }

    public bool OwnsConnection(Guid playerId, string connectionId)
    {
        lock (stateLock)
            return engine.FindPlayer(playerId)?.ConnectionId == connectionId;
    }

    public IReadOnlyCollection<RuntimePlayer> GetFinalPlayers()
    {
        lock (stateLock)
            return engine.Players.ToArray();
    }

    public GameStateSnapshot Snapshot()
    {
        lock (stateLock)
            return CreateSnapshot();
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(GameConstants.TickInterval);
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                List<(string Name, object Payload)> events = [];
                GameStateSnapshot? snapshot = null;
                Guid? winnerId = null;

                lock (stateLock)
                {
                    if (Status != RoomStatus.InProgress)
                        continue;

                    var changed = false;
                    while (commands.Reader.TryRead(out var command))
                    {
                        switch (command)
                        {
                            case MoveCommand move:
                                if (engine.Move(move.PlayerId, move.Direction, DateTime.UtcNow))
                                {
                                    changed = true;
                                    var player = engine.FindPlayer(move.PlayerId)!;
                                    events.Add(("PlayerMoved", new PlayerMovementEvent(
                                        player.Id, player.Name, player.Direction,
                                        (int)Math.Round(player.X), (int)Math.Round(player.Y))));
                                }
                                break;
                            case ShootCommand shoot:
                                changed |= engine.Shoot(shoot.PlayerId, DateTime.UtcNow);
                                break;
                            case DisconnectExpiredCommand disconnected:
                                var eliminated = engine.EliminateDisconnected(disconnected.PlayerId, DateTime.UtcNow);
                                if (eliminated is not null)
                                {
                                    changed = true;
                                    events.Add(("PlayerEliminated", eliminated));
                                    winnerId = engine.GetWinner();
                                }
                                break;
                        }

                        if (winnerId.HasValue)
                            break;
                    }

                    if (!winnerId.HasValue)
                    {
                        var tick = engine.Step(GameConstants.TickInterval, DateTime.UtcNow);
                        changed |= tick.Changed;
                        events.AddRange(tick.DestroyedBlocks.Select(item => ("BlockDestroyed", (object)item)));
                        events.AddRange(tick.Hits.Select(item => ("PlayerHit", (object)item)));
                        events.AddRange(tick.Eliminations.Select(item => ("PlayerEliminated", (object)item)));
                        winnerId = tick.WinnerId ?? engine.GetWinner();
                    }

                    if (winnerId.HasValue && !finishStarted)
                    {
                        finishStarted = true;
                        var winner = engine.FindPlayer(winnerId.Value)!;
                        winner.Placement = 1;
                        engine.Finish();
                        Status = RoomStatus.Finished;
                        changed = true;
                    }

                    if (changed)
                        snapshot = CreateSnapshot();
                }

                foreach (var gameEvent in events)
                    await broadcast(this, gameEvent.Name, gameEvent.Payload);
                if (snapshot is not null)
                    await broadcast(this, "GameStateUpdated", snapshot);
                if (winnerId.HasValue)
                    await finish(this, winnerId.Value);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private GameStateSnapshot CreateSnapshot() => new(
        RoomCode,
        MatchId,
        Status,
        HostPlayerId,
        MaxPlayers,
        MapName,
        engine.Players.Select(player => new PlayerStateDto(
            player.Id,
            player.Name,
            player.TankType,
            (int)Math.Round(player.X),
            (int)Math.Round(player.Y),
            player.Health,
            player.Score,
            player.Ammunition,
            player.Kills,
            player.Deaths,
            player.Direction,
            player.IsAlive ? "active" : "destroyed",
            player.IsConnected,
            player.Id == HostPlayerId)).ToArray(),
        engine.Bullets.Select(bullet => new BulletStateDto(
            bullet.Id, bullet.OwnerId, bullet.X, bullet.Y, bullet.Direction)).ToArray(),
        engine.DestroyedBlocks.ToArray(),
        DateTime.UtcNow);

    public async ValueTask DisposeAsync()
    {
        cancellation.Cancel();
        commands.Writer.TryComplete();
        try
        {
            await gameLoop;
        }
        catch (OperationCanceledException)
        {
        }
        cancellation.Dispose();
    }
}
