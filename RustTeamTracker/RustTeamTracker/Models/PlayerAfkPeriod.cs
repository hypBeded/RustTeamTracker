
namespace RustTeamTracker.Models;

public sealed class PlayerAfkPeriod
{
    public int Id { get; set; }

    public string SteamId { get; set; } = string.Empty;

    public DateTime StartedAtUtc { get; set; }

    public DateTime? EndedAtUtc { get; set; }
}
