using System;
using System.Reflection;
using System.Runtime.InteropServices;
using CursorVault.Models;
using Microsoft.Win32;

namespace CursorVault.Services;

public static class DiagnosticService
{
    public static string BuildReport(AppSettings settings, int packCount, int favoriteCount)
    {
        var storage = StorageService.GetUsage();
        var registry = TestCursorRegistry() ? "OK" : "ERREUR";
        return string.Join(Environment.NewLine, new[]
        {
            "CursorVault diagnostic",
            $"Version: {Assembly.GetExecutingAssembly().GetName().Version}",
            $"Windows: {RuntimeInformation.OSDescription}",
            $".NET: {RuntimeInformation.FrameworkDescription}",
            $"Architecture: {RuntimeInformation.ProcessArchitecture}",
            $"Packs: {packCount}",
            $"Favoris: {favoriteCount}",
            $"Registre curseurs: {registry}",
            $"Données: {AppPaths.DataRoot}",
            $"Mode portable: {(AppPaths.IsPortable ? "Oui" : "Non")}",
            $"Démarrage Windows: {(settings.StartWithWindows ? "Actif" : "Inactif")}",
            $"Arrière-plan: {(settings.MinimizeToTray ? "Actif" : "Inactif")}",
            $"Thème: {settings.Theme}",
            $"Couleur: {settings.AccentColor}",
            $"Police: {settings.FontFamily}",
            $"Stockage packs: {Format(storage.PacksBytes)}",
            $"Cache temporaire: {Format(storage.TempBytes)}",
            $"Stockage total: {Format(storage.TotalBytes)}"
        });
    }

    private static bool TestCursorRegistry()
    {
        try { using var key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Cursors", false); return key is not null; }
        catch { return false; }
    }

    private static string Format(long bytes)
    {
        if (bytes < 1024) return $"{bytes} o";
        if (bytes < 1024 * 1024) return $"{bytes / 1024d:0.0} Ko";
        return $"{bytes / 1024d / 1024d:0.0} Mo";
    }
}
