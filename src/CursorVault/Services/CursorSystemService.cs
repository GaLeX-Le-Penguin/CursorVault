using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using CursorVault.Models;
using Microsoft.Win32;

namespace CursorVault.Services;

public sealed class CursorSystemService
{
    private const uint SPI_SETCURSORS = 0x0057;
    private const uint SPIF_SENDCHANGE = 0x02;

    public static readonly string[] KnownRoles =
    {
        "Arrow", "Help", "AppStarting", "Wait", "Crosshair", "IBeam", "NWPen", "No",
        "SizeNS", "SizeWE", "SizeNWSE", "SizeNESW", "SizeAll", "UpArrow", "Hand", "Pin", "Person"
    };

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SystemParametersInfo(uint uiAction, uint uiParam, IntPtr pvParam, uint fWinIni);

    public void EnsureBackup()
    {
        AppPaths.Ensure();
        if (File.Exists(AppPaths.BackupFile)) return;
        SaveBackup(AppPaths.BackupFile);
    }

    public void SaveBackup(string path)
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Cursors", writable: false)
            ?? throw new InvalidOperationException("Impossible d'ouvrir la configuration des curseurs Windows.");

        var backup = new CursorBackup
        {
            SchemeName = Convert.ToString(key.GetValue("")) ?? "",
            Values = KnownRoles.ToDictionary(role => role, role => Convert.ToString(key.GetValue(role)) ?? "",
                StringComparer.OrdinalIgnoreCase)
        };

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(backup, new JsonSerializerOptions { WriteIndented = true }));
    }

    public void ApplyPack(
        CursorPack pack,
        MissingRoleBehavior missingRoleBehavior = MissingRoleBehavior.KeepCurrent,
        IReadOnlyDictionary<string, string>? manualMappings = null)
    {
        EnsureBackup();

        using var key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Cursors", writable: true)
            ?? throw new InvalidOperationException("Impossible d'ouvrir la configuration des curseurs Windows en écriture.");

        foreach (var role in KnownRoles)
        {
            if (pack.Cursors.TryGetValue(role, out var relativePath) && !string.IsNullOrWhiteSpace(relativePath))
            {
                var fullPath = ResolvePackFile(pack, relativePath);
                if (!File.Exists(fullPath))
                    throw new FileNotFoundException($"Le curseur '{role}' est introuvable.", fullPath);
                key.SetValue(role, fullPath, RegistryValueKind.ExpandString);
                continue;
            }

            switch (missingRoleBehavior)
            {
                case MissingRoleBehavior.KeepCurrent:
                    break;
                case MissingRoleBehavior.WindowsDefault:
                    key.SetValue(role, "", RegistryValueKind.ExpandString);
                    break;
                case MissingRoleBehavior.ChooseManually:
                    if (manualMappings is null || !manualMappings.TryGetValue(role, out var manualPath) || !File.Exists(manualPath))
                        throw new InvalidOperationException($"Aucun curseur manuel n'a été choisi pour le rôle '{role}'.");
                    if (!manualPath.EndsWith(".cur", StringComparison.OrdinalIgnoreCase) &&
                        !manualPath.EndsWith(".ani", StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException($"Le fichier choisi pour '{role}' n'est pas un curseur .cur ou .ani.");
                    key.SetValue(role, Path.GetFullPath(manualPath), RegistryValueKind.ExpandString);
                    break;
            }
        }

        key.SetValue("", pack.Name, RegistryValueKind.String);
        ReloadWindowsCursors();
    }

    public Dictionary<string, string> GetCurrentCursorValues(out string schemeName)
        => ReadCurrentCursorValues(out schemeName);

    public List<WindowsCursorScheme> GetInstalledSchemes()
    {
        var schemes = new Dictionary<string, WindowsCursorScheme>(StringComparer.CurrentCultureIgnoreCase);

        ReadSchemeRegistry(Registry.LocalMachine,
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Control Panel\Cursors\Schemes",
            "Système", schemes, overwrite: false);

        ReadSchemeRegistry(Registry.CurrentUser,
            @"Control Panel\Cursors\Schemes",
            "Utilisateur", schemes, overwrite: true);

        var current = ReadCurrentCursorValues(out var currentSchemeName);
        var hasMatchingScheme = false;

        foreach (var scheme in schemes.Values)
        {
            scheme.IsActive = SchemeMatchesCurrent(scheme, current) ||
                              (!string.IsNullOrWhiteSpace(currentSchemeName) &&
                               scheme.Name.Equals(currentSchemeName, StringComparison.CurrentCultureIgnoreCase));
            hasMatchingScheme |= scheme.IsActive;
        }

        if (!hasMatchingScheme)
        {
            schemes["Configuration actuelle (personnalisée)"] = new WindowsCursorScheme
            {
                Name = "Configuration actuelle (personnalisée)",
                Source = "Windows actif",
                IsActive = true,
                Cursors = current
            };
        }

        return schemes.Values
            .OrderByDescending(s => s.IsActive)
            .ThenBy(s => s.Source.Equals("Utilisateur", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(s => s.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    public void ApplyInstalledScheme(WindowsCursorScheme scheme)
    {
        EnsureBackup();

        using var key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Cursors", writable: true)
            ?? throw new InvalidOperationException("Impossible d'ouvrir la configuration des curseurs Windows en écriture.");

        foreach (var role in KnownRoles)
        {
            scheme.Cursors.TryGetValue(role, out var value);
            key.SetValue(role, value ?? "", RegistryValueKind.ExpandString);
        }

        key.SetValue("", scheme.Name, RegistryValueKind.String);
        ReloadWindowsCursors();
    }

    public HashSet<string> GetCurrentCursorPaths()
    {
        var current = ReadCurrentCursorValues(out _);
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var value in current.Values)
        {
            var normalized = NormalizeCursorPath(value);
            if (!string.IsNullOrWhiteSpace(normalized) && Path.IsPathRooted(normalized))
                result.Add(normalized);
        }

        return result;
    }

    public void RestoreBackup() => RestoreBackup(AppPaths.BackupFile);

    public void RestoreBackup(string backupPath)
    {
        if (!File.Exists(backupPath))
            throw new FileNotFoundException("Aucune sauvegarde CursorVault n'existe encore.", backupPath);

        var backup = JsonSerializer.Deserialize<CursorBackup>(File.ReadAllText(backupPath))
            ?? throw new InvalidOperationException("La sauvegarde est invalide.");

        using var key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Cursors", writable: true)
            ?? throw new InvalidOperationException("Impossible d'ouvrir la configuration des curseurs Windows en écriture.");

        foreach (var role in KnownRoles)
        {
            if (backup.Values.TryGetValue(role, out var value))
                key.SetValue(role, value ?? "", RegistryValueKind.ExpandString);
        }

        key.SetValue("", backup.SchemeName ?? "", RegistryValueKind.String);
        ReloadWindowsCursors();
    }

    private static void ReadSchemeRegistry(
        RegistryKey root,
        string subKeyPath,
        string source,
        Dictionary<string, WindowsCursorScheme> destination,
        bool overwrite)
    {
        using var key = root.OpenSubKey(subKeyPath, writable: false);
        if (key is null) return;

        foreach (var valueName in key.GetValueNames())
        {
            if (string.IsNullOrWhiteSpace(valueName)) continue;
            var raw = Convert.ToString(key.GetValue(valueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames));
            if (raw is null) continue;

            var values = raw.Split(',', StringSplitOptions.None);
            var cursors = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < KnownRoles.Length; i++)
            {
                var value = i < values.Length ? values[i].Trim().Trim('"') : "";
                cursors[KnownRoles[i]] = Environment.ExpandEnvironmentVariables(value);
            }

            var scheme = new WindowsCursorScheme
            {
                Name = valueName,
                Source = source,
                Cursors = cursors
            };

            if (overwrite || !destination.ContainsKey(valueName))
                destination[valueName] = scheme;
        }
    }

    private static Dictionary<string, string> ReadCurrentCursorValues(out string schemeName)
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Cursors", writable: false)
            ?? throw new InvalidOperationException("Impossible d'ouvrir la configuration des curseurs Windows.");

        schemeName = Convert.ToString(key.GetValue("")) ?? "";
        return KnownRoles.ToDictionary(
            role => role,
            role => Environment.ExpandEnvironmentVariables(Convert.ToString(key.GetValue(role)) ?? ""),
            StringComparer.OrdinalIgnoreCase);
    }

    private static bool SchemeMatchesCurrent(WindowsCursorScheme scheme, Dictionary<string, string> current)
    {
        foreach (var role in KnownRoles)
        {
            scheme.Cursors.TryGetValue(role, out var schemeValue);
            current.TryGetValue(role, out var currentValue);

            var a = NormalizeCursorPath(schemeValue ?? "");
            var b = NormalizeCursorPath(currentValue ?? "");
            if (!a.Equals(b, StringComparison.OrdinalIgnoreCase))
                return false;
        }

        return true;
    }

    private static string NormalizeCursorPath(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        var expanded = Environment.ExpandEnvironmentVariables(value.Trim().Trim('"'));
        try
        {
            return Path.IsPathRooted(expanded) ? Path.GetFullPath(expanded) : expanded;
        }
        catch
        {
            return expanded;
        }
    }

    private static string ResolvePackFile(CursorPack pack, string relativePath)
    {
        var packRoot = Path.GetFullPath(pack.FolderPath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var full = Path.GetFullPath(Path.Combine(pack.FolderPath, relativePath));
        if (!full.StartsWith(packRoot, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Le pack contient un chemin de fichier non autorisé.");
        return full;
    }

    private static void ReloadWindowsCursors()
    {
        if (!SystemParametersInfo(SPI_SETCURSORS, 0, IntPtr.Zero, SPIF_SENDCHANGE))
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(), "Windows n'a pas pu recharger les curseurs.");
    }

    private sealed class CursorBackup
    {
        public string? SchemeName { get; set; }
        public Dictionary<string, string?> Values { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
