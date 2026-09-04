namespace backend.api.Hubs.DTOs;

public enum RoomStatus
{
    Waiting,
    InProgress,
    Finished
}

public sealed record CreateRoomRequest(
    int MaxPlayers,
    string MapName,
    string? Password,
    string? Region = null
);

public sealed record JoinRoomRequest(
    string RoomCode,
    string? Password
);

public sealed record RoomSummary(
    string RoomCode,
    Guid HostPlayerId,
    string HostPlayerName,
    int PlayerCount,
    int MaxPlayers,
    string MapName,
    bool IsPrivate,
    RoomStatus Status
);

public sealed record MovementInput(string Direction, long Sequence = 0);

public sealed record ChatMessageInput(string Message);

public sealed record ChatMessageEvent(
    Guid PlayerId,
    string PlayerName,
    string Message,
    DateTime SentAt
);

public sealed record PlayerStateDto(
    Guid PlayerId,
    string PlayerName,
    string TankType,
    int X,
    int Y,
    int Health,
    int Score,
    int Ammunition,
    int Kills,
    int Deaths,
    string Direction,
    string Status,
    bool IsConnected,
    bool IsHost
);

public sealed record BulletStateDto(
    Guid Id,
    Guid OwnerId,
    double X,
    double Y,
    string Direction
);

public sealed record BlockDestroyedEvent(
    Guid PlayerId,
    int Row,
    int Column
);

public sealed record PlayerMovementEvent(
    Guid PlayerId,
    string PlayerName,
    string Direction,
    int X,
    int Y
);

public sealed record PlayerHitEvent(
    Guid AttackerId,
    Guid TargetId,
    int Damage,
    int RemainingHealth,
    DateTime ServerTime
);

public sealed record PlayerEliminatedEvent(
    Guid PlayerId,
    Guid? EliminatedBy,
    int Placement,
    DateTime ServerTime
);

public sealed record MatchStartedEvent(Guid MatchId, DateTime StartedAt);

public sealed record MatchEndedEvent(
    Guid MatchId,
    Guid WinnerId,
    string WinnerName,
    DateTime FinishedAt
);

public sealed record GameStateSnapshot(
    string RoomCode,
    Guid? MatchId,
    RoomStatus Status,
    Guid HostPlayerId,
    int MaxPlayers,
    string MapName,
    IReadOnlyCollection<PlayerStateDto> Players,
    IReadOnlyCollection<BulletStateDto> Bullets,
    IReadOnlyCollection<BlockDestroyedEvent> DestroyedBlocks,
    DateTime ServerTime
);
