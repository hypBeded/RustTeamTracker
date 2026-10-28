
using Microsoft.EntityFrameworkCore;
using RustTeamTracker.Data;
using RustTeamTracker.Models;

namespace RustTeamTracker.Services;

public sealed class PlayerActivityService(
    IDbContextFactory<AppDbContext> dbFactory)
{
    public async Task UpdateOnlinePlayersAsync(
        IEnumerable<(string SteamId, string Name)> onlinePlayers,
        CancellationToken cancellationToken = default)
    {
        await using var db =
            await dbFactory.CreateDbContextAsync(cancellationToken);

        var now = DateTime.UtcNow;
        var online = onlinePlayers.ToDictionary(
            p => p.SteamId,
            p => p.Name);

        var players = await db.Players
            .ToListAsync(cancellationToken);

        foreach (var item in online)
        {
            var player = players.FirstOrDefault(
                p => p.SteamId == item.Key);

            if (player is null)
            {
                player = new Player
                {
                    SteamId = item.Key,
                    Name = item.Value,
                    FirstSeenUtc = now
                };

                db.Players.Add(player);
                await db.SaveChangesAsync(cancellationToken);
                players.Add(player);
            }
            else
            {
                player.Name = item.Value;
            }

            var hasOpenSession = await db.Sessions.AnyAsync(
                s => s.PlayerId == player.Id &&
                     s.EndedAtUtc == null,
                cancellationToken);

            if (!hasOpenSession)
            {
                db.Sessions.Add(new PlayerSession
                {
                    PlayerId = player.Id,
                    StartedAtUtc = now,
                    AfkSeconds = 0
                });
            }
        }

        var openSessions = await db.Sessions
            .Include(s => s.Player)
            .Where(s => s.EndedAtUtc == null)
            .ToListAsync(cancellationToken);

        foreach (var session in openSessions)
        {
            if (!online.ContainsKey(session.Player.SteamId))
                session.EndedAtUtc = now;
        }

        await db.SaveChangesAsync(cancellationToken);
    } // UpdateOnlinePlayersAsync заканчивается здесь

    public async Task<Dictionary<string, int>> GetWipeOnlineSecondsAsync(
        DateTime wipeStartedAtUtc,
        CancellationToken cancellationToken = default)
    {
        await using var db =
            await dbFactory.CreateDbContextAsync(cancellationToken);

        var now = DateTime.UtcNow;

        var sessions = await db.Sessions
            .AsNoTracking()
            .Where(s =>
                (s.EndedAtUtc == null ||
                 s.EndedAtUtc > wipeStartedAtUtc) &&
                s.StartedAtUtc < now)
            .Select(s => new
            {
                s.Player.SteamId,
                s.StartedAtUtc,
                s.EndedAtUtc
            })
            .ToListAsync(cancellationToken);

        return sessions
            .GroupBy(s => s.SteamId)
            .ToDictionary(
                group => group.Key,
                group => group.Sum(s =>
                {
                    var start = s.StartedAtUtc < wipeStartedAtUtc
                        ? wipeStartedAtUtc
                        : s.StartedAtUtc;

                    var end = s.EndedAtUtc.HasValue &&
                              s.EndedAtUtc.Value < now
                        ? s.EndedAtUtc.Value
                        : now;

                    return end > start
                        ? (int)(end - start).TotalSeconds
                        : 0;
                }));
    }

    private sealed class PositionState
    {
        public float X { get; set; }
        public float Y { get; set; }
        public DateTime LastMovedAtUtc { get; set; }
        public bool IsAfk { get; set; }
    }

    private readonly Dictionary<string, PositionState> _positions = new();
    private readonly SemaphoreSlim _afkLock = new(1, 1);

    private static readonly TimeSpan AfkThreshold =
        TimeSpan.FromMinutes(5);

    private const float MovementThreshold = 2f;

    public async Task UpdatePlayerPositionsAsync(
        IEnumerable<(string SteamId, string Name, bool IsOnline,
            float X, float Y)> playerPositions,
        CancellationToken cancellationToken = default)
    {
        await _afkLock.WaitAsync(cancellationToken);

        try
        {
            await using var db =
                await dbFactory.CreateDbContextAsync(cancellationToken);

            var now = DateTime.UtcNow;

            foreach (var player in playerPositions.Where(p => p.IsOnline))
            {
                if (!_positions.TryGetValue(player.SteamId, out var state))
                {
                    _positions[player.SteamId] = new PositionState
                    {
                        X = player.X,
                        Y = player.Y,
                        LastMovedAtUtc = now
                    };

                    continue;
                }

                var dx = player.X - state.X;
                var dy = player.Y - state.Y;
                var moved = Math.Sqrt(dx * dx + dy * dy)
                            >= MovementThreshold;

                if (moved)
                {
                    if (state.IsAfk)
                    {
                        var openPeriod = await db.AfkPeriods
                            .Where(p => p.SteamId == player.SteamId
                                        && p.EndedAtUtc == null)
                            .OrderByDescending(p => p.StartedAtUtc)
                            .FirstOrDefaultAsync(cancellationToken);

                        if (openPeriod is not null)
                            openPeriod.EndedAtUtc = now;
                    }

                    state.X = player.X;
                    state.Y = player.Y;
                    state.LastMovedAtUtc = now;
                    state.IsAfk = false;
                }
                else if (!state.IsAfk &&
                         now - state.LastMovedAtUtc >= AfkThreshold)
                {
                    var afkStart = state.LastMovedAtUtc + AfkThreshold;

                    db.AfkPeriods.Add(new PlayerAfkPeriod
                    {
                        SteamId = player.SteamId,
                        StartedAtUtc = afkStart
                    });

                    state.IsAfk = true;
                }
            }

            await db.SaveChangesAsync(cancellationToken);
        }
        finally
        {
            _afkLock.Release();
        }
    }

    public async Task<Dictionary<string, int>> GetWipeAfkSecondsAsync(
        DateTime wipeStartedAtUtc,
        CancellationToken cancellationToken = default)
    {
        await using var db =
            await dbFactory.CreateDbContextAsync(cancellationToken);

        var now = DateTime.UtcNow;

        var periods = await db.AfkPeriods
            .AsNoTracking()
            .Where(p =>
                (p.EndedAtUtc == null || p.EndedAtUtc > wipeStartedAtUtc)
                && p.StartedAtUtc < now)
            .Select(p => new
            {
                p.SteamId,
                p.StartedAtUtc,
                p.EndedAtUtc
            })
            .ToListAsync(cancellationToken);

        return periods
            .GroupBy(p => p.SteamId)
            .ToDictionary(
                group => group.Key,
                group => group.Sum(p =>
                {
                    var start = p.StartedAtUtc < wipeStartedAtUtc
                        ? wipeStartedAtUtc
                        : p.StartedAtUtc;

                    var end = p.EndedAtUtc.HasValue &&
                              p.EndedAtUtc.Value < now
                        ? p.EndedAtUtc.Value
                        : now;

                    return end > start
                        ? (int)(end - start).TotalSeconds
                        : 0;
                }));
    }
    public async Task<HashSet<string>> GetCurrentlyAfkSteamIdsAsync(
    CancellationToken cancellationToken = default)
    {
        await using var db =
            await dbFactory.CreateDbContextAsync(cancellationToken);

        var steamIds = await db.AfkPeriods
            .AsNoTracking()
            .Where(p => p.EndedAtUtc == null)
            .Select(p => p.SteamId)
            .ToListAsync(cancellationToken);

        return steamIds.ToHashSet();
    }
}
