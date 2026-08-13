using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using CursorVault.Services;

namespace CursorVault.Models;

public sealed class CursorPack
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "Pack sans nom";
    public string Author { get; set; } = "Inconnu";
    public string OriginalAuthor { get; set; } = "";
    public string Description { get; set; } = "";
    public string VariantGroup { get; set; } = "";
    public string VariantName { get; set; } = "";
    public Dictionary<string, string> Cursors { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    [JsonIgnore]
    public string FolderPath { get; set; } = "";

    [JsonIgnore]
    public bool IsFavorite { get; set; }

    [JsonIgnore]
    public long SizeBytes { get; set; }

    [JsonIgnore]
    public DateTime AddedAt { get; set; }

    [JsonIgnore]
    public int CursorCount => Cursors.Count(kv => !string.IsNullOrWhiteSpace(kv.Value));

    [JsonIgnore]
    public int MissingRoleCount => CursorSystemRoleNames.All.Count(role => !Cursors.ContainsKey(role) || string.IsNullOrWhiteSpace(Cursors[role]));

    [JsonIgnore]
    public bool IsComplete => MissingRoleCount == 0;

    [JsonIgnore]
    public bool HasAnimated => Cursors.Values.Any(v => v.EndsWith(".ani", StringComparison.OrdinalIgnoreCase));

    [JsonIgnore]
    public bool HasStatic => Cursors.Values.Any(v => v.EndsWith(".cur", StringComparison.OrdinalIgnoreCase));

    [JsonIgnore]
    public string FavoriteMark => IsFavorite ? "★" : "☆";

    [JsonIgnore]
    public string FavoriteToolTip => LocalizationService.Translate("Favori", LocalizationService.CurrentLanguage);

    [JsonIgnore]
    public string CompletenessText => LocalizationService.Format("{0} / {1} rôles", LocalizationService.CurrentLanguage, CursorCount, CursorSystemRoleNames.All.Count);

    [JsonIgnore]
    public string CreatorDisplay => string.IsNullOrWhiteSpace(OriginalAuthor) ? Author : OriginalAuthor;

    [JsonIgnore]
    public string CreatorLine => LocalizationService.Format(
        "Créateur original : {0}",
        LocalizationService.CurrentLanguage,
        LocalizationService.TranslateMetadata(CreatorDisplay, LocalizationService.CurrentLanguage));

    [JsonIgnore]
    public string FormatText => HasAnimated && HasStatic ? "CUR + ANI" : HasAnimated ? "ANI" : "CUR";

    [JsonIgnore]
    public string VariantLine => string.IsNullOrWhiteSpace(VariantName) ? "" : $"{VariantGroup} • {VariantName}";
}

public static class CursorSystemRoleNames
{
    public static readonly IReadOnlyList<string> All = new[]
    {
        "Arrow", "Help", "AppStarting", "Wait", "Crosshair", "IBeam", "NWPen", "No",
        "SizeNS", "SizeWE", "SizeNWSE", "SizeNESW", "SizeAll", "UpArrow", "Hand", "Pin", "Person"
    };
}

public sealed class CursorRoleRow
{
    public string Role { get; init; } = "";
    public string DisplayRole { get; init; } = "";
    public string File { get; init; } = "";
    public string FullPath { get; init; } = "";
    public string Status { get; init; } = "";
}

public sealed class WindowsCursorScheme
{
    public string Name { get; init; } = "";
    public string Source { get; init; } = "";
    public bool IsActive { get; set; }
    public Dictionary<string, string> Cursors { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    public int CursorCount => Cursors.Count(kv => !string.IsNullOrWhiteSpace(kv.Value));
    public string ActiveText => IsActive ? LocalizationService.Translate("ACTIF", LocalizationService.CurrentLanguage) : "";
    public string SourceDisplay => LocalizationService.TranslateMetadata(Source, LocalizationService.CurrentLanguage);
    public string CursorCountText => LocalizationService.Format("{0} rôles configurés", LocalizationService.CurrentLanguage, CursorCount);
}

public sealed class PackAnalysisResult
{
    public int RoleCount { get; init; }
    public int CurCount { get; init; }
    public int AniCount { get; init; }
    public int MissingFiles { get; init; }
    public int InvalidFiles { get; init; }
    public int DuplicateFiles { get; init; }
    public long SizeBytes { get; init; }
}

public sealed class PackValidationResult
{
    public List<string> MissingRoles { get; } = new();
    public List<string> MissingFiles { get; } = new();
    public List<string> InvalidFiles { get; } = new();
    public bool IsValid => MissingFiles.Count == 0 && InvalidFiles.Count == 0;
    public bool IsComplete => MissingRoles.Count == 0;
}

public enum MissingRoleBehavior
{
    KeepCurrent,
    WindowsDefault,
    ChooseManually
}
