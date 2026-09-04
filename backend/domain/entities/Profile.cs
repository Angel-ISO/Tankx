namespace backend.domain.entities;

public class Profile : BaseEntity
{
    public string DisplayName { get; set; } = string.Empty;
    public string TankType { get; set; } = "tank_green";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<MatchParticipant> MatchParticipants { get; set; } = [];
}
