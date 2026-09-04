namespace backend.api.Hubs.Events;

public sealed record GameOverEvent(
    string PlayerId,
    string PlayerName,
    string TargetId,
    string TargetName,
    DateTime SentAt
);
