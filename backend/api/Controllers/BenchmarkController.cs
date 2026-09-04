using backend.api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace backend.api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BenchmarkController : BaseApiController
{
    private readonly QueryBenchmarkService _benchmarkService;
    private readonly IndexAnalysisService _indexAnalysisService;
    private readonly IndexImpactBenchmark _indexImpactBenchmark;

    public BenchmarkController(QueryBenchmarkService benchmarkService, IndexAnalysisService indexAnalysisService, IndexImpactBenchmark indexImpactBenchmark)
    {
        _benchmarkService = benchmarkService;
        _indexAnalysisService = indexAnalysisService;
        _indexImpactBenchmark = indexImpactBenchmark;
    }

    [HttpGet("run-comprehensive")]
    public async Task<ActionResult> RunComprehensiveBenchmark()
    {
        try
        {
            var results = await _benchmarkService.RunComprehensiveBenchmarkAsync();
            return Ok(new
            {
                success = true,
                data = results,
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, error = ex.Message });
        }
    }

    [HttpGet("tolist-vs-asnotracking/{entityType}")]
    public async Task<ActionResult> CompareToListVsAsNoTracking(string entityType)
    {
        try
        {
            var results = new List<object>();
            
            entityType = entityType.ToLower();
            
            if (entityType == "profile")
            {
                var toListResult = await _benchmarkService.BenchmarkToListAsync<backend.domain.entities.Profile>(10);
                var asNoTrackingResult = await _benchmarkService.BenchmarkAsNoTrackingAsync<backend.domain.entities.Profile>(10);
                results.Add(toListResult);
                results.Add(asNoTrackingResult);
            }
            else if (entityType == "match")
            {
                var toListResult = await _benchmarkService.BenchmarkToListAsync<backend.domain.entities.Match>(10);
                var asNoTrackingResult = await _benchmarkService.BenchmarkAsNoTrackingAsync<backend.domain.entities.Match>(10);
                results.Add(toListResult);
                results.Add(asNoTrackingResult);
            }
            else
            {
                return BadRequest(new { success = false, error = "Invalid entity type. Use 'profile' or 'match'" });
            }

            return Ok(new
            {
                success = true,
                data = results,
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, error = ex.Message });
        }
    }

    [HttpGet("pagination/{entityType}")]
    public async Task<ActionResult> BenchmarkPagination(string entityType, [FromQuery] int pageIndex = 1, [FromQuery] int pageSize = 10)
    {
        try
        {
            QueryBenchmarkService.BenchmarkResult result;
            
            entityType = entityType.ToLower();
            
            if (entityType == "profile")
            {
                result = await _benchmarkService.BenchmarkPaginatedQueryAsync<backend.domain.entities.Profile>(pageIndex, pageSize, 10);
            }
            else if (entityType == "match")
            {
                result = await _benchmarkService.BenchmarkPaginatedQueryAsync<backend.domain.entities.Match>(pageIndex, pageSize, 10);
            }
            else
            {
                return BadRequest(new { success = false, error = "Invalid entity type. Use 'profile' or 'match'" });
            }

            return Ok(new
            {
                success = true,
                data = result,
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, error = ex.Message });
        }
    }

    [HttpGet("index-impact")]
    public async Task<ActionResult> RunIndexImpactBenchmark()
    {
        try
        {
            var results = await _indexImpactBenchmark.RunAllIndexBenchmarks();
            return Ok(new
            {
                success = true,
                data = results,
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, error = ex.Message });
        }
    }

    [HttpPost("seed")]
    public async Task<ActionResult> SeedBenchmarkData([FromQuery] int profiles = 1000, [FromQuery] int matches = 100)
    {
        try
        {
            var result = await _benchmarkService.SeedBenchmarkDataAsync(profiles, matches);
            return Ok(new
            {
                success = true,
                data = new { profiles = result.Profiles, matches = result.Matches, participants = result.Participants },
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, error = ex.Message });
        }
    }

    [HttpDelete("seed")]
    public async Task<ActionResult> CleanupBenchmarkData()
    {
        try
        {
            var deleted = await _benchmarkService.CleanupBenchmarkDataAsync();
            return Ok(new
            {
                success = true,
                data = new { deletedProfiles = deleted },
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, error = ex.Message });
        }
    }

    [HttpGet("bulk-vs-standard")]
    public async Task<ActionResult> BenchmarkBulkVsStandard([FromQuery] int recordCount = 1000, [FromQuery] int iterations = 5)
    {
        try
        {
            var results = await _benchmarkService.BenchmarkBulkVsStandardAsync(recordCount, iterations);
            return Ok(new
            {
                success = true,
                data = results,
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, error = ex.Message });
        }
    }

    [HttpGet("search/{entityType}")]
    public async Task<ActionResult> BenchmarkSearch(string entityType, [FromQuery] string search = "test")
    {
        try
        {
            var results = new List<QueryBenchmarkService.BenchmarkResult>();

            entityType = entityType.ToLower();

            if (entityType == "profile")
            {
                var result = await _benchmarkService.BenchmarkSearchQueryAsync<backend.domain.entities.Profile>(
                    query => query.Where(p => EF.Functions.ILike(p.DisplayName, $"%{search}%")),
                    10);
                results.Add(result);
            }
            else if (entityType == "match")
            {
                var result = await _benchmarkService.BenchmarkSearchQueryAsync<backend.domain.entities.Match>(
                    query => query.Where(m => EF.Functions.ILike(m.MapName, $"%{search}%")),
                    10);
                results.Add(result);
            }
            else
            {
                return BadRequest(new { success = false, error = "Invalid entity type. Use 'profile' or 'match'" });
            }

            return Ok(new
            {
                success = true,
                data = results,
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, error = ex.Message });
        }
    }

    [HttpGet("indexes")]
    public async Task<ActionResult> GetAllIndexes()
    {
        try
        {
            var indexes = await _indexAnalysisService.GetAllIndexesAsync();
            return Ok(new
            {
                success = true,
                data = indexes,
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, error = ex.Message });
        }
    }

    [HttpGet("index-statistics")]
    public async Task<ActionResult> GetIndexStatistics()
    {
        try
        {
            var stats = await _indexAnalysisService.GetIndexStatisticsAsync();
            return Ok(new
            {
                success = true,
                data = stats,
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, error = ex.Message });
        }
    }

    [HttpGet("query-plan/{queryType}")]
    public async Task<ActionResult> AnalyzeQueryPlan(string queryType)
    {
        try
        {
            IndexAnalysisService.QueryPlanInfo result;
            
            queryType = queryType.ToLower();
            
            switch (queryType)
            {
                case "displayname":
                    result = await _indexAnalysisService.AnalyzeDisplayNameSearchAsync();
                    break;
                case "winnerid":
                    result = await _indexAnalysisService.AnalyzeWinnerIdQueryAsync();
                    break;
                case "matchparticipant":
                    result = await _indexAnalysisService.AnalyzeMatchParticipantQueryAsync();
                    break;
                default:
                    return BadRequest(new { success = false, error = "Invalid query type. Use 'displayname', 'winnerid', or 'matchparticipant'" });
            }

            return Ok(new
            {
                success = true,
                data = result,
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, error = ex.Message });
        }
    }
}
