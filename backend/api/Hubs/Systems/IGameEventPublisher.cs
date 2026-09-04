using backend.api.Hubs.Events;

namespace backend.api.Hubs.Systems;

public interface IGameEventPublisher
{
    Task PublishCollisionAsync(CollisionEvent collision, CancellationToken cancellationToken = default);

    Task PublishGameOverAsync(GameOverEvent gameOver, CancellationToken cancellationToken = default);
}
