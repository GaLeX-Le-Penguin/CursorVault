using System;
using System.IO;

namespace CursorVault.Services;

public static class PortableModeService
{
    public static bool IsPortable => AppPaths.IsPortable;

    public static void RequestMode(bool portable)
    {
        var flag = Path.Combine(AppContext.BaseDirectory, "portable.flag");
        var portableData = Path.Combine(AppContext.BaseDirectory, "Data");
        var localData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CursorVault");

        if (portable)
        {
            if (Directory.Exists(AppPaths.DataRoot))
                CopyDirectory(AppPaths.DataRoot, portableData);
            Directory.CreateDirectory(portableData);
            File.WriteAllText(flag, "CursorVault portable mode");
        }
        else
        {
            if (Directory.Exists(AppPaths.DataRoot))
                CopyDirectory(AppPaths.DataRoot, localData);
            Directory.CreateDirectory(localData);
            if (File.Exists(flag)) File.Delete(flag);
        }
    }

    private static void CopyDirectory(string source, string destination)
    {
        if (Path.GetFullPath(source).Equals(Path.GetFullPath(destination), StringComparison.OrdinalIgnoreCase)) return;
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.GetFiles(source))
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), overwrite: true);
        foreach (var dir in Directory.GetDirectories(source))
            CopyDirectory(dir, Path.Combine(destination, Path.GetFileName(dir)));
    }
}
