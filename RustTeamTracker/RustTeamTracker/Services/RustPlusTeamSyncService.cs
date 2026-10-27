
using RustPlusApi;

// Пример подключения.
/* Значения должны поступать из защищённой конфигурации.
var connection = new RustPlusConnection(
    server: serverIp,
    port: companionPort,
    playerId: steamId64,
    playerToken: playerToken
);

using var rustPlus = new RustPlus(connection);

await rustPlus.ConnectAsync();

var response = await rustPlus.GetTeamInfoAsync();

if (!response.IsSuccess || response.Data is null)
{
    Console.WriteLine(
        $"Не удалось получить команду: {response.Error?.Message}");
    return;
}

// На следующем шаге обработаем response.Data.Members:
// сохраним каждого участника по уникальному Steam ID в SQLite.
Console.WriteLine("Данные команды успешно получены.");
*/