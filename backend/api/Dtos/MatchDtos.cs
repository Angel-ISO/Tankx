using System.ComponentModel.DataAnnotations;
using backend.domain.entities;

namespace backend.api.Dtos;

public sealed record MatchDto(
    Guid Id,
    DateTime? StartedAt,
    DateTime? FinishedAt,
    double? DurationSeconds,
    Guid? WinnerId,
    string MapName,
    GameMode GameMode,
    Region Region,
    int ParticipantCount,
    List<MatchParticipantDto> Participants)
{
    public static MatchDto FromEntity(Match match) => new(
        match.Id,
        match.StartedAt,
        match.FinishedAt,
        match.Duration?.TotalSeconds,
        match.WinnerId,
        match.MapName,
        match.GameMode,
        match.Region,
        match.MatchParticipants.Count,
        match.MatchParticipants.Select(MatchParticipantDto.FromEntity).ToList());
}

public sealed class CreateMatchDto
{
    [Required, StringLength(50, MinimumLength = 1)]
    public string MapName { get; init; } = string.Empty;

    [EnumDataType(typeof(GameMode))]
    public GameMode GameMode { get; init; }
}

public sealed class UpdateMatchDto
{
    [StringLength(50, MinimumLength = 1)]
    public string? MapName { get; init; }

    [EnumDataType(typeof(GameMode))]
    public GameMode? GameMode { get; init; }

    public DateTime? FinishedAt { get; init; }
    public Guid? WinnerId { get; init; }
}
