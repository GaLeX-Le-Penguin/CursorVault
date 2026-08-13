using System;
using System.IO;

namespace CursorVault.Services;

public static class AppPaths
{
    public static string PortableFlag { get; } = Path.Combine(AppContext.BaseDirectory, "portable.flag");
    public static bool IsPortable { get; } = File.Exists(PortableFlag);

    public static string DataRoot { get; } = IsPortable
        ? Path.Combine(AppContext.BaseDirectory, "Data")
        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CursorVault");

    public static string PacksRoot { get; } = Path.Combine(DataRoot, "Packs");
    public static string BackupFile { get; } = Path.Combine(DataRoot, "cursor-backup.json");
    public static string SettingsFile { get; } = Path.Combine(DataRoot, "settings.json");
    public static string TempRoot { get; } = Path.Combine(DataRoot, "Temp");
    public static string LogsRoot { get; } = Path.Combine(DataRoot, "Logs");

    public static void Ensure()
    {
        Directory.CreateDirectory(DataRoot);
        Directory.CreateDirectory(PacksRoot);
        Directory.CreateDirectory(TempRoot);
        Directory.CreateDirectory(LogsRoot);
    }
}
