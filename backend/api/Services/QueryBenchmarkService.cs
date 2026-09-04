using System.Diagnostics;
using backend.domain.entities;
using backend.domain.interfaces;
using backend.persistence;
using Microsoft.EntityFrameworkCore;
using EFCore.BulkExtensions;

namespace backend.api.Services;

public class QueryBenchmarkService
{
    private readonly TankxContext _context;
    private readonly IUnitOfWork _unitOfWork;

    public QueryBenchmarkService(TankxContext context, IUnitOfWork unitOfWork)
    {
        _context = context;
        _unitOfWork = unitOfWork;
    }

    public class BenchmarkResult
    {
        public string OperationName { get; set; } = string.Empty;
        public double AverageTimeMs { get; set; }
        public double MinTimeMs { get; set; }
        public double MaxTimeMs { get; set; }
        public int Iterations { get; set; }
        public long TotalRecords { get; set; }
    }

    public async Task<BenchmarkResult> BenchmarkToListAsync<T>(int iterations = 10) where T : BaseEntity
    {
        var times = new List<long>();
        long totalRecords = 0;

        for (int i = 0; i < iterations; i++)
        {
            var stopwatch = Stopwatch.StartNew();
            
            var result = await _context.Set<T>().ToListAsync();
            totalRecords = result.Count;
            
            stopwatch.Stop();
            times.Add(stopwatch.ElapsedMilliseconds);
        }

        return new BenchmarkResult
        {
            OperationName = $"ToList() - {typeof(T).Name}",
            AverageTimeMs = times.Average(),
            MinTimeMs = times.Min(),
            MaxTimeMs = times.Max(),
            Iterations = iterations,
            TotalRecords = totalRecords
        };
    }

    public async Task<BenchmarkResult> BenchmarkAsNoTrackingAsync<T>(int iterations = 10) where T : BaseEntity
    {
        var times = new List<long>();
        long totalRecords = 0;

        for (int i = 0; i < iterations; i++)
        {
            var stopwatch = Stopwatch.StartNew();
            
            var result = await _context.Set<T>().AsNoTracking().ToListAsync();
            totalRecords = result.Count;
            
            stopwatch.Stop();
            times.Add(stopwatch.ElapsedMilliseconds);
        }

        return new BenchmarkResult
        {
            OperationName = $"AsNoTracking() - {typeof(T).Name}",
            AverageTimeMs = times.Average(),
            MinTimeMs = times.Min(),
            MaxTimeMs = times.Max(),
            Iterations = iterations,
            TotalRecords = totalRecords
        };
    }

    public async Task<BenchmarkResult> BenchmarkPaginatedQueryAsync<T>(int pageIndex, int pageSize, int iterations = 10) where T : BaseEntity
    {
        var times = new List<long>();
        long totalRecords = 0;

        for (int i = 0; i < iterations; i++)
        {
            var stopwatch = Stopwatch.StartNew();
            
            var query = _context.Set<T>().AsNoTracking();
            totalRecords = await query.CountAsync();
            var records = await query
                .OrderBy(entity => entity.Id)
                .Skip((pageIndex - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
            
            stopwatch.Stop();
            times.Add(stopwatch.ElapsedMilliseconds);
        }

        return new BenchmarkResult
        {
            OperationName = $"Paginated Query (Page {pageIndex}, Size {pageSize}) - {typeof(T).Name}",
            AverageTimeMs = times.Average(),
            MinTimeMs = times.Min(),
            MaxTimeMs = times.Max(),
            Iterations = iterations,
            TotalRecords = totalRecords
        };
    }

    public async Task<BenchmarkResult> BenchmarkSearchQueryAsync<T>(Func<IQueryable<T>, IQueryable<T>> searchFunc, int iterations = 10) where T : BaseEntity
    {
        var times = new List<long>();
        long totalRecords = 0;

        for (int i = 0; i < iterations; i++)
        {
            var stopwatch = Stopwatch.StartNew();
            
            var query = searchFunc(_context.Set<T>().AsNoTracking());
            var result = await query.ToListAsync();
            totalRecords = result.Count;
            
            stopwatch.Stop();
            times.Add(stopwatch.ElapsedMilliseconds);
        }

        return new BenchmarkResult
        {
            OperationName = $"Search Query - {typeof(T).Name}",
            AverageTimeMs = times.Average(),
            MinTimeMs = times.Min(),
            MaxTimeMs = times.Max(),
            Iterations = iterations,
            TotalRecords = totalRecords
        };
    }

    public async Task<BenchmarkResult> BenchmarkBulkInsertAsync<T>(IEnumerable<T> entities, int iterations = 5) where T : BaseEntity
    {
        var times = new List<long>();
        var entityList = entities.ToList();

        for (int i = 0; i < iterations; i++)
        {
            var tempEntities = entityList.Select(e => CloneEntity(e)).ToList();
            
            var stopwatch = Stopwatch.StartNew();
            
            await _context.BulkInsertAsync(tempEntities);

            stopwatch.Stop();
            times.Add(stopwatch.ElapsedMilliseconds);

            // Clean up
            await _context.BulkDeleteAsync(tempEntities);
        }

        return new BenchmarkResult
        {
            OperationName = $"BulkInsert ({entityList.Count} records) - {typeof(T).Name}",
            AverageTimeMs = times.Average(),
            MinTimeMs = times.Min(),
            MaxTimeMs = times.Max(),
            Iterations = iterations,
            TotalRecords = entityList.Count
        };
    }

    public async Task<BenchmarkResult> BenchmarkStandardInsertAsync<T>(IEnumerable<T> entities, int iterations = 5) where T : BaseEntity
    {
        var times = new List<long>();
        var entityList = entities.ToList();

        for (int i = 0; i < iterations; i++)
        {
            var tempEntities = entityList.Select(e => CloneEntity(e)).ToList();
            
            var stopwatch = Stopwatch.StartNew();
            
            _context.Set<T>().AddRange(tempEntities);
            await _context.SaveChangesAsync();
            
            stopwatch.Stop();
            times.Add(stopwatch.ElapsedMilliseconds);

            _context.Set<T>().RemoveRange(tempEntities);
            await _context.SaveChangesAsync();
        }

        return new BenchmarkResult
        {
            OperationName = $"Standard AddRange ({entityList.Count} records) - {typeof(T).Name}",
            AverageTimeMs = times.Average(),
            MinTimeMs = times.Min(),
            MaxTimeMs = times.Max(),
            Iterations = iterations,
            TotalRecords = entityList.Count
        };
    }

    private T CloneEntity<T>(T entity) where T : BaseEntity
    {
        
        var json = System.Text.Json.JsonSerializer.Serialize(entity);
        return System.Text.Json.JsonSerializer.Deserialize<T>(json) ?? throw new InvalidOperationException("Failed to clone entity");
    }

    public async Task<Dictionary<string, BenchmarkResult>> BenchmarkBulkVsStandardAsync(int recordCount = 1000, int iterations = 5)
    {
        var profiles = Enumerable.Range(0, recordCount)
            .Select(i => new Profile
            {
                DisplayName = $"benchmark_{Guid.NewGuid():N}",
                TankType = "tank_green"
            })
            .ToList();

        var bulkResult = await BenchmarkBulkInsertAsync(profiles, iterations);
        var standardResult = await BenchmarkStandardInsertAsync(profiles, iterations);

        return new Dictionary<string, BenchmarkResult>
        {
            ["bulkInsert"] = bulkResult,
            ["standardInsert"] = standardResult
        };
    }

    public async Task<(int Profiles, int Matches, int Participants)> SeedBenchmarkDataAsync(int profileCount = 1000, int matchCount = 100)
    {
        var profiles = Enumerable.Range(0, profileCount)
            .Select(i => new Profile
            {
                DisplayName = $"test_user_{i}_{Guid.NewGuid():N}".Substring(0, 20),
                TankType = "tank_green"
            })
            .ToList();

        await _context.BulkInsertAsync(profiles);

        var matches = Enumerable.Range(0, matchCount)
            .Select(i => new Match
            {
                MapName = $"map_{i}",
                GameMode = GameMode.Deathmatch,
                StartedAt = DateTime.UtcNow.AddMinutes(-i),
                WinnerId = i % 2 == 0 ? profiles[0].Id : null
            })
            .ToList();

        await _context.BulkInsertAsync(matches);

        var participants = new List<MatchParticipant>();
        for (int m = 0; m < matches.Count; m++)
        {
            for (int p = 0; p < 4; p++)
            {
                var profile = profiles[(m * 4 + p) % profiles.Count];
                participants.Add(new MatchParticipant
                {
                    MatchId = matches[m].Id,
                    ProfileId = profile.Id,
                    Kills = p * 3,
                    Deaths = p,
                    Score = p * 100,
                    Position = p + 1,
                    Accuracy = 0.5 + p * 0.1
                });
            }
        }

        await _context.BulkInsertAsync(participants);

        return (profiles.Count, matches.Count, participants.Count);
    }

    public async Task<int> CleanupBenchmarkDataAsync()
    {
        var profiles = await _context.Profiles
            .Where(p => EF.Functions.ILike(p.DisplayName, "test_user_%"))
            .Select(p => p.Id)
            .ToListAsync();

        if (profiles.Count > 0)
        {
            var profileIds = profiles.ToList();

            var matchIds = await _context.Matches
                .Where(m => EF.Functions.ILike(m.MapName, "map_%"))
                .Select(m => m.Id)
                .ToListAsync();

            await _context.MatchParticipants
                .Where(mp => profileIds.Contains(mp.ProfileId) || matchIds.Contains(mp.MatchId))
                .ExecuteDeleteAsync();

            if (matchIds.Count > 0)
            {
                await _context.Matches.Where(m => matchIds.Contains(m.Id)).ExecuteDeleteAsync();
            }

            await _context.Profiles.Where(p => profileIds.Contains(p.Id)).ExecuteDeleteAsync();
        }

        return profiles.Count;
    }

    public async Task<List<BenchmarkResult>> RunComprehensiveBenchmarkAsync()
    {
        var results = new List<BenchmarkResult>();

        results.Add(await BenchmarkToListAsync<Profile>(10));
        results.Add(await BenchmarkAsNoTrackingAsync<Profile>(10));

        results.Add(await BenchmarkToListAsync<Match>(10));
        results.Add(await BenchmarkAsNoTrackingAsync<Match>(10));

        results.Add(await BenchmarkPaginatedQueryAsync<Profile>(1, 10, 10));
        results.Add(await BenchmarkPaginatedQueryAsync<Match>(1, 10, 10));

        return results;
    }
}
