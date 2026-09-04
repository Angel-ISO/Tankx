using System.ComponentModel.DataAnnotations.Schema;

namespace backend.domain.entities;

public class Match : BaseEntity
{
    public DateTime? StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }

    [NotMapped]
    public TimeSpan? Duration => FinishedAt - StartedAt;

    public Guid? WinnerId { get; set; }
    public string MapName { get; set; } = string.Empty;
    public GameMode GameMode { get; set; }
    public Region Region { get; set; } = Region.NorthAmerica;
    public ICollection<MatchParticipant> MatchParticipants { get; set; } = [];
}
