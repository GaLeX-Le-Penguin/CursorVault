using System;
using System.IO;
using System.IO.Compression;
using System.Text.Json;

namespace CursorVault.Services;

public sealed class BackupService
{
    public void Export(string destination)
    {
        AppPaths.Ensure();
        var temp = Path.Combine(AppPaths.TempRoot, "backup-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        try
        {
            var meta = new { format = 1, createdUtc = DateTime.UtcNow, app = "CursorVault" };
            File.WriteAllText(Path.Combine(temp, "backup.json"), JsonSerializer.Serialize(meta, new JsonSerializerOptions { WriteIndented = true }));
            if (File.Exists(AppPaths.SettingsFile)) File.Copy(AppPaths.SettingsFile, Path.Combine(temp, "settings.json"), true);
            if (File.Exists(AppPaths.BackupFile)) File.Copy(AppPaths.BackupFile, Path.Combine(temp, "cursor-backup.json"), true);
            new CursorSystemService().SaveBackup(Path.Combine(temp, "current-cursors.json"));
            if (Directory.Exists(AppPaths.PacksRoot)) CopyDirectory(AppPaths.PacksRoot, Path.Combine(temp, "Packs"));
            if (File.Exists(destination)) File.Delete(destination);
            ZipFile.CreateFromDirectory(temp, destination, CompressionLevel.Optimal, false);
        }
        finally { TryDelete(temp); }
    }

    public void Import(string source)
    {
        AppPaths.Ensure();
        var temp = Path.Combine(AppPaths.TempRoot, "restore-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        try
        {
            ExtractSafely(source, temp);
            if (!File.Exists(Path.Combine(temp, "backup.json")))
                throw new InvalidOperationException("Ce fichier n'est pas une sauvegarde CursorVault valide.");

            var packs = Path.Combine(temp, "Packs");
            if (Directory.Exists(packs)) CopyDirectory(packs, AppPaths.PacksRoot);
            var settings = Path.Combine(temp, "settings.json");
            if (File.Exists(settings)) File.Copy(settings, AppPaths.SettingsFile, true);
            var cursorBackup = Path.Combine(temp, "cursor-backup.json");
            if (File.Exists(cursorBackup)) File.Copy(cursorBackup, AppPaths.BackupFile, true);
            var currentCursors = Path.Combine(temp, "current-cursors.json");
            if (File.Exists(currentCursors)) new CursorSystemService().RestoreBackup(currentCursors);
        }
        finally { TryDelete(temp); }
    }

    private static void ExtractSafely(string archivePath, string destination)
    {
        var root = Path.GetFullPath(destination).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        using var archive = ZipFile.OpenRead(archivePath);
        foreach (var entry in archive.Entries)
        {
            var target = Path.GetFullPath(Path.Combine(destination, entry.FullName));
            if (!target.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Sauvegarde refusée : chemin non autorisé.");
            if (string.IsNullOrEmpty(entry.Name)) { Directory.CreateDirectory(target); continue; }
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            entry.ExtractToFile(target, true);
        }
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.GetFiles(source)) File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), true);
        foreach (var dir in Directory.GetDirectories(source)) CopyDirectory(dir, Path.Combine(destination, Path.GetFileName(dir)));
    }

    private static void TryDelete(string path) { try { if (Directory.Exists(path)) Directory.Delete(path, true); } catch { } }
}
