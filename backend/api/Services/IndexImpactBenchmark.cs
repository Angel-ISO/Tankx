using System.Diagnostics;
using backend.domain.entities;
using backend.persistence;
using Microsoft.EntityFrameworkCore;

namespace backend.api.Services;

public class IndexImpactBenchmark
{
    private readonly TankxContext _context;

    public IndexImpactBenchmark(TankxContext context)
    {
        _context = context;
    }

    public class IndexBenchmarkResult
    {
        public string IndexName { get; set; } = string.Empty;
        public string TableName { get; set; } = string.Empty;
        public double TimeWithoutIndexMs { get; set; }
        public double TimeWithIndexMs { get; set; }
        public double ImprovementPercentage { get; set; }
        public long RecordsAffected { get; set; }
    }

    public async Task<IndexBenchmarkResult> BenchmarkDisplayNameIndex()
    {
        var result = new IndexBenchmarkResult
        {
            IndexName = "IX_Profiles_DisplayName",
            TableName = "Profiles"
        };

        try
        {
            await _context.Database.ExecuteSqlRawAsync("DROP INDEX IF EXISTS \"IX_Profiles_DisplayName\"");

            result.TimeWithoutIndexMs = await MeasureAsync(10, () => _context.Profiles
                .Where(p => p.DisplayName.StartsWith("test"))
                .Select(p => p.Id)
                .ToListAsync());

            await _context.Database.ExecuteSqlRawAsync(
                "CREATE UNIQUE INDEX \"IX_Profiles_DisplayName\" ON \"Profiles\" (\"DisplayName\")");

            result.TimeWithIndexMs = await MeasureAsync(10, () => _context.Profiles
                .Where(p => p.DisplayName.StartsWith("test"))
                .Select(p => p.Id)
                .ToListAsync());
        }
        finally
        {
            await _context.Database.ExecuteSqlRawAsync(
                "CREATE UNIQUE INDEX IF NOT EXISTS \"IX_Profiles_DisplayName\" ON \"Profiles\" (\"DisplayName\")");
        }

        result.RecordsAffected = await _context.Profiles.CountAsync(p => p.DisplayName.StartsWith("test"));
        result.ImprovementPercentage = ((result.TimeWithoutIndexMs - result.TimeWithIndexMs) / result.TimeWithoutIndexMs) * 100;

        return result;
    }

    public async Task<IndexBenchmarkResult> BenchmarkWinnerIdIndex()
    {
        var result = new IndexBenchmarkResult
        {
            IndexName = "IX_Matches_WinnerId",
            TableName = "Matches"
        };

        var winnerId = await _context.Matches
            .Where(m => m.WinnerId != null)
            .Select(m => m.WinnerId)
            .FirstOrDefaultAsync();

        if (winnerId == null)
        {
            result.TimeWithoutIndexMs = 0;
            result.TimeWithIndexMs = 0;
            result.RecordsAffected = 0;
            result.ImprovementPercentage = 0;
            return result;
        }

        try
        {
            await _context.Database.ExecuteSqlRawAsync("DROP INDEX IF EXISTS \"IX_Matches_WinnerId\"");

            result.TimeWithoutIndexMs = await MeasureAsync(10, () => _context.Matches
                .Where(m => m.WinnerId == winnerId)
                .Select(m => m.Id)
                .ToListAsync());

            await _context.Database.ExecuteSqlRawAsync(
                "CREATE INDEX \"IX_Matches_WinnerId\" ON \"Matches\" (\"WinnerId\")");

            result.TimeWithIndexMs = await MeasureAsync(10, () => _context.Matches
                .Where(m => m.WinnerId == winnerId)
                .Select(m => m.Id)
                .ToListAsync());
        }
        finally
        {
            await _context.Database.ExecuteSqlRawAsync(
                "CREATE INDEX IF NOT EXISTS \"IX_Matches_WinnerId\" ON \"Matches\" (\"WinnerId\")");
        }

        result.RecordsAffected = await _context.Matches.CountAsync(m => m.WinnerId == winnerId);
        result.ImprovementPercentage = ((result.TimeWithoutIndexMs - result.TimeWithIndexMs) / result.TimeWithoutIndexMs) * 100;

        return result;
    }

    public async Task<IndexBenchmarkResult> BenchmarkMatchParticipantCompositeIndex()
    {
        var result = new IndexBenchmarkResult
        {
            IndexName = "IX_MatchParticipants_MatchId_ProfileId",
            TableName = "MatchParticipants"
        };

        var testMatchId = await _context.Matches
            .OrderByDescending(m => m.StartedAt)
            .Select(m => m.Id)
            .FirstOrDefaultAsync();

        if (testMatchId == Guid.Empty)
        {
            result.TimeWithoutIndexMs = 0;
            result.TimeWithIndexMs = 0;
            result.RecordsAffected = 0;
            result.ImprovementPercentage = 0;
            return result;
        }

        try
        {
            await _context.Database.ExecuteSqlRawAsync("DROP INDEX IF EXISTS \"IX_MatchParticipants_MatchId_ProfileId\"");

            result.TimeWithoutIndexMs = await MeasureAsync(10, () => _context.MatchParticipants
                .Where(mp => mp.MatchId == testMatchId)
                .Select(mp => mp.Id)
                .ToListAsync());

            await _context.Database.ExecuteSqlRawAsync(
                "CREATE UNIQUE INDEX \"IX_MatchParticipants_MatchId_ProfileId\" ON \"MatchParticipants\" (\"MatchId\", \"ProfileId\")");

            result.TimeWithIndexMs = await MeasureAsync(10, () => _context.MatchParticipants
                .Where(mp => mp.MatchId == testMatchId)
                .Select(mp => mp.Id)
                .ToListAsync());
        }
        finally
        {
            await _context.Database.ExecuteSqlRawAsync(
                "CREATE UNIQUE INDEX IF NOT EXISTS \"IX_MatchParticipants_MatchId_ProfileId\" ON \"MatchParticipants\" (\"MatchId\", \"ProfileId\")");
        }

        result.RecordsAffected = await _context.MatchParticipants.CountAsync(mp => mp.MatchId == testMatchId);
        result.ImprovementPercentage = ((result.TimeWithoutIndexMs - result.TimeWithIndexMs) / result.TimeWithoutIndexMs) * 100;

        return result;
    }

    public async Task<IndexBenchmarkResult> BenchmarkDisplayNameTrgmIndex()
    {
        var result = new IndexBenchmarkResult
        {
            IndexName = "IX_Profiles_DisplayName_trgm",
            TableName = "Profiles"
        };

        await _context.Database.ExecuteSqlRawAsync("CREATE EXTENSION IF NOT EXISTS pg_trgm");

        try
        {
            await _context.Database.ExecuteSqlRawAsync("DROP INDEX IF EXISTS \"IX_Profiles_DisplayName_trgm\"");

            result.TimeWithoutIndexMs = await MeasureAsync(10, () => _context.Profiles
                .Where(p => EF.Functions.ILike(p.DisplayName, "%test%"))
                .Select(p => p.Id)
                .ToListAsync());

            await _context.Database.ExecuteSqlRawAsync(
                "CREATE INDEX \"IX_Profiles_DisplayName_trgm\" ON \"Profiles\" USING GIN (\"DisplayName\" gin_trgm_ops)");

            result.TimeWithIndexMs = await MeasureAsync(10, () => _context.Profiles
                .Where(p => EF.Functions.ILike(p.DisplayName, "%test%"))
                .Select(p => p.Id)
                .ToListAsync());
        }
        finally
        {
            await _context.Database.ExecuteSqlRawAsync(
                "CREATE INDEX IF NOT EXISTS \"IX_Profiles_DisplayName_trgm\" ON \"Profiles\" USING GIN (\"DisplayName\" gin_trgm_ops)");
        }

        result.RecordsAffected = await _context.Profiles.CountAsync(p => EF.Functions.ILike(p.DisplayName, "%test%"));
        result.ImprovementPercentage = ((result.TimeWithoutIndexMs - result.TimeWithIndexMs) / result.TimeWithoutIndexMs) * 100;

        return result;
    }

    private static async Task<double> MeasureAsync(int iterations, Func<Task> operation)
    {
        var times = new List<long>();
        for (int i = 0; i < iterations; i++)
        {
            var stopwatch = Stopwatch.StartNew();
            await operation();
            stopwatch.Stop();
            times.Add(stopwatch.ElapsedMilliseconds);
        }
        return times.Average();
    }

    public async Task<List<IndexBenchmarkResult>> RunAllIndexBenchmarks()
    {
        var results = new List<IndexBenchmarkResult>();

        try
        {
            results.Add(await BenchmarkDisplayNameIndex());
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error benchmarking DisplayName index: {ex.Message}");
        }

        try
        {
            results.Add(await BenchmarkWinnerIdIndex());
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error benchmarking WinnerId index: {ex.Message}");
        }

        try
        {
            results.Add(await BenchmarkMatchParticipantCompositeIndex());
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error benchmarking MatchParticipant composite index: {ex.Message}");
        }

        try
        {
            results.Add(await BenchmarkDisplayNameTrgmIndex());
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error benchmarking DisplayName trigram index: {ex.Message}");
        }

        return results;
    }
}