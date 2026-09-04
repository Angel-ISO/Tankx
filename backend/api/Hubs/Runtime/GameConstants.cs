namespace backend.api.Hubs.Runtime;

internal static class GameConstants
{
    public const int CellSize = 64;
    public const int TankSize = 42;
    public const int MovementStep = 16;
    public const int MaxHealth = 100;
    public const int BulletDamage = 25;
    public const double BulletSpeedPerSecond = 480;
    public const int MagazineSize = 12;
    public const int KillScore = 100;
    public const int BlockScore = 50;
    public static readonly TimeSpan MovementCooldown = TimeSpan.FromMilliseconds(45);
    public static readonly TimeSpan ShotCooldown = TimeSpan.FromMilliseconds(400);
    public static readonly TimeSpan ReloadDuration = TimeSpan.FromSeconds(2);
    public static readonly TimeSpan TickInterval = TimeSpan.FromMilliseconds(50);
    public static readonly TimeSpan ReconnectGracePeriod = TimeSpan.FromSeconds(10);
}
