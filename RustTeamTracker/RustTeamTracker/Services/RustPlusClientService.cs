
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RustPlusApi;
using RustTeamTracker.Models;

namespace RustTeamTracker.Services;

public sealed class RustPlusClientService : BackgroundService
{
    private readonly ILogger<RustPlusClientService> _logger;
    private readonly RustPlusDashboardState _dashboard;
    private readonly PlayerActivityService _activity;
    private readonly ServerWipeService _wipeService;
    public RustPlusClientService(
        ILogger<RustPlusClientService> logger,
        RustPlusDashboardState dashboard,
        PlayerActivityService activity,
        ServerWipeService wipeService)
    {
        _logger = logger;
        _dashboard = dashboard;
        _activity = activity;
        _wipeService = wipeService;
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

                await client.ConnectAsync();

                // ТЕСТ: отправляем сообщение в командный чат Rust
                var result = await client.SendTeamMessageAsync(
                    "RustTeamTracker подключился к серверу"
                );

                _logger.LogInformation(
                    "Результат отправки сообщения в чат: {@Result}",
                    result
                );


                _logger.LogInformation(
                    "Подключение к Rust+ {Ip}:{Port}",
                    ip,
                    port);

                await client.ConnectAsync();

                while (!stoppingToken.IsCancellationRequested)
                {
                    // Здесь остаётся твой существующий код опроса сервера
                }


                _logger.LogInformation(
                    "Соединение с Rust+ установлено.");

                while (!stoppingToken.IsCancellationRequested)
                {
                    var info = await client.GetInfoAsync();

                    var markers = await client.GetMapMarkersAsync();

                    if (markers.IsSuccess && markers.Data is not null)
                    {
                        _logger.LogInformation(
                            "Тип данных маркеров: {Type}",
                            markers.Data.GetType().FullName);

                        foreach (var property in markers.Data.GetType().GetProperties())
                        {
                            try
                            {
                                var value = property.GetValue(markers.Data);

                                if (value is System.Collections.IDictionary dictionary)
                                {
                                    foreach (System.Collections.DictionaryEntry entry in dictionary)
                                    {
                                        if (entry.Value is null)
                                            continue;

                                        _logger.LogInformation(
                                            "Тип маркера {Property}: {Type}",
                                            property.Name,
                                            entry.Value.GetType().FullName);

                                        foreach (var markerProperty in entry.Value.GetType().GetProperties())
                                        {
                                            object? markerValue;

                                            try
                                            {
                                                markerValue = markerProperty.GetValue(entry.Value);
                                            }
                                            catch
                                            {
                                                continue;
                                            }

                                            _logger.LogInformation(
                                                "  {MarkerProperty} = {Value}",
                                                markerProperty.Name,
                                                markerValue);
                                        }
                                    }
                                }

                                _logger.LogInformation(
                                    "Маркер карты: {Property} = {Value}",
                                    property.Name,
                                    value);

                                if (value is System.Collections.IEnumerable items
                                    && value is not string)
                                {
                                    foreach (var item in items)
                                    {
                                        if (item is null)
                                            continue;

                                        _logger.LogInformation(
                                            "Элемент {Property}: {Value}",
                                            property.Name,
                                            System.Text.Json.JsonSerializer.Serialize(item));
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogDebug(
                                    ex,
                                    "Не удалось прочитать свойство {Property}",
                                    property.Name);
                            }
                        }
                    }
                    else
                    {
                        _logger.LogWarning(
                            "Не удалось получить маркеры: {Error}",
                            markers.Error?.Message ?? "нет подробностей");
                    }

                    if (info.Data.WipeTime is DateTime wipeTime)
                    {
                        await _wipeService.SaveWipeIfMissingAsync(
                            wipeTime,
                            info.Data.Name,
                            stoppingToken);
                    }

                    if (info.IsSuccess && info.Data is not null)
                    {
                        _logger.LogInformation(
                            "Тип данных сервера: {Type}",
                            info.Data.GetType().FullName);

                        foreach (var property in info.Data.GetType().GetProperties())
                        {
                            if (property.Name.Contains(
                                    "wipe",
                                    StringComparison.OrdinalIgnoreCase))
                            {
                                object? value = null;

                                try
                                {
                                    value = property.GetValue(info.Data);
                                }
                                catch
                                {
                                    // Игнорируем ошибки чтения диагностического свойства.
                                }

                                _logger.LogInformation(
                                    "Поле вайпа: {Property} = {Value}",
                                    property.Name,
                                    value);
                            }
                        }
                    }

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
                        var members = teamInfo.Data.Members.ToList();

                        var firstMember = members.FirstOrDefault();

                        if (firstMember is not null)
                        {
                            foreach (var property in firstMember.GetType().GetProperties())
                            {
                                _logger.LogInformation(
                                    "Свойство игрока: {Property} = {Value}",
                                    property.Name,
                                    property.GetValue(firstMember));
                            }
                        }


                        var players = members.Select(member =>
                                new RustTeamPlayer
                                {
                                    SteamId = member.SteamId,
                                    Name = member.Name,
                                    IsOnline = member.IsOnline,
                                    IsAlive = member.IsAlive,
                                    IsLeader = member.SteamId == teamInfo.Data.LeaderSteamId,
                                    X = member.X,
                                    Y = member.Y
                                }).ToList();

                        _dashboard.SetTeamPlayers(players);
                        _dashboard.SetTeamError(null);

                        // Записываем только игроков, которых Rust+ считает онлайн.
                        var onlinePlayers = members
                            .Where(member => member.IsOnline)
                            .Select(member => (
                                SteamId: member.SteamId.ToString(),
                                Name: member.Name))
                            .ToList();

                        await _activity.UpdateOnlinePlayersAsync(
                            onlinePlayers,
                            stoppingToken);
                        await _activity.UpdatePlayerPositionsAsync(
                            members.Select(member => (
                            SteamId: member.SteamId.ToString(),
                            Name: member.Name,
                            IsOnline: member.IsOnline,
                            X: (float)member.X,
                            Y: (float)member.Y)),
                            stoppingToken);
                    }
                    else
                    {
                        // При ошибке не закрываем сессии:
                        // мы не знаем, кто действительно вышел.
                        _dashboard.SetTeamError(
                            teamInfo.Error?.Message ??
                            "Не удалось получить состав команды");
                    }



                    _logger.LogInformation(
                        "Сервер: {Name}; игроков: {Count}/{Max}",
                        info.Data.Name,
                        info.Data.PlayerCount,
                        info.Data.MaxPlayerCount);

                    await Task.Delay(
                        TimeSpan.FromSeconds(5),
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
