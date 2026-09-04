using backend.domain.entities;

namespace backend.domain.interfaces;

public interface IMatchParticipantRepository : IGenericRepository<MatchParticipant>
{
    Task<IEnumerable<MatchParticipant>> GetByMatchIdAsync(Guid matchId);
}
