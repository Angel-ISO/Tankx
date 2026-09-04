using backend.domain.entities;

namespace backend.domain.interfaces;

public interface IMatchRepository : IGenericRepository<Match>
{
    Task<(int TotalRecords, IEnumerable<Match> Records)> GetAllByRegionAsync(
        int pageIndex,
        int pageSize,
        string search,
        Region? region);
}
