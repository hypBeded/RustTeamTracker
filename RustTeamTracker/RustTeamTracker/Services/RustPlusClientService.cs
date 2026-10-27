
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RustPlusApi;

namespace RustTeamTracker.Services;

public sealed class RustPlusClientService : BackgroundService
{
    private readonly ILogger<RustPlusClientService> _logger;
    private readonly RustPlusDashboardState _dashboard;

    public RustPlusClientService(
        ILogger<RustPlusClientService> logger,
        RustPlusDashboardState dashboard)
    {
        _logger = logger;
        _dashboard = dashboard;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        var path = Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
            "RustTeamTracker",
            "server-connection.txt");

        if (!File.Exists(path))
        {
            _logger.LogError(
                "Не найден файл подключения: {Path}", path);

            _dashboard.SetError();
            return;
        }

        string ip;
        int port;
        ulong playerId;
        int playerToken;

        try
        {
            var settings = File.ReadAllLines(path)
                .Where(line => line.Contains('='))
                .Select(line => line.Split('=', 2))
                .ToDictionary(
                    parts => parts[0].Trim(),
                    parts => parts[1].Trim(),
                    StringComparer.OrdinalIgnoreCase);

            if (!settings.TryGetValue("Ip", out ip!) ||
                !settings.TryGetValue("Port", out var portText) ||
                !settings.TryGetValue("PlayerId", out var playerIdText) ||
                !settings.TryGetValue("PlayerToken", out var tokenText) ||
                !int.TryParse(portText, out port) ||
                !ulong.TryParse(playerIdText, out playerId) ||
                !int.TryParse(tokenText, out playerToken))
            {
                _logger.LogError(
                    "Файл подключения заполнен некорректно.");

                _dashboard.SetError();
                return;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex, "Не удалось прочитать файл подключения.");

            _dashboard.SetError();
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _dashboard.SetConnecting();

                using var client = new RustPlus(
                    ip,
                    port,
                    playerId,
                    playerToken,
                    false);



                _logger.LogInformation(
                    "Подключение к Rust+ {Ip}:{Port}",
                    ip,
                    port);

                await client.ConnectAsync();

                _logger.LogInformation(
                    "Соединение с Rust+ установлено.");

                while (!stoppingToken.IsCancellationRequested)
                {
                    var info = await client.GetInfoAsync();

                    if (!info.IsSuccess || info.Data is null)
                    {
                        _logger.LogWarning(
                            "Не удалось получить информацию сервера: {Error}",
                            info.Error?.Message ?? "нет подробностей");

                        _dashboard.SetDisconnected();
                        break;
                    }

                        _dashboard.SetConnected(
                                info.Data.Name,
                                (int)(info.Data.PlayerCount ?? 0),
                                (int)(info.Data.MaxPlayerCount ?? 0));

                    var teamInfo = await client.GetTeamInfoAsync();

                    if (teamInfo.IsSuccess && teamInfo.Data is not null)
                    {
                        var players = teamInfo.Data.Members.Select(member =>
                            new RustTeamPlayer
                            {
                                SteamId = member.SteamId,
                                Name = member.Name,
                                IsOnline = member.IsOnline,
                                IsAlive = member.IsAlive,
                                IsLeader = member.SteamId == teamInfo.Data.LeaderSteamId
                            });

                        _dashboard.SetTeamPlayers(players);
                    }
                    else
                    {
                        _dashboard.SetTeamError(
                            teamInfo.Error?.Message ?? "Не удалось получить состав команды");
                    }


                    _logger.LogInformation(
                        "Сервер: {Name}; игроков: {Count}/{Max}",
                        info.Data.Name,
                        info.Data.PlayerCount,
                        info.Data.MaxPlayerCount);

                    await Task.Delay(
                        TimeSpan.FromSeconds(30),
                        stoppingToken);
                }
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex, "Соединение с Rust+ потеряно.");

                _dashboard.SetDisconnected();
            }

            if (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation(
                    "Повторная попытка подключения через 15 секунд.");

                await Task.Delay(
                    TimeSpan.FromSeconds(15),
                    stoppingToken);
            }
        }
    }
}
