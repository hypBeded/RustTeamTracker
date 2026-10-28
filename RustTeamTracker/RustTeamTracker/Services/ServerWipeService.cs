
using Microsoft.EntityFrameworkCore;
using RustTeamTracker.Data;
using RustTeamTracker.Models;

namespace RustTeamTracker.Services;

public sealed class ServerWipeService(
    IDbContextFactory<AppDbContext> dbFactory)
{
    public async Task SaveWipeIfMissingAsync(
        DateTime wipeTime,
        string serverName,
        CancellationToken cancellationToken = default)
    {
        var wipeUtc = wipeTime.Kind switch
        {
            DateTimeKind.Utc => wipeTime,
            DateTimeKind.Local => wipeTime.ToUniversalTime(),
            _ => DateTime.SpecifyKind(wipeTime, DateTimeKind.Utc)
        };

        await using var db =
            await dbFactory.CreateDbContextAsync(cancellationToken);

        var exists = await db.ServerWipes.AnyAsync(
            w => w.WipeTimeUtc == wipeUtc,
            cancellationToken);

        if (exists)
            return;

        db.ServerWipes.Add(new ServerWipe
        {
            WipeTimeUtc = wipeUtc,
            ServerName = serverName,
            FirstDetectedAtUtc = DateTime.UtcNow
        });

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<ServerWipe>> GetWipesAsync(
        CancellationToken cancellationToken = default)
    {
        await using var db =
            await dbFactory.CreateDbContextAsync(cancellationToken);

        return await db.ServerWipes
            .AsNoTracking()
            .OrderByDescending(w => w.WipeTimeUtc)
            .ToListAsync(cancellationToken);
    }
}
