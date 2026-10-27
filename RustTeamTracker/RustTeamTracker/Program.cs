
using RustTeamTracker.Components;
using Microsoft.EntityFrameworkCore;
using RustTeamTracker.Data;
using RustTeamTracker.Services;
using Microsoft.Extensions.Logging;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.SetMinimumLevel(LogLevel.Debug);
builder.Logging.AddConsole();

// Blazor
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// SQLite
builder.Services.AddDbContextFactory<AppDbContext>(
    options => options.UseSqlite(
        "Data Source=rustteamtracker.db"));

// Application services
builder.Services.AddScoped<PlayerService>();
builder.Services.AddSingleton<RustPlusRegistrationService>();

var rustPlusConfigPath = Path.Combine(
    builder.Environment.ContentRootPath,
    "Config",
    "rustplus.config.json");

builder.Configuration["RustPlus:ConfigPath"] = rustPlusConfigPath;

builder.Services.AddHostedService<RustPlusClientService>();

builder.Services.AddSingleton<RustPlusDashboardState>();






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

app.Run();
