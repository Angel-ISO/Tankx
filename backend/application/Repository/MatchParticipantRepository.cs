using backend.domain.entities;
using backend.domain.interfaces;
using backend.persistence;
using Microsoft.EntityFrameworkCore;

namespace backend.application.Repository;

public class MatchParticipantRepository
    : GenericRepository<MatchParticipant>, IMatchParticipantRepository
{
    public MatchParticipantRepository(TankxContext context) : base(context) { }

    public override async Task<MatchParticipant?> GetByIdAsync(Guid id)
    {
        return await Context.MatchParticipants
            .Include(participant => participant.Profile)
            .Include(participant => participant.Match)
            .FirstOrDefaultAsync(participant => participant.Id == id);
    }

    public override async Task<(int TotalRecords, IEnumerable<MatchParticipant> Records)> GetAllAsync(
        int pageIndex,
        int pageSize,
        string search)
    {
        var query = Context.MatchParticipants
            .AsNoTracking()
            .Include(participant => participant.Profile)
            .Include(participant => participant.Match)
            .AsQueryable();

        if (!string.IsNullOrEmpty(search))
        {
            query = query.Where(participant =>
                EF.Functions.ILike(participant.Profile.DisplayName, $"%{search.Trim()}%"));
        }

        var totalRecords = await query.CountAsync();
        var records = await query
            .OrderByDescending(participant => participant.Score)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (totalRecords, records);
    }

    public async Task<IEnumerable<MatchParticipant>> GetByMatchIdAsync(Guid matchId)
    {
        return await Context.MatchParticipants
            .Where(participant => participant.MatchId == matchId)
            .ToListAsync();
    }
}