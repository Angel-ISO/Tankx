namespace backend.domain.interfaces;

public interface IUnitOfWork
{
    IProfileRepository Profiles { get; }
    IMatchParticipantRepository MatchParticipants { get; }
    IMatchRepository Matches { get; }
    Task<int> SaveAsync();
}
