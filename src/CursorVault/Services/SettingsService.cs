using System;
using System.IO;
using System.Text.Json;
using CursorVault.Models;

namespace CursorVault.Services;

public sealed class SettingsService
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public AppSettings Load()
    {
        AppPaths.Ensure();
        if (!File.Exists(AppPaths.SettingsFile))
            return new AppSettings();

        try
        {
            var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(AppPaths.SettingsFile), Options)
                ?? new AppSettings();
            settings.FavoritePackIds ??= new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
            return settings;
        }
        catch
        {
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        AppPaths.Ensure();
        File.WriteAllText(AppPaths.SettingsFile, JsonSerializer.Serialize(settings, Options));
    }
}
