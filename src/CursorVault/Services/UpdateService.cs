using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;

namespace CursorVault.Services;

public sealed record UpdateCheckResult(
    bool UpdateAvailable,
    Version CurrentVersion,
    Version LatestVersion,
    string DownloadUrl,
    string Notes);

public sealed class UpdateService
{
    private static readonly HttpClient Client = CreateClient();

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("CursorVault/1.1");
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        client.DefaultRequestHeaders.TryAddWithoutValidation("X-GitHub-Api-Version", "2026-03-10");
        return client;
    }

    public async Task<UpdateCheckResult> CheckAsync()
    {
        EnsureConfigured();

        using var response = await Client.GetAsync(GitHubUpdateConfig.LatestReleaseApiUrl);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"GitHub n'a pas pu fournir la dernière version de CursorVault (HTTP {(int)response.StatusCode}). " +
                "Vérifie le nom du compte, le dépôt et qu'une Release publique existe.");
        }

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var tag = root.TryGetProperty("tag_name", out var tagNode)
            ? tagNode.GetString() ?? ""
            : "";

        if (!Version.TryParse(tag.Trim().TrimStart('v', 'V'), out var latest))
            throw new InvalidOperationException($"Le tag de la dernière Release GitHub ('{tag}') n'est pas une version valide. Utilise un tag comme v1.2.0.");

        var current = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(1, 0, 0, 0);
        latest = Normalize(latest);
        current = Normalize(current);

        var notes = root.TryGetProperty("body", out var bodyNode)
            ? bodyNode.GetString() ?? ""
            : "";

        var downloadUrl = "";
        if (root.TryGetProperty("assets", out var assetsNode) && assetsNode.ValueKind == JsonValueKind.Array)
        {
            foreach (var asset in assetsNode.EnumerateArray())
            {
                var name = asset.TryGetProperty("name", out var nameNode) ? nameNode.GetString() ?? "" : "";
                if (!name.Equals(GitHubUpdateConfig.ReleaseAssetName, StringComparison.OrdinalIgnoreCase))
                    continue;

                downloadUrl = asset.TryGetProperty("browser_download_url", out var urlNode)
                    ? urlNode.GetString() ?? ""
                    : "";
                break;
            }
        }

        // Si le ZIP n'est pas joint, ouvre au minimum la page de la Release.
        if (string.IsNullOrWhiteSpace(downloadUrl))
        {
            downloadUrl = root.TryGetProperty("html_url", out var htmlNode)
                ? htmlNode.GetString() ?? GitHubUpdateConfig.LatestReleasePageUrl
                : GitHubUpdateConfig.LatestReleasePageUrl;
        }

        return new UpdateCheckResult(latest > current, current, latest, downloadUrl, notes);
    }

    private static void EnsureConfigured()
    {
        if (string.IsNullOrWhiteSpace(GitHubUpdateConfig.Owner) ||
            GitHubUpdateConfig.Owner.Contains("VOTRE_COMPTE", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Le compte GitHub de CursorVault n'est pas encore configuré. " +
                "Ouvre Services/GitHubUpdateConfig.cs et remplace VOTRE_COMPTE_GITHUB par ton nom de compte GitHub.");
        }
    }

    private static Version Normalize(Version v) =>
        new(v.Major, v.Minor, Math.Max(v.Build, 0), Math.Max(v.Revision, 0));
}
