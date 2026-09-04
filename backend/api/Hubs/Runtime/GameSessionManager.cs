using System.Collections.Concurrent;
using System.Security.Cryptography;
using backend.api.Hubs.DTOs;
using backend.domain.entities;
using backend.persistence;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace backend.api.Hubs.Runtime;

public sealed class GameSessionManager : IAsyncDisposable
{
    private const string RoomCodeAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    private readonly ConcurrentDictionary<string, GameRoom> rooms =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<Guid, string> playerRooms = [];
    private readonly ConcurrentDictionary<string, Guid> connectionPlayers = [];
    private readonly ConcurrentDictionary<Guid, DateTime> lastChatMessages = [];
    private readonly IHubContext<GameHub> hubContext;
    private readonly IServiceScopeFactory scopeFactory;
    private readonly ILogger<GameSessionManager> logger;
    private readonly SemaphoreSlim startLock = new(1, 1);

    public GameSessionManager(
        IHubContext<GameHub> hubContext,
        IServiceScopeFactory scopeFactory,
        ILogger<GameSessionManager> logger)
    {
        this.hubContext = hubContext;
        this.scopeFactory = scopeFactory;
        this.logger = logger;
    }

    public IReadOnlyCollection<RoomSummary> ListRooms() => rooms.Values
        .Select(room => room.Summary)
        .Where(room => room.Status == RoomStatus.Waiting && room.PlayerCount < room.MaxPlayers)
        .OrderBy(room => room.RoomCode)
        .ToArray();

    public async Task<(RoomSummary Room, GameStateSnapshot Snapshot)> CreateRoomAsync(
        Guid playerId,
        string connectionId,
        CreateRoomRequest request,
        CancellationToken cancellationToken)
    {
        if (request.MaxPlayers is < 2 or > 4)
            throw new InvalidOperationException("A room must allow between two and four players.");
        if (string.IsNullOrWhiteSpace(request.MapName))
            throw new InvalidOperationException("A map name is required.");
        var mapName = request.MapName.Trim().ToLowerInvariant();
        if (!GameMap.IsSupported(mapName))
            throw new InvalidOperationException("The selected map is not supported.");
        var password = NormalizePassword(request.Password);
        var region = ResolveRegion(request.Region);
        EnsurePlayerIsAvailable(playerId);
        var profile = await GetProfileAsync(playerId, cancellationToken);

        GameRoom room;
        do
        {
            var roomCode = CreateRoomCode();
            room = new GameRoom(
                roomCode,
                playerId,
                request.MaxPlayers,
                mapName,
                password is null ? null : HashPassword(password),
                region,
                BroadcastAsync,
                FinishMatchAsync);
            if (rooms.TryAdd(roomCode, room))
                break;
            await room.DisposeAsync();
        } while (true);

        var snapshot = room.Join(playerId, profile.DisplayName, profile.TankType, connectionId);
        playerRooms[playerId] = room.RoomCode;
        RemovePlayerConnections(playerId);
        connectionPlayers[connectionId] = playerId;
        return (room.Summary, snapshot);
    }

    public async Task<(RoomSummary Room, GameStateSnapshot Snapshot)> JoinRoomAsync(
        Guid playerId,
        string connectionId,
        JoinRoomRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.RoomCode))
            throw new InvalidOperationException("A room code is required.");
        var roomCode = request.RoomCode.Trim().ToUpperInvariant();
        if (!rooms.TryGetValue(roomCode, out var room))
            throw new InvalidOperationException("The room does not exist.");

        if (playerRooms.TryGetValue(playerId, out var currentRoom) &&
            !string.Equals(currentRoom, roomCode, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Leave the current room before joining another one.");
        if (!room.Contains(playerId) && !VerifyPassword(room.PasswordHash, request.Password))
            throw new InvalidOperationException("The room password is incorrect.");

        var profile = await GetProfileAsync(playerId, cancellationToken);
        var snapshot = room.Join(playerId, profile.DisplayName, profile.TankType, connectionId);
        playerRooms[playerId] = room.RoomCode;
        RemovePlayerConnections(playerId);
        connectionPlayers[connectionId] = playerId;
        await BroadcastAsync(room, "PlayerJoined", snapshot.Players.Single(player => player.PlayerId == playerId));
        await BroadcastAsync(room, "GameStateUpdated", snapshot);
        return (room.Summary, snapshot);
    }

    public async Task<string> LeaveRoomAsync(Guid playerId)
    {
        var room = GetPlayerRoom(playerId);
        var result = room.Leave(playerId);
        if (!result.Removed)
            throw new InvalidOperationException("Players cannot leave an active match; disconnect instead.");

        playerRooms.TryRemove(playerId, out _);
        RemovePlayerConnections(playerId);
        await BroadcastAsync(room, "PlayerLeft", playerId);
        if (result.Snapshot is not null)
            await BroadcastAsync(room, "GameStateUpdated", result.Snapshot);
        if (room.PlayerIds.Count == 0 && rooms.TryRemove(room.RoomCode, out var removed))
            await removed.DisposeAsync();
        return room.RoomCode;
    }

    public async Task<GameStateSnapshot> StartMatchAsync(Guid playerId, CancellationToken cancellationToken)
    {
        var room = GetPlayerRoom(playerId);
        await startLock.WaitAsync(cancellationToken);
        try
        {
            if (room.Status != RoomStatus.Waiting)
                throw new InvalidOperationException("The match has already started.");
            if (room.HostPlayerId != playerId)
                throw new InvalidOperationException("Only the host can start the match.");
            if (room.PlayerIds.Count != room.MaxPlayers)
                throw new InvalidOperationException("Every configured player slot must be filled before starting.");

            room.ReserveStart(playerId);
            GameStateSnapshot snapshot;
            Guid matchId;
            try
            {
                using var scope = scopeFactory.CreateScope();
                var persistence = scope.ServiceProvider.GetRequiredService<MatchPersistenceService>();
                matchId = await persistence.StartAsync(room.MapName, room.Region, room.PlayerIds, cancellationToken);
                snapshot = room.Start(playerId, matchId);
            }
            catch
            {
                room.CancelStart();
                throw;
            }

            await BroadcastAsync(room, "MatchStarted", new MatchStartedEvent(matchId, DateTime.UtcNow));
            await BroadcastAsync(room, "GameStateUpdated", snapshot);
            return snapshot;
        }
        finally
        {
            startLock.Release();
        }
    }

    public void Move(Guid playerId, MovementInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Direction) || input.Direction.Length > 10)
            throw new InvalidOperationException("The movement direction is invalid.");
        if (!GetPlayerRoom(playerId).TryEnqueue(new MoveCommand(playerId, input.Direction)))
            throw new InvalidOperationException("The movement command could not be accepted.");
    }

    public void Shoot(Guid playerId)
    {
        if (!GetPlayerRoom(playerId).TryEnqueue(new ShootCommand(playerId)))
            throw new InvalidOperationException("The shot command could not be accepted.");
    }

    public async Task SendMessageAsync(Guid playerId, ChatMessageInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Message))
            throw new InvalidOperationException("A message is required.");
        var message = input.Message.Trim();
        if (message.Length is < 1 or > 200)
            throw new InvalidOperationException("Messages must contain between one and 200 characters.");
        var now = DateTime.UtcNow;
        if (lastChatMessages.TryGetValue(playerId, out var previous) &&
            now - previous < TimeSpan.FromMilliseconds(250))
            throw new InvalidOperationException("Messages are being sent too quickly.");
        lastChatMessages[playerId] = now;
        var room = GetPlayerRoom(playerId);
        var player = room.Snapshot().Players.Single(item => item.PlayerId == playerId);
        await BroadcastAsync(room, "ReceiveMessage", new ChatMessageEvent(
            playerId, player.PlayerName, message, DateTime.UtcNow));
    }

    public async Task HandleDisconnectedAsync(string connectionId)
    {
        if (!connectionPlayers.TryRemove(connectionId, out var playerId) ||
            !playerRooms.TryGetValue(playerId, out var roomCode) ||
            !rooms.TryGetValue(roomCode, out var room))
            return;

        if (!room.OwnsConnection(playerId, connectionId))
            return;

        if (room.Status != RoomStatus.InProgress)
        {
            var result = room.Leave(playerId);
            if (!result.Removed)
            {
                var reservedSnapshot = room.MarkDisconnected(playerId);
                if (reservedSnapshot is not null)
                    await BroadcastAsync(room, "GameStateUpdated", reservedSnapshot);
                _ = ResolveReservedDisconnectAsync(room, playerId);
                return;
            }
            playerRooms.TryRemove(playerId, out _);
            await BroadcastAsync(room, "PlayerLeft", playerId);
            if (result.Snapshot is not null)
                await BroadcastAsync(room, "GameStateUpdated", result.Snapshot);
            if (room.PlayerIds.Count == 0 && rooms.TryRemove(room.RoomCode, out var removed))
                await removed.DisposeAsync();
            return;
        }

        var snapshot = room.MarkDisconnected(playerId);
        if (snapshot is not null)
            await BroadcastAsync(room, "GameStateUpdated", snapshot);

        _ = EliminateAfterReconnectGraceAsync(room, playerId);
    }

    private async Task ResolveReservedDisconnectAsync(GameRoom room, Guid playerId)
    {
        await Task.Delay(GameConstants.ReconnectGracePeriod);
        if (room.Status == RoomStatus.InProgress)
        {
            room.TryEnqueue(new DisconnectExpiredCommand(playerId));
            return;
        }

        if (room.Status == RoomStatus.Waiting)
        {
            var result = room.Leave(playerId);
            if (!result.Removed)
                return;
            playerRooms.TryRemove(playerId, out _);
            await BroadcastAsync(room, "PlayerLeft", playerId);
            if (result.Snapshot is not null)
                await BroadcastAsync(room, "GameStateUpdated", result.Snapshot);
            if (room.PlayerIds.Count == 0 && rooms.TryRemove(room.RoomCode, out var removed))
                await removed.DisposeAsync();
        }
    }

    private async Task EliminateAfterReconnectGraceAsync(GameRoom room, Guid playerId)
    {
        await Task.Delay(GameConstants.ReconnectGracePeriod);
        if (room.Status == RoomStatus.InProgress)
            room.TryEnqueue(new DisconnectExpiredCommand(playerId));
    }

    private async Task FinishMatchAsync(GameRoom room, Guid winnerId)
    {
        if (!room.MatchId.HasValue)
            return;
        try
        {
            using var scope = scopeFactory.CreateScope();
            var persistence = scope.ServiceProvider.GetRequiredService<MatchPersistenceService>();
            await persistence.FinishAsync(room.MatchId.Value, winnerId, room.GetFinalPlayers());
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Could not persist completed match {MatchId}", room.MatchId);
        }

        var winner = room.Snapshot().Players.Single(player => player.PlayerId == winnerId);
        await BroadcastAsync(room, "MatchEnded", new MatchEndedEvent(
            room.MatchId.Value, winnerId, winner.PlayerName, DateTime.UtcNow));
    }

    private Task BroadcastAsync(GameRoom room, string method, object payload) =>
        hubContext.Clients.Group(room.RoomCode).SendAsync(method, payload);

    private GameRoom GetPlayerRoom(Guid playerId)
    {
        if (!playerRooms.TryGetValue(playerId, out var roomCode) || !rooms.TryGetValue(roomCode, out var room))
            throw new InvalidOperationException("Join a room before using this operation.");
        return room;
    }

    private void EnsurePlayerIsAvailable(Guid playerId)
    {
        if (playerRooms.TryGetValue(playerId, out var roomCode) && rooms.ContainsKey(roomCode))
            throw new InvalidOperationException("Leave the current room before creating another one.");
        playerRooms.TryRemove(playerId, out _);
    }

    private async Task<Profile> GetProfileAsync(Guid playerId, CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TankxContext>();
        return await context.Profiles.AsNoTracking().SingleOrDefaultAsync(
                   profile => profile.Id == playerId, cancellationToken)
               ?? throw new InvalidOperationException("Create a profile before joining a room.");
    }

    private void RemovePlayerConnections(Guid playerId)
    {
        foreach (var connection in connectionPlayers.Where(item => item.Value == playerId))
            connectionPlayers.TryRemove(connection.Key, out _);
    }

    private static string CreateRoomCode() => string.Create(6, 0, (span, _) =>
    {
        for (var index = 0; index < span.Length; index++)
            span[index] = RoomCodeAlphabet[RandomNumberGenerator.GetInt32(RoomCodeAlphabet.Length)];
    });

    private static Region ResolveRegion(string? requested)
    {
        if (string.IsNullOrWhiteSpace(requested))
            return Region.NorthAmerica;

        var normalized = requested.Trim();
        return Enum.TryParse<Region>(normalized, ignoreCase: true, out var region)
            ? region
            : throw new InvalidOperationException($"The region '{normalized}' is not supported.");
    }

    private static string? NormalizePassword(string? password)
    {
        if (string.IsNullOrWhiteSpace(password))
            return null;
        var normalized = password.Trim();
        if (normalized.Length is < 4 or > 64)
            throw new InvalidOperationException("Room passwords must contain between four and 64 characters.");
        return normalized;
    }

    private static string HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, 100_000, HashAlgorithmName.SHA256, 32);
        return $"{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }

    private static bool VerifyPassword(string? storedHash, string? password)
    {
        if (storedHash is null)
            return true;
        if (string.IsNullOrWhiteSpace(password))
            return false;
        var parts = storedHash.Split('.', 2);
        if (parts.Length != 2)
            return false;
        var salt = Convert.FromBase64String(parts[0]);
        var expected = Convert.FromBase64String(parts[1]);
        var actual = Rfc2898DeriveBytes.Pbkdf2(password.Trim(), salt, 100_000, HashAlgorithmName.SHA256, 32);
        return CryptographicOperations.FixedTimeEquals(expected, actual);
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var room in rooms.Values)
            await room.DisposeAsync();
        startLock.Dispose();
    }
}
