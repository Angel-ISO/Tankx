namespace backend.api.Hubs.Systems;

public interface IEventHistoryStore
{
    Task AppendAsync(string payloadJson, CancellationToken cancellationToken = default);

   
    Task<IReadOnlyList<string>> GetRecentAsync(int count, CancellationToken cancellationToken = default);
}
