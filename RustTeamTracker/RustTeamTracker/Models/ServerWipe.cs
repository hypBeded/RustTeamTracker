
namespace RustTeamTracker.Models;

public sealed class ServerWipe
{
    public int Id { get; set; }

    public DateTime WipeTimeUtc { get; set; }

    public string ServerName { get; set; } = string.Empty;

    public DateTime FirstDetectedAtUtc { get; set; } = DateTime.UtcNow;
}
