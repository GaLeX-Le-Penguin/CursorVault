using System.IO;
using System.Linq;

namespace CursorVault.Services;

public sealed record StorageUsage(long PacksBytes, long TempBytes, long BackupBytes, long SettingsBytes)
{
    public long TotalBytes => PacksBytes + TempBytes + BackupBytes + SettingsBytes;
}

public static class StorageService
{
    public static StorageUsage GetUsage() => new(
        GetDirectorySize(AppPaths.PacksRoot),
        GetDirectorySize(AppPaths.TempRoot),
        File.Exists(AppPaths.BackupFile) ? new FileInfo(AppPaths.BackupFile).Length : 0,
        File.Exists(AppPaths.SettingsFile) ? new FileInfo(AppPaths.SettingsFile).Length : 0);

    public static void ClearTemp()
    {
        if (Directory.Exists(AppPaths.TempRoot))
        {
            foreach (var file in Directory.GetFiles(AppPaths.TempRoot, "*", SearchOption.TopDirectoryOnly))
                TryDeleteFile(file);
            foreach (var dir in Directory.GetDirectories(AppPaths.TempRoot))
                TryDeleteDirectory(dir);
        }
    }

    private static long GetDirectorySize(string path)
    {
        if (!Directory.Exists(path)) return 0;
        try { return Directory.GetFiles(path, "*", SearchOption.AllDirectories).Sum(f => new FileInfo(f).Length); }
        catch { return 0; }
    }

    private static void TryDeleteFile(string path) { try { File.Delete(path); } catch { } }
    private static void TryDeleteDirectory(string path) { try { Directory.Delete(path, true); } catch { } }
}
