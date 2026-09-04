namespace backend.domain.entities;

public class MatchParticipant : BaseEntity
{
    public int Kills { get; set; }
    public int Deaths { get; set; }
    public int Score { get; set; }
    public int Position { get; set; }
    public double Accuracy { get; set; }
    public bool Disconnected { get; set; }
    public Guid ProfileId { get; set; }
    public Profile Profile { get; set; } = null!;
    public Guid MatchId { get; set; }
    public Match Match { get; set; } = null!;
}
