namespace backend.api.Hubs.Events;

public sealed record CollisionEvent(
    string PlayerId,
    string PlayerName,
    string TargetId,
    string TargetName,
    int Damage,
    DateTime SentAt
);
