using System.Diagnostics;
using backend.api.Services;
using backend.domain.entities;
using backend.persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace backend.api.Controllers;

[Authorize]
public sealed class RedisCacheController : BaseApiController
{
    private readonly RedisCacheService cache;
    private readonly TankxContext context;

    public RedisCacheController(RedisCacheService cache, TankxContext context)
    {
        this.cache = cache;
        this.context = context;
    }

    [HttpGet("online")]
    public async Task<ActionResult> GetOnlinePlayers()
    {
        var players = await cache.GetOnlinePlayersAsync();
        return Ok(new
        {
            success = true,
            data = new
            {
                count = players.Count,
                playerIds = players
            }
        });
    }

    [HttpGet("leaderboard/top")]
    public async Task<ActionResult> GetTopScores([FromQuery] int count = 10)
    {
        var fromCache = await cache.GetTopScoresAsync(count);
        var hit = fromCache.Count > 0;

        if (!hit)
        {
            var profiles = await context.Profiles
                .AsNoTracking()
                .Select(profile => new
                {
                    profile.Id,
                    profile.DisplayName,
                    Score = profile.MatchParticipants.Sum(participant => (int?)participant.Score) ?? 0
                })
                .OrderByDescending(entry => entry.Score)
                .Take(count)
                .ToListAsync();

            return Ok(new
            {
                success = true,
                source = "postgres",
                data = profiles.Select(profile => new { profile.Id, profile.DisplayName, profile.Score })
            });
        }

        var ids = fromCache.Select(entry => entry.Member).ToList();
        var names = await context.Profiles
            .AsNoTracking()
            .Where(profile => ids.Contains(profile.Id.ToString()))
            .Select(profile => new { profile.Id, profile.DisplayName })
            .ToDictionaryAsync(entry => entry.Id.ToString(), entry => entry.DisplayName);

        return Ok(new
        {
            success = true,
            source = "redis",
            data = fromCache.Select(entry => new
            {
                ProfileId = entry.Member,
                DisplayName = names.GetValueOrDefault(entry.Member, entry.Member),
                Score = entry.Score
            })
        });
    }

    [HttpGet("sessions/active")]
    public async Task<ActionResult> GetActiveSessions()
    {
        var count = await cache.GetActiveSessionCountAsync();
        return Ok(new
        {
            success = true,
            data = new { activeSessions = count }
        });
    }

    [HttpGet("benchmark")]
    public async Task<ActionResult> BenchmarkCacheVsDatabase()
    {
        var onlineCacheTimes = new List<long>();
        var onlineDbTimes = new List<long>();
        var leaderboardCacheTimes = new List<long>();
        var leaderboardDbTimes = new List<long>();

        for (int i = 0; i < 10; i++)
        {
            var stopwatch = Stopwatch.StartNew();
            await cache.GetOnlinePlayerCountAsync();
            stopwatch.Stop();
            onlineCacheTimes.Add(stopwatch.ElapsedMilliseconds);
        }

        for (int i = 0; i < 10; i++)
        {
            var stopwatch = Stopwatch.StartNew();
            await context.Profiles.CountAsync();
            stopwatch.Stop();
            onlineDbTimes.Add(stopwatch.ElapsedMilliseconds);
        }

        for (int i = 0; i < 10; i++)
        {
            var stopwatch = Stopwatch.StartNew();
            await cache.GetTopScoresAsync(10);
            stopwatch.Stop();
            leaderboardCacheTimes.Add(stopwatch.ElapsedMilliseconds);
        }

        for (int i = 0; i < 10; i++)
        {
            var stopwatch = Stopwatch.StartNew();
            await context.Profiles
                .AsNoTracking()
                .Select(profile => new
                {
                    profile.Id,
                    Score = profile.MatchParticipants.Sum(participant => (int?)participant.Score) ?? 0
                })
                .OrderByDescending(entry => entry.Score)
                .Take(10)
                .ToListAsync();
            stopwatch.Stop();
            leaderboardDbTimes.Add(stopwatch.ElapsedMilliseconds);
        }

        return Ok(new
        {
            success = true,
            data = new
            {
                onlinePlayers = new
                {
                    redis = new
                    {
                        averageMs = onlineCacheTimes.Average(),
                        minMs = onlineCacheTimes.Min(),
                        maxMs = onlineCacheTimes.Max(),
                        iterations = 10
                    },
                    postgres = new
                    {
                        averageMs = onlineDbTimes.Average(),
                        minMs = onlineDbTimes.Min(),
                        maxMs = onlineDbTimes.Max(),
                        iterations = 10
                    }
                },
                leaderboardTop10 = new
                {
                    redis = new
                    {
                        averageMs = leaderboardCacheTimes.Average(),
                        minMs = leaderboardCacheTimes.Min(),
                        maxMs = leaderboardCacheTimes.Max(),
                        iterations = 10
                    },
                    postgres = new
                    {
                        averageMs = leaderboardDbTimes.Average(),
                        minMs = leaderboardDbTimes.Min(),
                        maxMs = leaderboardDbTimes.Max(),
                        iterations = 10
                    }
                }
            }
        });
    }

    [HttpPost("leaderboard/seed")]
    public async Task<ActionResult> SeedLeaderboard([FromQuery] int count = 25)
    {
        var profiles = await context.Profiles
            .AsNoTracking()
            .Take(count)
            .ToListAsync();

        foreach (var profile in profiles)
        {
            var score = new Random(profile.Id.GetHashCode()).Next(100, 5000);
            await cache.AddScoreAsync(profile.Id, score);
        }

        return Ok(new
        {
            success = true,
            data = new { seeded = profiles.Count }
        });
    }

    [HttpDelete("leaderboard")]
    public async Task<ActionResult> ClearLeaderboard()
    {
        await cache.ClearLeaderboardAsync();
        return Ok(new { success = true });
    }
}