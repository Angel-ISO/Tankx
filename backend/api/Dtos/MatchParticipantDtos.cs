using System.ComponentModel.DataAnnotations;
using backend.domain.entities;

namespace backend.api.Dtos;

public sealed record MatchParticipantDto(
    Guid Id,
    Guid ProfileId,
    Guid MatchId,
    string DisplayName,
    int Kills,
    int Deaths,
    int Score,
    int Position,
    double Accuracy,
    bool Disconnected)
{
    public static MatchParticipantDto FromEntity(MatchParticipant participant) => new(
        participant.Id,
        participant.ProfileId,
        participant.MatchId,
        participant.Profile?.DisplayName ?? string.Empty,
        participant.Kills,
        participant.Deaths,
        participant.Score,
        participant.Position,
        participant.Accuracy,
        participant.Disconnected);
}

public sealed class CreateMatchParticipantDto
{
    public Guid MatchId { get; init; }

    [Range(0, int.MaxValue)]
    public int Kills { get; init; }

    [Range(0, int.MaxValue)]
    public int Deaths { get; init; }

    [Range(0, int.MaxValue)]
    public int Score { get; init; }

    [Range(0, int.MaxValue)]
    public int Position { get; init; }

    [Range(0, 100)]
    public double Accuracy { get; init; }

    public bool Disconnected { get; init; }
}

public sealed class UpdateMatchParticipantDto
{
    [Range(0, int.MaxValue)]
    public int? Kills { get; init; }

    [Range(0, int.MaxValue)]
    public int? Deaths { get; init; }

    [Range(0, int.MaxValue)]
    public int? Score { get; init; }

    [Range(0, int.MaxValue)]
    public int? Position { get; init; }

    [Range(0, 100)]
    public double? Accuracy { get; init; }

    public bool? Disconnected { get; init; }
}
