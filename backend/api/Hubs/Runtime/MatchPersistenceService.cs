using backend.api.Services;
using backend.domain.entities;
using backend.persistence;
using Microsoft.EntityFrameworkCore;

namespace backend.api.Hubs.Runtime;

internal sealed class MatchPersistenceService
{
    private readonly TankxContext context;
    private readonly RedisCacheService cache;

    public MatchPersistenceService(TankxContext context, RedisCacheService cache)
    {
        this.context = context;
        this.cache = cache;
    }

    public async Task<Guid> StartAsync(
        string mapName,
        Region region,
        IReadOnlyCollection<Guid> playerIds,
        CancellationToken cancellationToken = default)
    {
        var existingProfiles = await context.Profiles
            .Where(profile => playerIds.Contains(profile.Id))
            .Select(profile => profile.Id)
            .ToListAsync(cancellationToken);
        if (existingProfiles.Count != playerIds.Count)
            throw new InvalidOperationException("Every room member must have a profile before the match starts.");

        var match = new Match
        {
            StartedAt = DateTime.UtcNow,
            MapName = mapName,
            GameMode = GameMode.Deathmatch,
            Region = region
        };
        context.Matches.Add(match);

        var position = 1;
        foreach (var playerId in playerIds)
        {
            context.MatchParticipants.Add(new MatchParticipant
            {
                MatchId = match.Id,
                ProfileId = playerId,
                Position = position++
            });
        }

        await context.SaveChangesAsync(cancellationToken);
        return match.Id;
    }

    public async Task FinishAsync(
        Guid matchId,
        Guid winnerId,
        IReadOnlyCollection<RuntimePlayer> players,
        CancellationToken cancellationToken = default)
    {
        var match = await context.Matches
            .Include(entity => entity.MatchParticipants)
            .SingleOrDefaultAsync(entity => entity.Id == matchId, cancellationToken)
            ?? throw new InvalidOperationException("The persistent match could not be found.");

        match.FinishedAt = DateTime.UtcNow;
        match.WinnerId = winnerId;

        foreach (var participant in match.MatchParticipants)
        {
            var player = players.Single(runtimePlayer => runtimePlayer.Id == participant.ProfileId);
            participant.Kills = player.Kills;
            participant.Deaths = player.Deaths;
            participant.Score = player.Score;
            participant.Position = player.Id == winnerId ? 1 : player.Placement;
            participant.Accuracy = player.ShotsFired == 0
                ? 0
                : Math.Round(player.ShotsHit * 100d / player.ShotsFired, 2);
            participant.Disconnected = !player.IsConnected;

            await cache.AddScoreAsync(participant.ProfileId, player.Score);
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
