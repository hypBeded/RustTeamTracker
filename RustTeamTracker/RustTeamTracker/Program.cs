
using RustTeamTracker.Components;
using Microsoft.EntityFrameworkCore;
using RustTeamTracker.Data;
using RustTeamTracker.Services;
using Microsoft.Extensions.Logging;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.SetMinimumLevel(LogLevel.Debug);
builder.Logging.AddConsole();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddDbContextFactory<AppDbContext>(
    options => options.UseSqlite("Data Source=rustteamtracker.db"));

builder.Services.AddScoped<PlayerService>();

builder.Services.AddSingleton<RustPlusDashboardState>();
builder.Services.AddSingleton<PlayerActivityService>();
builder.Services.AddSingleton<RustPlusRegistrationService>();
builder.Services.AddSingleton<ServerWipeService>();
builder.Services.AddHostedService<RustPlusClientService>();

var app = builder.Build();

// Ensure database exists
using (var scope = app.Services.CreateScope())
{
    var factory = scope.ServiceProvider
        .GetRequiredService<IDbContextFactory<AppDbContext>>();

    await using var db = await factory.CreateDbContextAsync();
    await db.Database.EnsureCreatedAsync();
}

// HTTP pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler(
        "/Error",
        createScopeForErrors: true);

    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute(
    "/not-found",
    createScopeForStatusCodePages: true);

app.UseHttpsRedirection();
app.UseAntiforgery();

app.MapStaticAssets();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();


using (var scope = app.Services.CreateScope())
{
    var factory = scope.ServiceProvider
        .GetRequiredService<IDbContextFactory<AppDbContext>>();

    await using var db = await factory.CreateDbContextAsync();

    await db.Database.ExecuteSqlRawAsync("""
        CREATE TABLE IF NOT EXISTS ServerWipes (
            Id INTEGER NOT NULL
                CONSTRAINT PK_ServerWipes PRIMARY KEY AUTOINCREMENT,
            WipeTimeUtc TEXT NOT NULL,
            ServerName TEXT NOT NULL,
            FirstDetectedAtUtc TEXT NOT NULL
        );
        """);

    await db.Database.ExecuteSqlRawAsync("""
        CREATE UNIQUE INDEX IF NOT EXISTS
        IX_ServerWipes_WipeTimeUtc
        ON ServerWipes (WipeTimeUtc);
        """);
    await db.Database.ExecuteSqlRawAsync("""
    CREATE TABLE IF NOT EXISTS AfkPeriods (
        Id INTEGER NOT NULL
            CONSTRAINT PK_AfkPeriods PRIMARY KEY AUTOINCREMENT,
        SteamId TEXT NOT NULL,
        StartedAtUtc TEXT NOT NULL,
        EndedAtUtc TEXT NULL
    );
    """);
}


app.Run();
