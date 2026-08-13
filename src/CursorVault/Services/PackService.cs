using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using System.Text;
using CursorVault.Models;

namespace CursorVault.Services;

public sealed class PackService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public void EnsureStarterPacks()
    {
        AppPaths.Ensure();
        var sourceRoot = Path.Combine(AppContext.BaseDirectory, "CursorVault_Data", "StarterPacks");
        // Compatibilité avec les anciennes publications / exécution depuis Visual Studio.
        if (!Directory.Exists(sourceRoot))
            sourceRoot = Path.Combine(AppContext.BaseDirectory, "StarterPacks");
        if (!Directory.Exists(sourceRoot)) return;

        foreach (var sourceDir in Directory.GetDirectories(sourceRoot))
        {
            var destination = Path.Combine(AppPaths.PacksRoot, Path.GetFileName(sourceDir));
            if (!Directory.Exists(destination))
                CopyDirectory(sourceDir, destination);
        }
    }

    public List<CursorPack> LoadPacks()
    {
        AppPaths.Ensure();
        var result = new List<CursorPack>();

        foreach (var dir in Directory.GetDirectories(AppPaths.PacksRoot))
        {
            var manifest = Path.Combine(dir, "pack.json");
            if (!File.Exists(manifest)) continue;

            try
            {
                var pack = JsonSerializer.Deserialize<CursorPack>(File.ReadAllText(manifest), JsonOptions);
                if (pack is null) continue;
                pack.FolderPath = dir;
                NormalizeMetadata(pack);
                pack.SizeBytes = GetDirectorySize(dir);
                pack.AddedAt = Directory.GetCreationTime(dir);
                result.Add(pack);
                SavePack(pack);
            }
            catch
            {
                // Un pack invalide est ignoré plutôt que de bloquer la bibliothèque entière.
            }
        }

        return result.OrderBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    public List<CursorPack> ImportPath(string path)
    {
        if (Directory.Exists(path))
            return new List<CursorPack> { ImportFolder(path) };

        if (!File.Exists(path))
            throw new FileNotFoundException("Le fichier à importer est introuvable.", path);

        if (path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            return ImportArchive(path);

        if (IsCursorFile(path))
            return new List<CursorPack> { ImportFiles(new[] { path }, Path.GetFileNameWithoutExtension(path)) };

        throw new InvalidOperationException("CursorVault accepte les dossiers, .zip, .cur et .ani.");
    }

    public List<CursorPack> ImportDroppedFiles(IEnumerable<string> paths)
    {
        var input = paths.Where(p => !string.IsNullOrWhiteSpace(p)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var cursorFiles = input.Where(File.Exists).Where(IsCursorFile).ToList();
        var others = input.Except(cursorFiles, StringComparer.OrdinalIgnoreCase).ToList();
        var result = new List<CursorPack>();

        if (cursorFiles.Count > 0)
        {
            var name = cursorFiles.Count == 1 ? Path.GetFileNameWithoutExtension(cursorFiles[0]) : "Pack importé";
            result.Add(ImportFiles(cursorFiles, name));
        }

        foreach (var path in others)
            result.AddRange(ImportPath(path));

        return result;
    }

    public CursorPack ImportFolder(string sourceFolder)
    {
        if (!Directory.Exists(sourceFolder))
            throw new DirectoryNotFoundException(sourceFolder);

        var cursorFiles = Directory.GetFiles(sourceFolder, "*.*", SearchOption.AllDirectories)
            .Where(IsCursorFile)
            .ToList();

        if (cursorFiles.Count == 0)
            throw new InvalidOperationException("Le dossier ne contient aucun fichier .cur ou .ani.");

        CursorPack pack;
        var sourceManifest = Path.Combine(sourceFolder, "pack.json");
        if (File.Exists(sourceManifest))
        {
            pack = JsonSerializer.Deserialize<CursorPack>(File.ReadAllText(sourceManifest), JsonOptions)
                ?? throw new InvalidOperationException("Le fichier pack.json est invalide.");
        }
        else
        {
            pack = BuildAutomaticPack(sourceFolder, cursorFiles);
        }

        NormalizeMetadata(pack);
        var safeFolder = MakeSafeFolderName(pack.Name);
        var destination = MakeUniqueDestination(Path.Combine(AppPaths.PacksRoot, safeFolder));
        CopyDirectory(sourceFolder, destination);

        pack.FolderPath = destination;
        pack.Id = Path.GetFileName(destination);
        SavePack(pack);
        return pack;
    }

    public CursorPack ImportFiles(IEnumerable<string> files, string name)
    {
        var valid = files.Where(File.Exists).Where(IsCursorFile).ToList();
        if (valid.Count == 0)
            throw new InvalidOperationException("Aucun fichier .cur ou .ani valide n'a été fourni.");

        var temp = Path.Combine(AppPaths.TempRoot, "import-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        try
        {
            foreach (var file in valid)
                File.Copy(file, MakeUniqueFilePath(Path.Combine(temp, Path.GetFileName(file))), false);

            var cursorFiles = Directory.GetFiles(temp).Where(IsCursorFile).ToList();
            var pack = BuildAutomaticPack(temp, cursorFiles);
            pack.Name = string.IsNullOrWhiteSpace(name) ? "Pack importé" : name.Trim();
            pack.Author = "Auteur inconnu (import local)";
            pack.OriginalAuthor = pack.Author;
            File.WriteAllText(Path.Combine(temp, "pack.json"), JsonSerializer.Serialize(pack, JsonOptions));
            return ImportFolder(temp);
        }
        finally
        {
            TryDeleteDirectory(temp);
        }
    }

    public List<CursorPack> ImportArchive(string archivePath)
    {
        AppPaths.Ensure();
        var temp = Path.Combine(AppPaths.TempRoot, "zip-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);

        try
        {
            ExtractZipSafely(archivePath, temp);

            var manifests = Directory.GetFiles(temp, "pack.json", SearchOption.AllDirectories);
            if (manifests.Length > 0)
            {
                var imported = new List<CursorPack>();
                foreach (var folder in manifests.Select(Path.GetDirectoryName).Where(p => p is not null).Distinct(StringComparer.OrdinalIgnoreCase))
                    imported.Add(ImportFolder(folder!));
                return imported;
            }

            var candidate = Directory.GetDirectories(temp, "*", SearchOption.AllDirectories)
                .Prepend(temp)
                .Select(dir => new
                {
                    Dir = dir,
                    Count = Directory.GetFiles(dir, "*.*", SearchOption.TopDirectoryOnly).Count(IsCursorFile)
                })
                .OrderByDescending(x => x.Count)
                .FirstOrDefault(x => x.Count > 0);

            if (candidate is null)
                throw new InvalidOperationException("L'archive ZIP ne contient aucun curseur .cur ou .ani.");

            return new List<CursorPack> { ImportFolder(candidate.Dir) };
        }
        finally
        {
            TryDeleteDirectory(temp);
        }
    }

    public CursorPack CreatePack(string name, string originalAuthor, string description, IReadOnlyDictionary<string, string> roleFiles)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidOperationException("Le nom du pack est obligatoire.");
        if (string.IsNullOrWhiteSpace(originalAuthor))
            throw new InvalidOperationException("Le créateur original est obligatoire.");

        var selected = roleFiles.Where(kv => !string.IsNullOrWhiteSpace(kv.Value)).ToList();
        if (selected.Count == 0)
            throw new InvalidOperationException("Sélectionnez au moins un fichier curseur.");

        var destination = MakeUniqueDestination(Path.Combine(AppPaths.PacksRoot, MakeSafeFolderName(name)));
        Directory.CreateDirectory(destination);

        var pack = new CursorPack
        {
            Id = Path.GetFileName(destination),
            Name = name.Trim(),
            Author = originalAuthor.Trim(),
            OriginalAuthor = originalAuthor.Trim(),
            Description = description?.Trim() ?? "",
            FolderPath = destination
        };

        foreach (var (role, file) in selected)
        {
            if (!CursorSystemService.KnownRoles.Contains(role, StringComparer.OrdinalIgnoreCase)) continue;
            if (!File.Exists(file) || !IsCursorFile(file)) continue;

            var destinationFile = MakeUniqueFilePath(Path.Combine(destination, Path.GetFileName(file)));
            File.Copy(file, destinationFile, false);
            pack.Cursors[role] = Path.GetFileName(destinationFile);
        }

        SavePack(pack);
        return pack;
    }

    public void RenamePack(CursorPack pack, string newName)
    {
        if (string.IsNullOrWhiteSpace(newName))
            throw new InvalidOperationException("Le nouveau nom ne peut pas être vide.");
        pack.Name = newName.Trim();
        pack.VariantGroup = "";
        pack.VariantName = "";
        SavePack(pack);
    }

    public void SavePack(CursorPack pack)
    {
        if (string.IsNullOrWhiteSpace(pack.FolderPath))
            throw new InvalidOperationException("Le dossier du pack n'est pas défini.");
        NormalizeMetadata(pack);
        Directory.CreateDirectory(pack.FolderPath);
        File.WriteAllText(Path.Combine(pack.FolderPath, "pack.json"), JsonSerializer.Serialize(pack, JsonOptions));
    }

    public void DeletePack(CursorPack pack)
    {
        if (!Directory.Exists(pack.FolderPath)) return;
        var root = Path.GetFullPath(AppPaths.PacksRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var full = Path.GetFullPath(pack.FolderPath);
        if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Suppression refusée en dehors de la bibliothèque CursorVault.");
        Directory.Delete(full, recursive: true);
    }

    public void ExportPack(CursorPack pack, string destinationZip)
    {
        if (!Directory.Exists(pack.FolderPath))
            throw new DirectoryNotFoundException(pack.FolderPath);
        if (File.Exists(destinationZip)) File.Delete(destinationZip);
        ZipFile.CreateFromDirectory(pack.FolderPath, destinationZip, CompressionLevel.Optimal, includeBaseDirectory: true);
    }

    public PackValidationResult ValidatePack(CursorPack pack)
    {
        var result = new PackValidationResult();
        var root = Path.GetFullPath(pack.FolderPath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;

        foreach (var role in CursorSystemService.KnownRoles)
        {
            if (!pack.Cursors.TryGetValue(role, out var relative) || string.IsNullOrWhiteSpace(relative))
            {
                result.MissingRoles.Add(role);
                continue;
            }

            try
            {
                var full = Path.GetFullPath(Path.Combine(pack.FolderPath, relative));
                if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase) || !IsCursorFile(full))
                {
                    result.InvalidFiles.Add(role);
                    continue;
                }
                if (!File.Exists(full))
                    result.MissingFiles.Add(role);
                else if (!IsCursorBinaryValid(full))
                    result.InvalidFiles.Add(role);
            }
            catch
            {
                result.InvalidFiles.Add(role);
            }
        }

        return result;
    }

    public PackAnalysisResult AnalyzePack(CursorPack pack)
    {
        var validation = ValidatePack(pack);
        var files = pack.Cursors.Values
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Select(v => SafeResolve(pack, v))
            .Where(File.Exists)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var hashes = files.Select(ComputeFileHash).Where(h => h.Length > 0).ToList();
        var duplicateCount = hashes.GroupBy(h => h, StringComparer.OrdinalIgnoreCase).Sum(g => Math.Max(0, g.Count() - 1));
        return new PackAnalysisResult
        {
            RoleCount = pack.CursorCount,
            CurCount = files.Count(f => f.EndsWith(".cur", StringComparison.OrdinalIgnoreCase)),
            AniCount = files.Count(f => f.EndsWith(".ani", StringComparison.OrdinalIgnoreCase)),
            MissingFiles = validation.MissingFiles.Count,
            InvalidFiles = validation.InvalidFiles.Count,
            DuplicateFiles = duplicateCount,
            SizeBytes = files.Sum(f => new FileInfo(f).Length)
        };
    }

    public string ComputePackFingerprint(CursorPack pack)
    {
        using var sha = SHA256.Create();
        var pieces = new List<string>();
        foreach (var role in CursorSystemService.KnownRoles)
        {
            if (!pack.Cursors.TryGetValue(role, out var relative) || string.IsNullOrWhiteSpace(relative))
            {
                pieces.Add(role + ":-");
                continue;
            }
            var full = SafeResolve(pack, relative);
            pieces.Add(role + ":" + (File.Exists(full) ? ComputeFileHash(full) : "missing:" + relative));
        }
        return Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(string.Join("|", pieces))));
    }

    public void GenerateInstallInf(CursorPack pack, string destination)
    {
        var validation = ValidatePack(pack);
        if (!validation.IsValid)
            throw new InvalidOperationException("Le pack contient des fichiers invalides ou absents.");

        var outputDir = Path.GetDirectoryName(destination) ?? throw new InvalidOperationException("Dossier de destination invalide.");
        Directory.CreateDirectory(outputDir);
        var exportedByRole = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var exportedBySource = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var role in CursorSystemService.KnownRoles)
        {
            if (!pack.Cursors.TryGetValue(role, out var relative) || string.IsNullOrWhiteSpace(relative)) continue;
            var source = SafeResolve(pack, relative);
            if (!File.Exists(source)) continue;
            if (exportedBySource.TryGetValue(source, out var existingName))
            {
                exportedByRole[role] = existingName;
                continue;
            }

            var name = Path.GetFileName(source);
            if (!usedNames.Add(name))
            {
                name = role + "_" + name;
                usedNames.Add(name);
            }
            var destinationFile = Path.Combine(outputDir, name);
            if (!Path.GetFullPath(source).Equals(Path.GetFullPath(destinationFile), StringComparison.OrdinalIgnoreCase))
                File.Copy(source, destinationFile, overwrite: true);
            exportedBySource[source] = name;
            exportedByRole[role] = name;
        }

        var values = CursorSystemService.KnownRoles.Select(role =>
            exportedByRole.TryGetValue(role, out var name) ? @"%SystemRoot%\Cursors\" + name : "").ToArray();
        var scheme = string.Join(",", values);
        var sb = new StringBuilder();
        sb.AppendLine("; Generated by CursorVault");
        sb.AppendLine($"; Original creator: {pack.CreatorDisplay}");
        sb.AppendLine("[Version]");
        sb.AppendLine("Signature=\"$CHICAGO$\"");
        sb.AppendLine();
        sb.AppendLine("[DefaultInstall]");
        sb.AppendLine("CopyFiles=CursorFiles");
        sb.AppendLine("AddReg=CursorScheme");
        sb.AppendLine();
        sb.AppendLine("[DestinationDirs]");
        sb.AppendLine("CursorFiles=10,\"Cursors\"");
        sb.AppendLine();
        sb.AppendLine("[CursorFiles]");
        foreach (var file in exportedByRole.Values.Distinct(StringComparer.OrdinalIgnoreCase)) sb.AppendLine(file);
        sb.AppendLine();
        sb.AppendLine("[CursorScheme]");
        sb.AppendLine($"HKCU,\"Control Panel\\Cursors\\Schemes\",\"{EscapeInf(pack.Name)}\",0x00000000,\"{EscapeInf(scheme)}\"");
        File.WriteAllText(destination, sb.ToString(), Encoding.Unicode);
    }

    public static bool IsCursorBinaryValid(string file)
    {
        try
        {
            using var stream = File.OpenRead(file);
            if (file.EndsWith(".cur", StringComparison.OrdinalIgnoreCase))
            {
                Span<byte> header = stackalloc byte[6];
                if (stream.Read(header) != 6) return false;
                var reserved = BitConverter.ToUInt16(header[..2]);
                var type = BitConverter.ToUInt16(header.Slice(2, 2));
                var count = BitConverter.ToUInt16(header.Slice(4, 2));
                return reserved == 0 && type == 2 && count > 0;
            }
            if (file.EndsWith(".ani", StringComparison.OrdinalIgnoreCase))
            {
                Span<byte> header = stackalloc byte[12];
                if (stream.Read(header) != 12) return false;
                return Encoding.ASCII.GetString(header[..4]) == "RIFF" && Encoding.ASCII.GetString(header.Slice(8, 4)) == "ACON";
            }
            return false;
        }
        catch { return false; }
    }

    private static string ComputeFileHash(string file)
    {
        try
        {
            using var sha = SHA256.Create();
            using var stream = File.OpenRead(file);
            return Convert.ToHexString(sha.ComputeHash(stream));
        }
        catch { return ""; }
    }

    private static string SafeResolve(CursorPack pack, string relative)
    {
        try { return Path.GetFullPath(Path.Combine(pack.FolderPath, relative)); }
        catch { return Path.Combine(pack.FolderPath, relative); }
    }

    private static string EscapeInf(string value) => value.Replace("\"", "\"\"");

    public PackValidationResult RepairPack(CursorPack pack)
    {
        var validation = ValidatePack(pack);
        foreach (var role in validation.MissingFiles.Concat(validation.InvalidFiles).Distinct(StringComparer.OrdinalIgnoreCase).ToList())
            pack.Cursors.Remove(role);
        SavePack(pack);
        return ValidatePack(pack);
    }

    private CursorPack BuildAutomaticPack(string sourceFolder, List<string> cursorFiles)
    {
        var pack = new CursorPack
        {
            Name = Path.GetFileName(sourceFolder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)),
            Author = "Auteur inconnu (import local)",
            OriginalAuthor = "Auteur inconnu (import local)",
            Description = "Pack importé automatiquement par CursorVault."
        };

        var relativeByName = cursorFiles
            .GroupBy(f => Normalize(Path.GetFileNameWithoutExtension(f)))
            .ToDictionary(
                g => g.Key,
                g => Path.GetRelativePath(sourceFolder, g.First()),
                StringComparer.OrdinalIgnoreCase);

        string? Find(params string[] hints)
        {
            foreach (var hint in hints)
            {
                var normalizedHint = Normalize(hint);
                var match = relativeByName.FirstOrDefault(kv => kv.Key.Contains(normalizedHint, StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrEmpty(match.Value)) return match.Value;
            }
            return null;
        }

        var mappings = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["Arrow"] = Find("arrow", "normal", "pointer", "select"),
            ["Help"] = Find("help", "question"),
            ["AppStarting"] = Find("appstarting", "working", "background"),
            ["Wait"] = Find("wait", "busy", "loading"),
            ["Crosshair"] = Find("crosshair", "precision", "cross"),
            ["IBeam"] = Find("ibeam", "text", "beam"),
            ["NWPen"] = Find("pen", "handwriting"),
            ["No"] = Find("forbidden", "unavailable", "no"),
            ["SizeNS"] = Find("sizens", "vertical"),
            ["SizeWE"] = Find("sizewe", "horizontal"),
            ["SizeNWSE"] = Find("sizenwse", "diagonalresize1", "nwse"),
            ["SizeNESW"] = Find("sizenesw", "diagonalresize2", "nesw"),
            ["SizeAll"] = Find("sizeall", "move"),
            ["UpArrow"] = Find("uparrow", "alternate", "up"),
            ["Hand"] = Find("hand", "link"),
            ["Pin"] = Find("pin", "location"),
            ["Person"] = Find("person", "people")
        };

        foreach (var (role, file) in mappings)
        {
            if (!string.IsNullOrWhiteSpace(file))
                pack.Cursors[role] = file;
        }

        if (pack.Cursors.Count == 0 && cursorFiles.Count > 0)
            pack.Cursors["Arrow"] = Path.GetRelativePath(sourceFolder, cursorFiles[0]);

        return pack;
    }

    private static void NormalizeMetadata(CursorPack pack)
    {
        if (string.IsNullOrWhiteSpace(pack.Author)) pack.Author = "Inconnu";
        if (string.IsNullOrWhiteSpace(pack.OriginalAuthor)) pack.OriginalAuthor = pack.Author;
        if (string.IsNullOrWhiteSpace(pack.Id))
            pack.Id = !string.IsNullOrWhiteSpace(pack.FolderPath) ? Path.GetFileName(pack.FolderPath) : MakeSafeFolderName(pack.Name);
        pack.Cursors = pack.Cursors is null
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(pack.Cursors, StringComparer.OrdinalIgnoreCase);
        DetectVariant(pack);
    }

    private static void DetectVariant(CursorPack pack)
    {
        if (!string.IsNullOrWhiteSpace(pack.VariantName) && !string.IsNullOrWhiteSpace(pack.VariantGroup)) return;
        var match = Regex.Match(pack.Name, @"(?i)\b(light|dark)\b");
        if (!match.Success) return;
        pack.VariantName = match.Value.Equals("dark", StringComparison.OrdinalIgnoreCase) ? "Dark" : "Light";
        pack.VariantGroup = Regex.Replace(pack.Name, @"(?i)\b(light|dark)\b", " ");
        pack.VariantGroup = Regex.Replace(pack.VariantGroup, @"\s+", " ").Trim(' ', '-', '_');
    }

    private static void ExtractZipSafely(string archivePath, string destination)
    {
        var destinationRoot = Path.GetFullPath(destination).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        using var archive = ZipFile.OpenRead(archivePath);
        foreach (var entry in archive.Entries)
        {
            var target = Path.GetFullPath(Path.Combine(destination, entry.FullName));
            if (!target.StartsWith(destinationRoot, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Archive ZIP refusée : chemin de fichier non autorisé.");

            if (string.IsNullOrEmpty(entry.Name))
            {
                Directory.CreateDirectory(target);
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            entry.ExtractToFile(target, overwrite: true);
        }
    }

    private static bool IsCursorFile(string file) =>
        file.EndsWith(".cur", StringComparison.OrdinalIgnoreCase) ||
        file.EndsWith(".ani", StringComparison.OrdinalIgnoreCase);

    private static string Normalize(string value) => Regex.Replace(value.ToLowerInvariant(), "[^a-z0-9]", "");

    private static string MakeSafeFolderName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(value.Select(c => invalid.Contains(c) ? '_' : c).ToArray()).Trim();
        return string.IsNullOrWhiteSpace(cleaned) ? "ImportedPack" : cleaned;
    }

    private static string MakeUniqueDestination(string basePath)
    {
        if (!Directory.Exists(basePath)) return basePath;
        for (var i = 2; i < 1000; i++)
        {
            var candidate = $"{basePath}-{i}";
            if (!Directory.Exists(candidate)) return candidate;
        }
        throw new IOException("Impossible de créer un dossier unique pour le pack.");
    }

    private static string MakeUniqueFilePath(string basePath)
    {
        if (!File.Exists(basePath)) return basePath;
        var dir = Path.GetDirectoryName(basePath)!;
        var name = Path.GetFileNameWithoutExtension(basePath);
        var ext = Path.GetExtension(basePath);
        for (var i = 2; i < 1000; i++)
        {
            var candidate = Path.Combine(dir, $"{name}-{i}{ext}");
            if (!File.Exists(candidate)) return candidate;
        }
        throw new IOException("Impossible de créer un nom de fichier unique.");
    }

    private static long GetDirectorySize(string path)
    {
        try
        {
            return Directory.GetFiles(path, "*", SearchOption.AllDirectories).Sum(f => new FileInfo(f).Length);
        }
        catch
        {
            return 0;
        }
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.GetFiles(source))
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), overwrite: true);
        foreach (var dir in Directory.GetDirectories(source))
            CopyDirectory(dir, Path.Combine(destination, Path.GetFileName(dir)));
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
        }
        catch
        {
            // Nettoyage temporaire non bloquant.
        }
    }
}
