using backend.domain.entities;
using backend.domain.interfaces;
using backend.persistence;
using Microsoft.EntityFrameworkCore;

namespace backend.application.Repository;

public class MatchRepository : GenericRepository<Match>, IMatchRepository
{
    public MatchRepository(TankxContext context) : base(context) { }

    public override async Task<Match?> GetByIdAsync(Guid id)
    {
        return await Context.Matches
            .Include(match => match.MatchParticipants)
            .ThenInclude(p => p.Profile)
            .FirstOrDefaultAsync(match => match.Id == id);
    }

    public override async Task<(int TotalRecords, IEnumerable<Match> Records)> GetAllAsync(
        int pageIndex,
        int pageSize,
        string search)
    {
        var query = Context.Matches
            .AsNoTracking()
            .Include(match => match.MatchParticipants)
            .ThenInclude(p => p.Profile)
            .AsQueryable();

        if (!string.IsNullOrEmpty(search))
        {
            query = query.Where(match =>
                EF.Functions.ILike(match.MapName, $"%{search.Trim()}%"));
        }

        var totalRecords = await query.CountAsync();
        var records = await query
            .OrderByDescending(match => match.StartedAt)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (totalRecords, records);
    }

    public async Task<(int TotalRecords, IEnumerable<Match> Records)> GetAllByRegionAsync(
        int pageIndex,
        int pageSize,
        string search,
        Region? region)
    {
        var query = Context.Matches
            .AsNoTracking()
            .Include(match => match.MatchParticipants)
            .ThenInclude(p => p.Profile)
            .AsQueryable();

        if (region is not null)
        {
            query = query.Where(match => match.Region == region);
        }

        if (!string.IsNullOrEmpty(search))
        {
            query = query.Where(match =>
                EF.Functions.ILike(match.MapName, $"%{search.Trim()}%"));
        }

        var totalRecords = await query.CountAsync();
        var records = await query
            .OrderByDescending(match => match.StartedAt)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (totalRecords, records);
    }
}