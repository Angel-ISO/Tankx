using backend.domain.entities;
using backend.domain.interfaces;
using backend.persistence;
using Microsoft.EntityFrameworkCore;

namespace backend.application.Repository;

public class ProfileRepository : GenericRepository<Profile>, IProfileRepository
{
    public ProfileRepository(TankxContext context) : base(context) { }

    public override async Task<(int TotalRecords, IEnumerable<Profile> Records)> GetAllAsync(
        int pageIndex,
        int pageSize,
        string search)
    {
        var query = Context.Profiles.AsNoTracking().AsQueryable();

        if (!string.IsNullOrEmpty(search))
        {
            query = query.Where(profile =>
                EF.Functions.ILike(profile.DisplayName, $"%{search.Trim()}%"));
        }

        var totalRecords = await query.CountAsync();
        var records = await query
            .OrderBy(profile => profile.DisplayName)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (totalRecords, records);
    }
}
