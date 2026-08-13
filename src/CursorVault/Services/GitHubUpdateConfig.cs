namespace CursorVault.Services;

/// <summary>
/// Configuration GitHub utilisée par le système de mise à jour.
/// Modifie Owner une seule fois avant de publier CursorVault.
/// L'utilisateur final n'a rien à configurer dans l'application.
/// </summary>
public static class GitHubUpdateConfig
{
    // Exemple : si ton dépôt est https://github.com/MonCompte/CursorVault
    // remplace la valeur ci-dessous par MonCompte.
    public const string Owner = "GaLeX-Le-Penguin";

    public const string Repository = "CursorVault";

    // Le ZIP joint à chaque GitHub Release doit garder exactement ce nom.
    public const string ReleaseAssetName = "CursorVault.zip";

    public static string LatestReleaseApiUrl =>
        $"https://api.github.com/repos/{Owner}/{Repository}/releases/latest";

    public static string LatestReleasePageUrl =>
        $"https://github.com/{Owner}/{Repository}/releases/latest";
}
