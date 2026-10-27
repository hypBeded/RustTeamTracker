
namespace RustTeamTracker.Models;

public class Player
{
    public int Id { get; set; }

    public string SteamId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public DateTime FirstSeenUtc { get; set; } =
        DateTime.UtcNow;

    public List<PlayerSession> Sessions { get; set; } = [];
}
