
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using RustPlusApi.Fcm.Registration;
using RustPlusApi.Fcm.Data;

namespace RustTeamTracker.Services;

public sealed class RustPlusRegistrationService
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public RustPlusRegistrationService(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory;
    }

    public string Status { get; private set; } = "Не подключено";
    public string? LoginUrl { get; private set; }
    public string? ServerName { get; private set; }
    public string? ConfigPath { get; private set; }
    public bool IsRunning { get; private set; }

    public event Action? StateChanged;

    private void UpdateStatus(string status)
    {
        Status = status;
        StateChanged?.Invoke();
    }

    public async Task RegisterAsync()
    {
        if (!await _gate.WaitAsync(0))
            return;

        try
        {
            IsRunning = true;
            LoginUrl = null;
            ServerName = null;

            ConfigPath = Path.Combine(
                AppContext.BaseDirectory,
                "Config",
                "rustplus.config.json");

            UpdateStatus("Проверяем сохранённую конфигурацию…");

            if (!File.Exists(ConfigPath))
            {
                UpdateStatus($"Конфиг не найден: {ConfigPath}");
                return;
            }

            UpdateStatus("Загружаем FCM-реквизиты…");

            var credentials = RustPlusConfigLoader.Load(ConfigPath);

            UpdateStatus("Создаём FCM PairingListener…");

            using var listener = new PairingListener(
                credentials,
                loggerFactory: _loggerFactory);

            listener.Listening += (_, _) =>
                UpdateStatus("FCM: подключение активно, ожидаем привязку.");

            UpdateStatus("Ожидаем привязку Rust+ к серверу…");

            var pairing = await listener.WaitForServerPairingAsync();

            ServerName = pairing.Name;

            UpdateStatus(
                $"Сервер «{pairing.Name}» привязан. " +
                $"IP: {pairing.Ip}, порт: {pairing.Port}. " +
                $"Player ID: {pairing.PlayerId}.");

            var directory = Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData),
                "RustTeamTracker");

            Directory.CreateDirectory(directory);

            var connectionPath = Path.Combine(
                directory,
                "server-connection.txt");

            await File.WriteAllTextAsync(
                connectionPath,
                $"ServerName={pairing.Name}{Environment.NewLine}" +
                $"Ip={pairing.Ip}{Environment.NewLine}" +
                $"Port={pairing.Port}{Environment.NewLine}" +
                $"PlayerId={pairing.PlayerId}{Environment.NewLine}" +
                $"PlayerToken={pairing.PlayerToken}{Environment.NewLine}");

            UpdateStatus($"Регистрация завершена. Данные сохранены в {directory}");
        }
        catch (Exception ex)
        {
            UpdateStatus($"Ошибка: {ex.GetBaseException().Message}");
        }
        finally
        {
            IsRunning = false;
            StateChanged?.Invoke();
            _gate.Release();
        }
    }
}
