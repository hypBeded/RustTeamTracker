
using Microsoft.EntityFrameworkCore;
using RustTeamTracker.Models;

namespace RustTeamTracker.Data;


public class AppDbContext(
    DbContextOptions<AppDbContext> options)
    : DbContext(options)
{
    public DbSet<Player> Players => Set<Player>();
    public DbSet<ServerWipe> ServerWipes => Set<ServerWipe>();
    public DbSet<PlayerSession> Sessions =>
        Set<PlayerSession>();
    public DbSet<PlayerAfkPeriod> AfkPeriods => Set<PlayerAfkPeriod>();
    protected override void OnModelCreating(
        ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ServerWipe>()
            .HasIndex(w => w.WipeTimeUtc)
            .IsUnique();

        modelBuilder.Entity<Player>()
            .HasIndex(p => p.SteamId)
            .IsUnique();

        modelBuilder.Entity<PlayerSession>()
            .HasOne(s => s.Player)
            .WithMany(p => p.Sessions)
            .HasForeignKey(s => s.PlayerId);

        modelBuilder.Entity<PlayerSession>()
            .HasIndex(s => s.StartedAtUtc);
    }
}
