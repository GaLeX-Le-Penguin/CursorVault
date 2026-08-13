using System;
using System.Collections.Generic;

namespace CursorVault.Models;

public sealed class AppSettings
{
    public string Theme { get; set; } = "Dark";
    public string AccentColor { get; set; } = "#6EA8FE";
    public string FontFamily { get; set; } = "Segoe UI";
    public string Language { get; set; } = "fr-FR";
    public bool UseSystemLanguage { get; set; } = true;
    public bool StartWithWindows { get; set; }
    public bool MinimizeToTray { get; set; }
    public string PackFilter { get; set; } = "Tous";
    public string LibrarySort { get; set; } = "Favoris d'abord";
    public string InterfaceSize { get; set; } = "Normale";
    public MissingRoleBehavior MissingRoleBehavior { get; set; } = MissingRoleBehavior.KeepCurrent;
    public HashSet<string> FavoritePackIds { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public bool RotationEnabled { get; set; }
    public string RotationMode { get; set; } = "Démarrage";
    public bool RotationFavoritesOnly { get; set; }

    // Synchronisation avec le thème Windows.
    public bool FollowWindowsTheme { get; set; }
    public string LightThemePackId { get; set; } = "";
    public string DarkThemePackId { get; set; } = "";

}
