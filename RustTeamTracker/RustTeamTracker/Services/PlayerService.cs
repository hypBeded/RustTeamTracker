
using Microsoft.EntityFrameworkCore;
using RustTeamTracker.Data;
using RustTeamTracker.Models;

namespace RustTeamTracker.Services;

public class PlayerService(
    IDbContextFactory<AppDbContext> dbFactory)
{
    public async Task<List<Player>> GetPlayersAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();

        return await db.Players
            .AsNoTracking()
            .OrderBy(p => p.Name)
            .ToListAsync();
    }

    public async Task AddPlayerAsync(string name, string steamId)
    {
        name = name.Trim();
        steamId = steamId.Trim();

        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Укажи имя игрока.");

        if (string.IsNullOrWhiteSpace(steamId))
            throw new ArgumentException("Укажи Steam ID.");

        await using var db = await dbFactory.CreateDbContextAsync();

        bool exists = await db.Players
            .AnyAsync(p => p.SteamId == steamId);

        if (exists)
            throw new InvalidOperationException(
                "Игрок с таким Steam ID уже добавлен.");

        db.Players.Add(new Player
        {
            Name = name,
            SteamId = steamId
        });

        await db.SaveChangesAsync();
    }
}
