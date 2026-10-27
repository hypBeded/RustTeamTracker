
using System;
using System.IO;
using System.Text.Json;
using RustPlusApi.Fcm.Data;

namespace RustTeamTracker.Services;

public static class RustPlusConfigLoader
{
    public static Credentials Load(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                "Файл конфигурации Rust+ не найден.", path);
        }

        using var document = JsonDocument.Parse(
            File.ReadAllText(path));

        var root = document.RootElement;
        var fcmCredentials = root.GetProperty("fcm_credentials");
        var gcmElement = fcmCredentials.GetProperty("gcm");
        var fcmElement = fcmCredentials.GetProperty("fcm");

        return new Credentials
        {
            Gcm = new Gcm
            {
                AndroidId = ulong.Parse(
        gcmElement.GetProperty("androidId").GetString()!),

                SecurityToken = ulong.Parse(
        gcmElement.GetProperty("securityToken").GetString()!)
            },
            Fcm = new FcmToken
            {
                Token = fcmElement
                    .GetProperty("token").GetString()!
            },
            ExpoPushToken = root
                .GetProperty("expo_push_token").GetString()!
        };
    }
}
