
using System;
using System.Collections.Generic;

namespace RustTeamTracker.Services;

public sealed class RustTeamPlayer
{
    public ulong SteamId { get; set; }
    public string Name { get; set; } = "";
    public bool IsOnline { get; set; }
    public bool IsAlive { get; set; }
    public bool IsLeader { get; set; }
}

public sealed class RustPlusDashboardState
{
    public string ConnectionStatus { get; private set; } = "Подключение...";
    public string ServerName { get; private set; } = "Неизвестный сервер";
    public int ServerPlayerCount { get; private set; }
    public int ServerMaxPlayerCount { get; private set; }
    public DateTime? LastUpdated { get; private set; }

    public List<RustTeamPlayer> TeamPlayers { get; private set; } = new();
    public string? TeamError { get; private set; }

    public void SetConnecting()
    {
        ConnectionStatus = "Подключение...";
    }

    public void SetConnected(
        string serverName,
        int playerCount,
        int maxPlayerCount)
    {
        ConnectionStatus = "Подключено";
        ServerName = serverName;
        ServerPlayerCount = playerCount;
        ServerMaxPlayerCount = maxPlayerCount;
        LastUpdated = DateTime.Now;
    }

    public void SetTeamPlayers(IEnumerable<RustTeamPlayer> players)
    {
        TeamPlayers = new List<RustTeamPlayer>(players);
        TeamError = null;
        LastUpdated = DateTime.Now;
    }

    public void SetTeamError(string message)
    {
        TeamError = message;
    }

    public void SetDisconnected()
    {
        ConnectionStatus = "Переподключение";
    }

    public void SetError()
    {
        ConnectionStatus = "Ошибка подключения";
    }
}
