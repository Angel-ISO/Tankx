using backend.api.Hubs.DTOs;

namespace backend.api.Hubs.Runtime;

internal sealed class RuntimePlayer
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required string TankType { get; init; }
    public required string ConnectionId { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public int Health { get; set; } = GameConstants.MaxHealth;
    public int Score { get; set; }
    public int Ammunition { get; set; } = GameConstants.MagazineSize;
    public int Kills { get; set; }
    public int Deaths { get; set; }
    public int ShotsFired { get; set; }
    public int ShotsHit { get; set; }
    public int Placement { get; set; }
    public string Direction { get; set; } = "up";
    public bool IsAlive { get; set; } = true;
    public bool IsConnected { get; set; } = true;
    public DateTime LastMovementAt { get; set; } = DateTime.MinValue;
    public DateTime LastShotAt { get; set; } = DateTime.MinValue;
    public DateTime? ReloadCompletesAt { get; set; }
}

internal sealed class RuntimeBullet
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required Guid OwnerId { get; init; }
    public double X { get; set; }
    public double Y { get; set; }
    public required string Direction { get; init; }
}

internal sealed record EngineEvents(
    bool Changed,
    IReadOnlyList<BlockDestroyedEvent> DestroyedBlocks,
    IReadOnlyList<PlayerHitEvent> Hits,
    IReadOnlyList<PlayerEliminatedEvent> Eliminations,
    Guid? WinnerId = null
);

internal sealed class GameEngine
{
    private static readonly (int X, int Y)[] SpawnPoints =
    [
        (64, 64),
        (1408, 64),
        (64, 960),
        (1408, 960)
    ];

    private readonly GameMap map = new();
    private readonly Dictionary<Guid, RuntimePlayer> players = [];
    private readonly List<RuntimeBullet> bullets = [];
    private readonly List<BlockDestroyedEvent> destroyedBlocks = [];

    public IReadOnlyCollection<RuntimePlayer> Players => players.Values;
    public IReadOnlyCollection<RuntimeBullet> Bullets => bullets;
    public IReadOnlyCollection<BlockDestroyedEvent> DestroyedBlocks => destroyedBlocks;

    public RuntimePlayer AddPlayer(Guid id, string name, string tankType, string connectionId)
    {
        if (players.TryGetValue(id, out var existing))
        {
            existing.ConnectionId = connectionId;
            existing.IsConnected = true;
            return existing;
        }

        var spawn = SpawnPoints[players.Count];
        var player = new RuntimePlayer
        {
            Id = id,
            Name = name,
            TankType = tankType,
            ConnectionId = connectionId,
            X = spawn.X,
            Y = spawn.Y
        };
        players.Add(id, player);
        return player;
    }

    public bool RemovePlayer(Guid playerId) => players.Remove(playerId);

    public RuntimePlayer? FindPlayer(Guid playerId) => players.GetValueOrDefault(playerId);

    public void Start()
    {
        var index = 0;
        foreach (var player in players.Values)
        {
            var spawn = SpawnPoints[index++];
            player.X = spawn.X;
            player.Y = spawn.Y;
            player.Health = GameConstants.MaxHealth;
            player.Score = 0;
            player.Ammunition = GameConstants.MagazineSize;
            player.Kills = 0;
            player.Deaths = 0;
            player.ShotsFired = 0;
            player.ShotsHit = 0;
            player.Placement = 0;
            player.Direction = "up";
            player.IsAlive = true;
            player.ReloadCompletesAt = null;
        }
    }

    public bool Move(Guid playerId, string direction, DateTime now)
    {
        if (!players.TryGetValue(playerId, out var player) || !player.IsAlive || !player.IsConnected)
            return false;
        if (now - player.LastMovementAt < GameConstants.MovementCooldown)
            return false;
        if (!TryNormalizeDirection(direction, out var normalized))
            return false;

        player.Direction = normalized;
        player.LastMovementAt = now;
        var (dx, dy) = DirectionVector(normalized);
        var nextX = player.X + dx * GameConstants.MovementStep;
        var nextY = player.Y + dy * GameConstants.MovementStep;

        if (map.CollidesWithTank(nextX, nextY) || players.Values.Any(other =>
                other.Id != playerId && other.IsAlive && RectanglesOverlap(
                    nextX, nextY, GameConstants.TankSize, GameConstants.TankSize,
                    other.X, other.Y, GameConstants.TankSize, GameConstants.TankSize)))
            return true;

        player.X = nextX;
        player.Y = nextY;
        return true;
    }

    public bool Shoot(Guid playerId, DateTime now)
    {
        if (!players.TryGetValue(playerId, out var player) || !player.IsAlive || !player.IsConnected)
            return false;
        if (now - player.LastShotAt < GameConstants.ShotCooldown)
            return false;

        if (player.Ammunition == 0)
        {
            if (player.ReloadCompletesAt is null)
                player.ReloadCompletesAt = now + GameConstants.ReloadDuration;
            return false;
        }

        player.LastShotAt = now;
        player.Ammunition--;
        player.ShotsFired++;
        if (player.Ammunition == 0)
            player.ReloadCompletesAt = now + GameConstants.ReloadDuration;

        var (dx, dy) = DirectionVector(player.Direction);
        bullets.Add(new RuntimeBullet
        {
            OwnerId = player.Id,
            Direction = player.Direction,
            X = player.X + GameConstants.TankSize / 2d + dx * (GameConstants.TankSize / 2d + 8),
            Y = player.Y + GameConstants.TankSize / 2d + dy * (GameConstants.TankSize / 2d + 8)
        });
        return true;
    }

    public EngineEvents Step(TimeSpan elapsed, DateTime now)
    {
        var changed = ReloadPlayers(now);
        var blocks = new List<BlockDestroyedEvent>();
        var hits = new List<PlayerHitEvent>();
        var eliminations = new List<PlayerEliminatedEvent>();

        for (var index = bullets.Count - 1; index >= 0; index--)
        {
            var bullet = bullets[index];
            var (dx, dy) = DirectionVector(bullet.Direction);
            bullet.X += dx * GameConstants.BulletSpeedPerSecond * elapsed.TotalSeconds;
            bullet.Y += dy * GameConstants.BulletSpeedPerSecond * elapsed.TotalSeconds;
            changed = true;

            var cell = map.GetCell(bullet.X, bullet.Y);
            if (cell != 0)
            {
                if (cell == 2)
                {
                    var coordinates = map.GetCoordinates(bullet.X, bullet.Y);
                    if (map.Destroy(coordinates.Row, coordinates.Column))
                    {
                        var block = new BlockDestroyedEvent(bullet.OwnerId, coordinates.Row, coordinates.Column);
                        destroyedBlocks.Add(block);
                        blocks.Add(block);
                        if (players.TryGetValue(bullet.OwnerId, out var owner))
                            owner.Score += GameConstants.BlockScore;
                    }
                }
                bullets.RemoveAt(index);
                continue;
            }

            var target = players.Values.FirstOrDefault(player =>
                player.Id != bullet.OwnerId && player.IsAlive &&
                PointInsideRectangle(bullet.X, bullet.Y, player.X, player.Y, GameConstants.TankSize));
            if (target is null)
                continue;

            bullets.RemoveAt(index);
            target.Health = Math.Max(0, target.Health - GameConstants.BulletDamage);
            players.GetValueOrDefault(bullet.OwnerId)!.ShotsHit++;
            hits.Add(new PlayerHitEvent(
                bullet.OwnerId, target.Id, GameConstants.BulletDamage, target.Health, now));

            if (target.Health == 0)
            {
                target.IsAlive = false;
                target.Deaths++;
                target.Placement = players.Values.Count(player => player.IsAlive) + 1;
                var attacker = players.GetValueOrDefault(bullet.OwnerId);
                if (attacker is not null)
                {
                    attacker.Kills++;
                    attacker.Score += GameConstants.KillScore;
                }
                eliminations.Add(new PlayerEliminatedEvent(target.Id, bullet.OwnerId, target.Placement, now));
                var eliminationWinner = GetWinner();
                if (eliminationWinner.HasValue)
                    return new EngineEvents(true, blocks, hits, eliminations, eliminationWinner);
            }
        }

        var winner = GetWinner();
        return new EngineEvents(changed, blocks, hits, eliminations, winner);
    }

    public PlayerEliminatedEvent? EliminateDisconnected(Guid playerId, DateTime now)
    {
        var player = FindPlayer(playerId);
        if (player is null || !player.IsAlive || player.IsConnected)
            return null;

        player.Health = 0;
        player.IsAlive = false;
        player.Deaths++;
        player.Placement = players.Values.Count(candidate => candidate.IsAlive) + 1;
        return new PlayerEliminatedEvent(player.Id, null, player.Placement, now);
    }

    public Guid? GetWinner()
    {
        var alive = players.Values.Where(player => player.IsAlive).Take(2).ToArray();
        return alive.Length == 1 && players.Count >= 2 ? alive[0].Id : null;
    }

    public void Finish() => bullets.Clear();

    private bool ReloadPlayers(DateTime now)
    {
        var changed = false;
        foreach (var player in players.Values.Where(player =>
                     player.ReloadCompletesAt.HasValue && player.ReloadCompletesAt <= now))
        {
            player.Ammunition = GameConstants.MagazineSize;
            player.ReloadCompletesAt = null;
            changed = true;
        }
        return changed;
    }

    private static bool TryNormalizeDirection(string direction, out string normalized)
    {
        normalized = direction.Trim().ToLowerInvariant();
        return normalized is "up" or "down" or "left" or "right";
    }

    private static (int X, int Y) DirectionVector(string direction) => direction switch
    {
        "up" => (0, -1),
        "down" => (0, 1),
        "left" => (-1, 0),
        "right" => (1, 0),
        _ => (0, 0)
    };

    private static bool RectanglesOverlap(
        double leftX, double leftY, double leftWidth, double leftHeight,
        double rightX, double rightY, double rightWidth, double rightHeight) =>
        leftX < rightX + rightWidth && leftX + leftWidth > rightX &&
        leftY < rightY + rightHeight && leftY + leftHeight > rightY;

    private static bool PointInsideRectangle(
        double pointX, double pointY, double x, double y, double size) =>
        pointX >= x && pointX <= x + size && pointY >= y && pointY <= y + size;
}
