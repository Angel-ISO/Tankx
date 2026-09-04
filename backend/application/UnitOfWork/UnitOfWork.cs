using backend.persistence;
using backend.domain.interfaces;
using backend.application.Repository;


namespace backend.application.UnitOfWork;
public class UnitOfWork : IUnitOfWork, IDisposable
{
    private readonly TankxContext _context;
    private IProfileRepository? _profiles;
    private IMatchParticipantRepository? _matchParticipants;
    private IMatchRepository? _matches;

    public UnitOfWork(TankxContext context)
    {
        _context = context;
    }

    public IProfileRepository Profiles =>
        _profiles ??= new ProfileRepository(_context);

    public IMatchParticipantRepository MatchParticipants =>
        _matchParticipants ??= new MatchParticipantRepository(_context);

    public IMatchRepository Matches =>
        _matches ??= new MatchRepository(_context);

    public async Task<int> SaveAsync()
    {
        return await _context.SaveChangesAsync();
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
