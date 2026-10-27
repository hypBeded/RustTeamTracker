
namespace RustTeamTracker.Models;

public class PlayerSession
{
    public int Id { get; set; }

    public int PlayerId { get; set; }

    public Player Player { get; set; } = null!;

    public DateTime StartedAtUtc { get; set; }

    public DateTime? EndedAtUtc { get; set; }

    public int AfkSeconds { get; set; }
}
