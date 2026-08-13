using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace CursorVault.Services;

public static class LocalizationService
{
    public sealed record LanguageOption(string Code, string DisplayName);

    public static IReadOnlyList<LanguageOption> Languages { get; } = new[]
    {
        new LanguageOption("fr-FR", "Français"),
        new LanguageOption("en-US", "English"),
        new LanguageOption("es-ES", "Español"),
        new LanguageOption("de-DE", "Deutsch"),
        new LanguageOption("it-IT", "Italiano")
    };

    private static readonly DependencyProperty OriginalTextProperty =
        DependencyProperty.RegisterAttached("OriginalText", typeof(string), typeof(LocalizationService));
    private static readonly DependencyProperty OriginalContentProperty =
        DependencyProperty.RegisterAttached("OriginalContent", typeof(string), typeof(LocalizationService));
    private static readonly DependencyProperty OriginalHeaderProperty =
        DependencyProperty.RegisterAttached("OriginalHeader", typeof(string), typeof(LocalizationService));
    private static readonly DependencyProperty OriginalToolTipProperty =
        DependencyProperty.RegisterAttached("OriginalToolTip", typeof(string), typeof(LocalizationService));
    private static readonly DependencyProperty OriginalTitleProperty =
        DependencyProperty.RegisterAttached("OriginalTitle", typeof(string), typeof(LocalizationService));

    private static readonly IReadOnlyDictionary<string, Dictionary<string, string>> Maps =
        new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["en-US"] = BuildEnglish(),
            ["es-ES"] = BuildSpanish(),
            ["de-DE"] = BuildGerman(),
            ["it-IT"] = BuildItalian()
        };

    public static string CurrentLanguage { get; private set; } = "fr-FR";

    public static void SetLanguage(string? code)
    {
        CurrentLanguage = NormalizeLanguage(code);
    }

    public static string DetectSystemLanguage()
    {
        try
        {
            var culture = CultureInfo.InstalledUICulture;
            return culture.TwoLetterISOLanguageName.ToLowerInvariant() switch
            {
                "fr" => "fr-FR",
                "en" => "en-US",
                "es" => "es-ES",
                "de" => "de-DE",
                "it" => "it-IT",
                _ => "en-US"
            };
        }
        catch
        {
            return "en-US";
        }
    }

    public static string Format(string frenchFormat, string? language, params object?[] args)
        => string.Format(CultureInfo.GetCultureInfo(NormalizeLanguage(language)), Translate(frenchFormat, language), args);

    public static string TranslateMetadata(string? value, string? language)
    {
        if (string.IsNullOrWhiteSpace(value)) return value ?? "";
        var canonical = value.Trim() switch
        {
            "Import local" => "Importation locale",
            "Auteur inconnu (import local)" => "Auteur inconnu (importation locale)",
            _ => value.Trim()
        };
        return Translate(canonical, language);
    }

    public static string TranslateRole(string role, string? language)
    {
        var key = role switch
        {
            "Arrow" => "Sélection normale",
            "Help" => "Sélection d’aide",
            "AppStarting" => "Travail en arrière-plan",
            "Wait" => "Occupé",
            "Crosshair" => "Sélection de précision",
            "IBeam" => "Sélection de texte",
            "NWPen" => "Écriture manuscrite",
            "No" => "Non disponible",
            "SizeNS" => "Redimensionnement vertical",
            "SizeWE" => "Redimensionnement horizontal",
            "SizeNWSE" => "Redimensionnement diagonal 1",
            "SizeNESW" => "Redimensionnement diagonal 2",
            "SizeAll" => "Déplacer",
            "UpArrow" => "Sélection alternative",
            "Hand" => "Sélection de lien",
            "Pin" => "Sélection d’emplacement",
            "Person" => "Sélection de personne",
            _ => role
        };
        return Translate(key, language);
    }

    public static string NormalizeLanguage(string? code)
    {
        foreach (var language in Languages)
            if (language.Code.Equals(code, StringComparison.OrdinalIgnoreCase))
                return language.Code;
        return "fr-FR";
    }

    public static string Translate(string french, string? language)
    {
        var normalized = NormalizeLanguage(language);
        if (normalized.Equals("fr-FR", StringComparison.OrdinalIgnoreCase)) return french;
        if (FeatureMaps.TryGetValue(normalized, out var feature) && feature.TryGetValue(french, out var featureTranslated))
            return featureTranslated;
        if (SupplementalMaps.TryGetValue(normalized, out var extra) && extra.TryGetValue(french, out var extraTranslated))
            return extraTranslated;
        return Maps.TryGetValue(normalized, out var map) && map.TryGetValue(french, out var translated)
            ? translated
            : french;
    }

    public static void Apply(DependencyObject root, string? language)
    {
        var normalized = NormalizeLanguage(language);
        CurrentLanguage = normalized;
        try
        {
            var culture = CultureInfo.GetCultureInfo(normalized);
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;
        }
        catch { }

        ApplyRecursive(root, normalized, new HashSet<DependencyObject>());
    }

    private static void ApplyRecursive(DependencyObject node, string language, HashSet<DependencyObject> visited)
    {
        if (!visited.Add(node)) return;

        switch (node)
        {
            case TextBlock textBlock when !BindingOperations.IsDataBound(textBlock, TextBlock.TextProperty):
                if (!string.IsNullOrWhiteSpace(textBlock.Text))
                {
                    var original = Remember(node, OriginalTextProperty, textBlock.Text);
                    textBlock.Text = Translate(original, language);
                }
                break;
            case ContentControl contentControl when contentControl.Content is string content &&
                                                    !BindingOperations.IsDataBound(contentControl, ContentControl.ContentProperty):
                var originalContent = Remember(node, OriginalContentProperty, content);
                contentControl.Content = Translate(originalContent, language);
                break;
        }

        if (node is HeaderedContentControl headered && headered.Header is string header &&
            !BindingOperations.IsDataBound(headered, HeaderedContentControl.HeaderProperty))
        {
            var originalHeader = Remember(node, OriginalHeaderProperty, header);
            headered.Header = Translate(originalHeader, language);
        }

        if (node is HeaderedItemsControl headeredItems && headeredItems.Header is string itemsHeader &&
            !BindingOperations.IsDataBound(headeredItems, HeaderedItemsControl.HeaderProperty))
        {
            var originalHeader = Remember(node, OriginalHeaderProperty, itemsHeader);
            headeredItems.Header = Translate(originalHeader, language);
        }

        if (node is FrameworkElement element && element.ToolTip is string toolTip)
        {
            var originalToolTip = Remember(node, OriginalToolTipProperty, toolTip);
            element.ToolTip = Translate(originalToolTip, language);
        }

        if (node is Window window && !string.IsNullOrWhiteSpace(window.Title))
        {
            var originalTitle = Remember(node, OriginalTitleProperty, window.Title);
            window.Title = Translate(originalTitle, language);
        }

        if (node is DataGrid dataGrid)
        {
            foreach (var column in dataGrid.Columns)
            {
                if (column.Header is not string columnHeader) continue;
                var originalHeader = Remember(column, OriginalHeaderProperty, columnHeader);
                column.Header = Translate(originalHeader, language);
            }
        }

        if (node is FrameworkElement fe && fe.ContextMenu is not null)
            ApplyRecursive(fe.ContextMenu, language, visited);

        if (node is ItemsControl itemsControl)
        {
            foreach (var item in itemsControl.Items)
                if (item is DependencyObject dependencyItem)
                    ApplyRecursive(dependencyItem, language, visited);
        }

        foreach (var child in LogicalTreeHelper.GetChildren(node))
            if (child is DependencyObject dependencyChild)
                ApplyRecursive(dependencyChild, language, visited);
    }

    private static string Remember(DependencyObject target, DependencyProperty property, string current)
    {
        var remembered = target.GetValue(property) as string;
        if (!string.IsNullOrEmpty(remembered)) return remembered;
        target.SetValue(property, current);
        return current;
    }

    private static readonly IReadOnlyDictionary<string, Dictionary<string, string>> FeatureMaps =
        new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["en-US"] = new(StringComparer.Ordinal)
            {
                ["Favoris d'abord"]="Favorites first", ["Nom A-Z"]="Name A-Z", ["Nom Z-A"]="Name Z-A", ["Créateur"]="Creator", ["Complets d'abord"]="Complete first", ["Plus récemment ajoutés"]="Recently added",
                ["Exporter install.inf"]="Export install.inf", ["Analyser"]="Analyze", ["Thème Windows"]="Windows theme", ["Applique automatiquement un pack clair ou sombre lorsque Windows change de thème."]="Automatically applies a light or dark pack when Windows changes theme.",
                ["Suivre le thème clair / sombre de Windows"]="Follow Windows light / dark theme", ["Pack pour le thème clair"]="Pack for light theme", ["Pack pour le thème sombre"]="Pack for dark theme",
                ["Sauvegarde et mode portable"]="Backup and portable mode", ["Exporte ou restaure toute la configuration et permet de stocker les données à côté de l'exécutable."]="Exports or restores the full configuration and can store data next to the executable.",
                ["Exporter une sauvegarde"]="Export backup", ["Importer une sauvegarde"]="Import backup", ["Mode portable"]="Portable mode", ["Le changement du mode portable prend effet au prochain démarrage."]="Portable mode changes take effect on next startup.",
                ["Tri des packs et taille générale de la fenêtre."]="Pack sorting and general window size.", ["Tri par défaut"]="Default sorting", ["Taille de l'interface"]="Interface size", ["Compacte"]="Compact", ["Normale"]="Normal", ["Grande"]="Large",
                ["Diagnostic et stockage"]="Diagnostics and storage", ["Informations techniques, consommation disque et nettoyage du cache temporaire."]="Technical information, disk usage and temporary cache cleanup.", ["Afficher le diagnostic"]="Show diagnostics", ["Copier le diagnostic"]="Copy diagnostics", ["Nettoyer le cache"]="Clear cache",
                ["Mises à jour"]="Updates", ["Vérifie automatiquement la dernière version publiée sur GitHub."]="Automatically checks the latest version published on GitHub.", ["Source : GitHub Releases"]="Source: GitHub Releases", ["Version actuelle : {0}"]="Current version: {0}", ["Rechercher les mises à jour"]="Check for updates", ["Diagnostic"]="Diagnostics", ["État de CursorVault, environnement Windows et utilisation du stockage."]="CursorVault status, Windows environment and storage usage.", ["Variantes liées"]="Linked variants", ["Variante : {0}"]="Variant: {0}", ["{0} liée(s)"]="{0} linked"
            },
            ["es-ES"] = new(StringComparer.Ordinal)
            {
                ["Favoris d'abord"]="Favoritos primero", ["Nom A-Z"]="Nombre A-Z", ["Nom Z-A"]="Nombre Z-A", ["Créateur"]="Creador", ["Complets d'abord"]="Completos primero", ["Plus récemment ajoutés"]="Añadidos recientemente",
                ["Exporter install.inf"]="Exportar install.inf", ["Analyser"]="Analizar", ["Thème Windows"]="Tema de Windows", ["Applique automatiquement un pack clair ou sombre lorsque Windows change de thème."]="Aplica automáticamente un paquete claro u oscuro cuando Windows cambia de tema.", ["Suivre le thème clair / sombre de Windows"]="Seguir tema claro / oscuro de Windows", ["Pack pour le thème clair"]="Pack para tema claro", ["Pack pour le thème sombre"]="Pack para tema oscuro",
                ["Sauvegarde et mode portable"]="Copia de seguridad y modo portátil", ["Exporte ou restaure toute la configuration et permet de stocker les données à côté de l'exécutable."]="Exporta o restaura toda la configuración y permite guardar los datos junto al ejecutable.", ["Exporter une sauvegarde"]="Exportar copia", ["Importer une sauvegarde"]="Importar copia", ["Mode portable"]="Modo portátil", ["Le changement du mode portable prend effet au prochain démarrage."]="El cambio de modo portátil se aplica en el próximo inicio.", ["Tri des packs et taille générale de la fenêtre."]="Orden de paquetes y tamaño general de la ventana.", ["Tri par défaut"]="Orden predeterminado", ["Taille de l'interface"]="Tamaño de interfaz", ["Compacte"]="Compacta", ["Normale"]="Normal", ["Grande"]="Grande",
                ["Diagnostic et stockage"]="Diagnóstico y almacenamiento", ["Informations techniques, consommation disque et nettoyage du cache temporaire."]="Información técnica, uso de disco y limpieza de caché temporal.", ["Afficher le diagnostic"]="Mostrar diagnóstico", ["Copier le diagnostic"]="Copiar diagnóstico", ["Nettoyer le cache"]="Limpiar caché", ["Mises à jour"]="Actualizaciones", ["Vérifie automatiquement la dernière version publiée sur GitHub."]="Comprueba automáticamente la última versión publicada en GitHub.", ["Source : GitHub Releases"]="Fuente: GitHub Releases", ["Version actuelle : {0}"]="Versión actual: {0}", ["Rechercher les mises à jour"]="Buscar actualizaciones", ["Diagnostic"]="Diagnóstico", ["État de CursorVault, environnement Windows et utilisation du stockage."]="Estado de CursorVault, entorno Windows y uso del almacenamiento.", ["Variantes liées"]="Variantes vinculadas", ["Variante : {0}"]="Variante: {0}", ["{0} liée(s)"]="{0} vinculada(s)"
            },
            ["de-DE"] = new(StringComparer.Ordinal)
            {
                ["Favoris d'abord"]="Favoriten zuerst", ["Nom A-Z"]="Name A-Z", ["Nom Z-A"]="Name Z-A", ["Créateur"]="Ersteller", ["Complets d'abord"]="Vollständige zuerst", ["Plus récemment ajoutés"]="Zuletzt hinzugefügt",
                ["Exporter install.inf"]="install.inf exportieren", ["Analyser"]="Analysieren", ["Thème Windows"]="Windows-Design", ["Applique automatiquement un pack clair ou sombre lorsque Windows change de thème."]="Wendet automatisch ein helles oder dunkles Paket an, wenn Windows das Design wechselt.", ["Suivre le thème clair / sombre de Windows"]="Helles / dunkles Windows-Design übernehmen", ["Pack pour le thème clair"]="Paket für helles Design", ["Pack pour le thème sombre"]="Paket für dunkles Design",
                ["Sauvegarde et mode portable"]="Sicherung und portabler Modus", ["Exporte ou restaure toute la configuration et permet de stocker les données à côté de l'exécutable."]="Exportiert oder stellt die gesamte Konfiguration wieder her und kann Daten neben der ausführbaren Datei speichern.", ["Exporter une sauvegarde"]="Sicherung exportieren", ["Importer une sauvegarde"]="Sicherung importieren", ["Mode portable"]="Portabler Modus", ["Le changement du mode portable prend effet au prochain démarrage."]="Die Änderung des portablen Modus wird beim nächsten Start wirksam.", ["Tri des packs et taille générale de la fenêtre."]="Paketsortierung und allgemeine Fenstergröße.", ["Tri par défaut"]="Standardsortierung", ["Taille de l'interface"]="Oberflächengröße", ["Compacte"]="Kompakt", ["Normale"]="Normal", ["Grande"]="Groß",
                ["Diagnostic et stockage"]="Diagnose und Speicher", ["Informations techniques, consommation disque et nettoyage du cache temporaire."]="Technische Informationen, Speicherbelegung und Bereinigung des temporären Caches.", ["Afficher le diagnostic"]="Diagnose anzeigen", ["Copier le diagnostic"]="Diagnose kopieren", ["Nettoyer le cache"]="Cache leeren", ["Mises à jour"]="Updates", ["Vérifie automatiquement la dernière version publiée sur GitHub."]="Prüft automatisch die neueste auf GitHub veröffentlichte Version.", ["Source : GitHub Releases"]="Quelle: GitHub Releases", ["Version actuelle : {0}"]="Aktuelle Version: {0}", ["Rechercher les mises à jour"]="Nach Updates suchen", ["Diagnostic"]="Diagnose", ["État de CursorVault, environnement Windows et utilisation du stockage."]="CursorVault-Status, Windows-Umgebung und Speichernutzung.", ["Variantes liées"]="Verknüpfte Varianten", ["Variante : {0}"]="Variante: {0}", ["{0} liée(s)"]="{0} verknüpft"
            },
            ["it-IT"] = new(StringComparer.Ordinal)
            {
                ["Favoris d'abord"]="Preferiti prima", ["Nom A-Z"]="Nome A-Z", ["Nom Z-A"]="Nome Z-A", ["Créateur"]="Autore", ["Complets d'abord"]="Completi prima", ["Plus récemment ajoutés"]="Aggiunti di recente",
                ["Exporter install.inf"]="Esporta install.inf", ["Analyser"]="Analizza", ["Thème Windows"]="Tema Windows", ["Applique automatiquement un pack clair ou sombre lorsque Windows change de thème."]="Applica automaticamente un pacchetto chiaro o scuro quando Windows cambia tema.", ["Suivre le thème clair / sombre de Windows"]="Segui tema chiaro / scuro di Windows", ["Pack pour le thème clair"]="Pacchetto per tema chiaro", ["Pack pour le thème sombre"]="Pacchetto per tema scuro",
                ["Sauvegarde et mode portable"]="Backup e modalità portatile", ["Exporte ou restaure toute la configuration et permet de stocker les données à côté de l'exécutable."]="Esporta o ripristina l'intera configurazione e consente di memorizzare i dati accanto all'eseguibile.", ["Exporter une sauvegarde"]="Esporta backup", ["Importer une sauvegarde"]="Importa backup", ["Mode portable"]="Modalità portatile", ["Le changement du mode portable prend effet au prochain démarrage."]="La modifica della modalità portatile ha effetto al prossimo avvio.", ["Tri des packs et taille générale de la fenêtre."]="Ordinamento dei pacchetti e dimensione generale della finestra.", ["Tri par défaut"]="Ordinamento predefinito", ["Taille de l'interface"]="Dimensione interfaccia", ["Compacte"]="Compatta", ["Normale"]="Normale", ["Grande"]="Grande",
                ["Diagnostic et stockage"]="Diagnostica e archiviazione", ["Informations techniques, consommation disque et nettoyage du cache temporaire."]="Informazioni tecniche, utilizzo del disco e pulizia della cache temporanea.", ["Afficher le diagnostic"]="Mostra diagnostica", ["Copier le diagnostic"]="Copia diagnostica", ["Nettoyer le cache"]="Pulisci cache", ["Mises à jour"]="Aggiornamenti", ["Vérifie automatiquement la dernière version publiée sur GitHub."]="Controlla automaticamente l’ultima versione pubblicata su GitHub.", ["Source : GitHub Releases"]="Fonte: GitHub Releases", ["Version actuelle : {0}"]="Versione attuale: {0}", ["Rechercher les mises à jour"]="Cerca aggiornamenti", ["Diagnostic"]="Diagnostica", ["État de CursorVault, environnement Windows et utilisation du stockage."]="Stato di CursorVault, ambiente Windows e utilizzo dello spazio.", ["Variantes liées"]="Varianti collegate", ["Variante : {0}"]="Variante: {0}", ["{0} liée(s)"]="{0} collegata/e"
            }
        };

    private static readonly IReadOnlyDictionary<string, Dictionary<string, string>> SupplementalMaps =
        new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["en-US"] = new(StringComparer.Ordinal)
            {
                ["Créateur original"] = "Original creator", ["Créateur original : {0}"] = "Original creator: {0}", ["{0} / {1} rôles"] = "{0} / {1} roles", ["{0} rôles configurés"] = "{0} roles configured",
                ["Import local"] = "Local import", ["Importation locale"] = "Local import", ["Auteur inconnu (import local)"] = "Unknown author (local import)", ["Auteur inconnu (importation locale)"] = "Unknown author (local import)", ["Système"] = "System", ["Utilisateur"] = "User", ["Windows actif"] = "Active Windows configuration", ["Configuration actuelle (personnalisée)"] = "Current configuration (custom)",
                ["Complet"] = "Complete", ["Incomplet"] = "Incomplete", ["Disponible"] = "Available", ["Manquant"] = "Missing", ["Absent"] = "Missing file", ["Invalide"] = "Invalid", ["Par défaut"] = "Default", ["(non fourni)"] = "(not provided)", ["(Windows par défaut)"] = "(Windows default)",
                ["Format : {0}"] = "Format: {0}", ["Taille : {0} • Ajouté : {1}"] = "Size: {0} • Added: {1}", ["{0} pack(s) installé(s) localement"] = "{0} pack(s) installed locally", ["{0} modèle(s) enregistré(s) dans Windows"] = "{0} scheme(s) registered in Windows",
                ["modèle actuellement actif"] = "currently active scheme", ["rôles configurés"] = "roles configured", ["Configuration personnalisée"] = "Custom configuration", ["Inconnu"] = "Unknown",
                ["Pack complet et prêt à être appliqué."] = "Pack is complete and ready to apply.", ["Pack incomplet : {0} rôle(s) manquant(s). Le comportement défini dans Paramètres sera utilisé."] = "Incomplete pack: {0} role(s) missing. The behavior configured in Settings will be used.", ["Pack à réparer : {0} fichier(s) absent(s), {1} référence(s) invalide(s)."] = "Pack needs repair: {0} missing file(s), {1} invalid reference(s).",
                ["Sélectionnez d'abord un pack."] = "Select a pack first.", ["Pack « {0} » appliqué."] = "Pack “{0}” applied.", ["Application annulée."] = "Application canceled.", ["Sélectionnez d'abord un modèle Windows."] = "Select a Windows scheme first.", ["Modèle Windows « {0} » appliqué."] = "Windows scheme “{0}” applied.",
                ["Ce modèle est actuellement utilisé par Windows."] = "This scheme is currently used by Windows.", ["Sélectionnez un rôle pour prévisualiser son curseur."] = "Select a role to preview its cursor.", ["Ce rôle utilise le curseur Windows par défaut."] = "This role uses the default Windows cursor.",
                ["Favori"] = "Favorite", ["Rechercher par nom, créateur ou description"] = "Search by name, creator or description", ["Rechercher un modèle Windows"] = "Search for a Windows scheme", ["Menu Pointeurs"] = "Pointer menu", ["Dossier Cursors"] = "Cursors folder", ["Menu Windows"] = "Windows menu",
                ["Les packs importés sont copiés dans %LocalAppData%\\CursorVault\\Packs. Les fichiers sont vérifiés avant chaque application."] = "Imported packs are copied to %LocalAppData%\\CursorVault\\Packs. Files are checked before each application.",
                ["Données"] = "Data", ["Démarrage automatique désactivé."] = "Automatic startup disabled.", ["Le fonctionnement en arrière-plan est activé."] = "Background operation enabled.", ["Le fonctionnement en arrière-plan est désactivé."] = "Background operation disabled.", ["Comportement des rôles manquants enregistré."] = "Missing-role behavior saved.", ["Rotation automatique désactivée."] = "Automatic rotation disabled.", ["favoris uniquement"] = "favorites only",
                ["Pack importé"] = "Imported pack", ["Aucun pack"] = "No pack", ["Importez un dossier, un ZIP ou des fichiers .cur / .ani."] = "Import a folder, ZIP, or .cur / .ani files.", ["Aucun pack importé."] = "No pack imported.", ["{0} pack(s) importé(s) et installé(s) dans CursorVault."] = "{0} pack(s) imported and installed in CursorVault.",
                ["Sélection normale"] = "Normal select", ["Sélection d’aide"] = "Help select", ["Travail en arrière-plan"] = "Working in background", ["Occupé"] = "Busy", ["Sélection de précision"] = "Precision select", ["Sélection de texte"] = "Text select", ["Écriture manuscrite"] = "Handwriting", ["Non disponible"] = "Unavailable", ["Redimensionnement vertical"] = "Vertical resize", ["Redimensionnement horizontal"] = "Horizontal resize", ["Redimensionnement diagonal 1"] = "Diagonal resize 1", ["Redimensionnement diagonal 2"] = "Diagonal resize 2", ["Déplacer"] = "Move", ["Sélection alternative"] = "Alternate select", ["Sélection de lien"] = "Link select", ["Sélection d’emplacement"] = "Location select", ["Sélection de personne"] = "Person select",
                ["Créer un pack CursorVault"] = "Create a CursorVault pack", ["Créateur de pack"] = "Pack creator", ["Le créateur original est obligatoire et restera affiché même si le pack est renommé."] = "The original creator is required and will remain visible even if the pack is renamed.", ["Nom du pack"] = "Pack name", ["Description"] = "Description", ["Associer les rôles Windows"] = "Assign Windows roles", ["Choisir…"] = "Choose…", ["Annuler"] = "Cancel", ["CRÉER LE PACK"] = "CREATE PACK", ["Non défini"] = "Not set", ["Valider"] = "Confirm",
                ["Renommer le pack"] = "Rename pack", ["Nouveau nom. Le créateur original « {0} » restera affiché :"] = "New name. Original creator “{0}” will remain displayed:", ["Pack renommé en « {0} ». Créateur original conservé : {1}."] = "Pack renamed to “{0}”. Original creator preserved: {1}.", ["Pack « {0} » créé. Créateur original : {1}."] = "Pack “{0}” created. Original creator: {1}.",
                ["Sélectionnez au moins un fichier curseur."] = "Select at least one cursor file.", ["Le nom du pack est obligatoire."] = "Pack name is required.", ["Le créateur original est obligatoire."] = "Original creator is required.", ["Curseur pour {0}"] = "Cursor for {0}", ["Curseurs Windows (*.cur;*.ani)|*.cur;*.ani|Tous les fichiers (*.*)|*.*"] = "Windows cursors (*.cur;*.ani)|*.cur;*.ani|All files (*.*)|*.*",
                ["Aucun modèle Windows détecté"] = "No Windows scheme detected",
                ["Aucun schéma n'a été trouvé dans le Registre Windows."] = "No scheme was found in the Windows Registry.",
                ["Bibliothèque actualisée."] = "Library refreshed.",
                ["Choisir un curseur pour le rôle manquant : {0}"] = "Choose a cursor for the missing role: {0}",
                ["Choisissez un dossier contenant des curseurs"] = "Choose a folder containing cursors",
                ["Configuration présente avant le premier lancement de CursorVault restaurée."] = "Configuration from before the first CursorVault launch restored.",
                ["Exporter le pack CursorVault"] = "Export CursorVault pack",
                ["Importer des curseurs ou des archives"] = "Import cursors or archives",
                ["Ouvrez le menu Pointeurs Windows pour vérifier les schémas installés."] = "Open the Windows Pointers menu to check installed schemes.",
                ["Pack aléatoire : « {0} »."] = "Random pack: “{0}”.",
                ["Pack « {0} » supprimé de CursorVault."] = "Pack “{0}” removed from CursorVault.",
                ["Rotation activée : {0}{1}."] = "Rotation enabled: {0}{1}.",
                ["Rotation automatique : « {0} »."] = "Automatic rotation: “{0}”.",
                ["Réparation terminée : {0} référence(s) cassée(s) retirée(s). Le pack contient maintenant {1}/{2} rôles."] = "Repair complete: {0} broken reference(s) removed. The pack now contains {1}/{2} roles.",
                ["Supprimer « {0} » de CursorVault ?\n\nCréateur original : {1}\nLes fichiers du pack seront supprimés de la bibliothèque locale."] = "Remove “{0}” from CursorVault?\n\nOriginal creator: {1}\nThe pack files will be deleted from the local library.",
                ["« {0} » ajouté aux favoris."] = "“{0}” added to favorites.",
                ["« {0} » retiré des favoris."] = "“{0}” removed from favorites.",
                ["Rose clair avec contour profond."] = "Light rose with a deep outline.",
                ["Pack Minecraft Netherite animé. 11 rôles Windows standards fournis par le schéma original."] = "Animated Minecraft Netherite pack. 11 standard Windows roles provided by the original scheme.",
                ["Pack sombre et épais avec animations d’attente. 15 rôles Windows standards."] = "Dark, bold pack with waiting animations. 15 standard Windows roles.",
                ["Pack Overwatch complet avec curseurs statiques et animations."] = "Complete Overwatch pack with static and animated cursors.",
                ["Concept de curseurs Windows 11 — variante light."] = "Windows 11 cursor concept — light variant.",
                ["Concept de curseurs Windows 11 — variante dark."] = "Windows 11 cursor concept — dark variant.",
                ["Pack Legend of Zelda animé. 15 rôles Windows standards, avec quelques variantes supplémentaires."] = "Animated Legend of Zelda pack. 15 standard Windows roles, with a few extra variants.",
                ["Pack Mario Gant. Archive fournie avec crédit au créateur original Behelit. 15 rôles Windows standards."] = "Mario Gant pack. Archive supplied with credit to the original creator Behelit. 15 standard Windows roles.",
                ["Clair, net et légèrement bleuté."] = "Light, clean and slightly bluish.",
                ["Vert lumineux avec contour sombre."] = "Bright green with a dark outline.",
                ["W11 Tail Cursor Concept Free — variante Light, avec pointeur principal à traîne."] = "W11 Tail Cursor Concept Free — Light variant, with a trailing main pointer.",
                ["Aucun favori valide n’est disponible pour la rotation automatique."] = "No valid favorite is available for automatic rotation.",
                ["Aucun pack valide n’est disponible."] = "No valid pack is available.",
                ["Curseurs Windows (*.cur;*.ani)|*.cur;*.ani"] = "Windows cursors (*.cur;*.ani)|*.cur;*.ani",
                ["Impossible de déterminer le dossier Windows."] = "Unable to determine the Windows folder.",
                ["La rotation automatique ignore les packs incomplets lorsque le mode manuel est sélectionné."] = "Automatic rotation skips incomplete packs when manual mode is selected.",
                ["Le pack contient des références cassées. Utilisez Réparer avant de l’appliquer. Fichiers absents : {0}; références invalides : {1}."] = "The pack contains broken references. Use Repair before applying it. Missing files: {0}; invalid references: {1}.",
                ["Pack exporté : {0}"] = "Pack exported: {0}",
            },
            ["es-ES"] = new(StringComparer.Ordinal)
            {
                ["Créateur original"] = "Creador original", ["Créateur original : {0}"] = "Creador original: {0}", ["{0} / {1} rôles"] = "{0} / {1} roles", ["{0} rôles configurés"] = "{0} roles configurados", ["Import local"] = "Importación local", ["Importation locale"] = "Importación local", ["Auteur inconnu (import local)"] = "Autor desconocido (importación local)", ["Auteur inconnu (importation locale)"] = "Autor desconocido (importación local)", ["Système"] = "Sistema", ["Utilisateur"] = "Usuario", ["Windows actif"] = "Configuración activa de Windows", ["Configuration actuelle (personnalisée)"] = "Configuración actual (personalizada)", ["Complet"] = "Completo", ["Incomplet"] = "Incompleto", ["Disponible"] = "Disponible", ["Manquant"] = "Faltante", ["Absent"] = "Archivo ausente", ["Invalide"] = "No válido", ["Par défaut"] = "Predeterminado", ["(non fourni)"] = "(no proporcionado)", ["(Windows par défaut)"] = "(predeterminado de Windows)", ["Configuration personnalisée"] = "Configuración personalizada", ["Inconnu"] = "Desconocido", ["Favori"] = "Favorito", ["Menu Pointeurs"] = "Menú de punteros", ["Dossier Cursors"] = "Carpeta Cursors", ["Menu Windows"] = "Menú de Windows", ["Données"] = "Datos", ["Pack importé"] = "Paquete importado",
                ["Sélection normale"] = "Selección normal", ["Sélection d’aide"] = "Selección de ayuda", ["Travail en arrière-plan"] = "Trabajando en segundo plano", ["Occupé"] = "Ocupado", ["Sélection de précision"] = "Selección de precisión", ["Sélection de texte"] = "Selección de texto", ["Écriture manuscrite"] = "Escritura a mano", ["Non disponible"] = "No disponible", ["Redimensionnement vertical"] = "Cambiar tamaño vertical", ["Redimensionnement horizontal"] = "Cambiar tamaño horizontal", ["Redimensionnement diagonal 1"] = "Cambiar tamaño diagonal 1", ["Redimensionnement diagonal 2"] = "Cambiar tamaño diagonal 2", ["Déplacer"] = "Mover", ["Sélection alternative"] = "Selección alternativa", ["Sélection de lien"] = "Selección de vínculo", ["Sélection d’emplacement"] = "Selección de ubicación", ["Sélection de personne"] = "Selección de persona",
                ["Créer un pack CursorVault"] = "Crear un paquete CursorVault", ["Créateur de pack"] = "Creador de paquetes", ["Le créateur original est obligatoire et restera affiché même si le pack est renommé."] = "El creador original es obligatorio y seguirá visible aunque se cambie el nombre del paquete.", ["Nom du pack"] = "Nombre del paquete", ["Description"] = "Descripción", ["Associer les rôles Windows"] = "Asignar roles de Windows", ["Choisir…"] = "Elegir…", ["Annuler"] = "Cancelar", ["CRÉER LE PACK"] = "CREAR PAQUETE", ["Non défini"] = "Sin definir", ["Valider"] = "Confirmar",
                ["0"] = "0",
                ["PACKS"] = "PAQUETES",
                ["Windows"] = "Windows",
                ["Français"] = "Français",
                ["English"] = "English",
                ["Español"] = "Español",
                ["Deutsch"] = "Deutsch",
                ["Italiano"] = "Italiano",
                ["Application annulée."] = "Aplicación cancelada.",
                ["Aucun modèle Windows détecté"] = "No se detectó ningún esquema de Windows",
                ["Aucun pack"] = "Ningún paquete",
                ["Aucun pack importé."] = "No se importó ningún paquete.",
                ["Aucun schéma n'a été trouvé dans le Registre Windows."] = "No se encontró ningún esquema en el Registro de Windows.",
                ["Bibliothèque actualisée."] = "Biblioteca actualizada.",
                ["Ce modèle est actuellement utilisé par Windows."] = "Este esquema está siendo utilizado por Windows.",
                ["Ce rôle utilise le curseur Windows par défaut."] = "Este rol utiliza el cursor predeterminado de Windows.",
                ["Choisir un curseur pour le rôle manquant : {0}"] = "Elegir un cursor para el rol faltante: {0}",
                ["Choisissez un dossier contenant des curseurs"] = "Elige una carpeta que contenga cursores",
                ["Comportement des rôles manquants enregistré."] = "Comportamiento de roles faltantes guardado.",
                ["Comportement pour les rôles absents d’un pack."] = "Comportamiento para los roles que faltan en un paquete.",
                ["Configuration de l’apparence, des packs incomplets et de la rotation automatique."] = "Configura la apariencia, el idioma, la integración del sistema, los paquetes incompletos y la rotación automática.",
                ["Configuration présente avant le premier lancement de CursorVault restaurée."] = "Se restauró la configuración anterior al primer inicio de CursorVault.",
                ["Curseur pour {0}"] = "Cursor para {0}",
                ["Curseurs Windows (*.cur;*.ani)|*.cur;*.ani|Tous les fichiers (*.*)|*.*"] = "Cursores de Windows (*.cur;*.ani)|*.cur;*.ani|Todos los archivos (*.*)|*.*",
                ["Démarrage automatique désactivé."] = "Inicio automático desactivado.",
                ["Dépose ici un .zip, un dossier ou plusieurs fichiers .cur / .ani depuis l’Explorateur Windows."] = "Suelta aquí un .zip, una carpeta o varios archivos .cur / .ani desde el Explorador de Windows.",
                ["Exporter le pack CursorVault"] = "Exportar paquete CursorVault",
                ["Format : {0}"] = "Formato: {0}",
                ["Importer des curseurs ou des archives"] = "Importar cursores o archivos",
                ["Importez un dossier, un ZIP ou des fichiers .cur / .ani."] = "Importa una carpeta, un ZIP o archivos .cur / .ani.",
                ["La rotation horaire ou quotidienne fonctionne pendant que CursorVault est ouvert. Le mode démarrage s’exécute à chaque lancement."] = "La rotación horaria o diaria funciona mientras CursorVault está abierto. El modo de inicio se ejecuta en cada lanzamiento.",
                ["Le créateur original est obligatoire."] = "El creador original es obligatorio.",
                ["Le fonctionnement en arrière-plan est activé."] = "Funcionamiento en segundo plano activado.",
                ["Le fonctionnement en arrière-plan est désactivé."] = "Funcionamiento en segundo plano desactivado.",
                ["Le nom du pack est obligatoire."] = "El nombre del paquete es obligatorio.",
                ["Les imports sont installés automatiquement dans la bibliothèque locale de CursorVault."] = "Las importaciones se instalan automáticamente en la biblioteca local de CursorVault.",
                ["Les packs importés sont copiés dans %LocalAppData%\\CursorVault\\Packs. Les fichiers sont vérifiés avant chaque application."] = "Los paquetes importados se copian en %LocalAppData%\\CursorVault\\Packs. Los archivos se verifican antes de cada aplicación.",
                ["Modèle Windows « {0} » appliqué."] = "Esquema de Windows “{0}” aplicado.",
                ["Nouveau nom. Le créateur original « {0} » restera affiché :"] = "Nuevo nombre. El creador original “{0}” seguirá mostrándose:",
                ["Ouvrez le menu Pointeurs Windows pour vérifier les schémas installés."] = "Abre el menú Punteros de Windows para comprobar los esquemas instalados.",
                ["Pack aléatoire : « {0} »."] = "Paquete aleatorio: “{0}”.",
                ["Pack complet et prêt à être appliqué."] = "Paquete completo y listo para aplicar.",
                ["Pack incomplet : {0} rôle(s) manquant(s). Le comportement défini dans Paramètres sera utilisé."] = "Paquete incompleto: faltan {0} rol(es). Se usará el comportamiento definido en Configuración.",
                ["Pack renommé en « {0} ». Créateur original conservé : {1}."] = "Paquete renombrado como “{0}”. Creador original conservado: {1}.",
                ["Pack « {0} » appliqué."] = "Paquete “{0}” aplicado.",
                ["Pack « {0} » créé. Créateur original : {1}."] = "Paquete “{0}” creado. Creador original: {1}.",
                ["Pack « {0} » supprimé de CursorVault."] = "Paquete “{0}” eliminado de CursorVault.",
                ["Pack à réparer : {0} fichier(s) absent(s), {1} référence(s) invalide(s)."] = "Paquete para reparar: {0} archivo(s) ausente(s), {1} referencia(s) no válida(s).",
                ["Quand cette option est active, Réduire masque la fenêtre. Le bouton Fermer la place aussi en arrière-plan. Utilisez Quitter CursorVault pour arrêter complètement l’application."] = "Cuando esta opción está activa, Minimizar oculta la ventana. Cerrar también la envía al fondo. Usa Salir de CursorVault para detener por completo la aplicación.",
                ["Rechercher par nom, créateur ou description"] = "Buscar por nombre, creador o descripción",
                ["Rechercher un modèle Windows"] = "Buscar un esquema de Windows",
                ["Renommer le pack"] = "Renombrar paquete",
                ["Rotation activée : {0}{1}."] = "Rotación activada: {0}{1}.",
                ["Rotation automatique : « {0} »."] = "Rotación automática: “{0}”.",
                ["Rotation automatique désactivée."] = "Rotación automática desactivada.",
                ["Réparation terminée : {0} référence(s) cassée(s) retirée(s). Le pack contient maintenant {1}/{2} rôles."] = "Reparación terminada: se eliminaron {0} referencia(s) rota(s). El paquete contiene ahora {1}/{2} roles.",
                ["Supprimer « {0} » de CursorVault ?\n\nCréateur original : {1}\nLes fichiers du pack seront supprimés de la bibliothèque locale."] = "¿Eliminar “{0}” de CursorVault?\n\nCreador original: {1}\nLos archivos del paquete se eliminarán de la biblioteca local.",
                ["Sélectionnez d'abord un modèle Windows."] = "Selecciona primero un esquema de Windows.",
                ["Sélectionnez d'abord un pack."] = "Selecciona primero un paquete.",
                ["Sélectionnez un rôle dans la liste."] = "Selecciona un rol de la lista.",
                ["Sélectionnez un rôle pour prévisualiser son curseur."] = "Selecciona un rol para previsualizar su cursor.",
                ["Sélectionnez une ligne dans la liste."] = "Selecciona una fila de la lista.",
                ["Taille : {0} • Ajouté : {1}"] = "Tamaño: {0} • Añadido: {1}",
                ["favoris uniquement"] = "solo favoritos",
                ["modèle actuellement actif"] = "esquema actualmente activo",
                ["{0} modèle(s) enregistré(s) dans Windows"] = "{0} esquema(s) registrado(s) en Windows",
                ["{0} pack(s) importé(s) et installé(s) dans CursorVault."] = "{0} paquete(s) importado(s) e instalado(s) en CursorVault.",
                ["{0} pack(s) installé(s) localement"] = "{0} paquete(s) instalado(s) localmente",
                ["« {0} » ajouté aux favoris."] = "“{0}” añadido a favoritos.",
                ["« {0} » retiré des favoris."] = "“{0}” eliminado de favoritos.",
                ["Rose clair avec contour profond."] = "Rosa claro con contorno profundo.",
                ["Pack Minecraft Netherite animé. 11 rôles Windows standards fournis par le schéma original."] = "Paquete animado Minecraft Netherite. 11 roles estándar de Windows proporcionados por el esquema original.",
                ["Pack sombre et épais avec animations d’attente. 15 rôles Windows standards."] = "Paquete oscuro y grueso con animaciones de espera. 15 roles estándar de Windows.",
                ["Pack Overwatch complet avec curseurs statiques et animations."] = "Paquete Overwatch completo con cursores estáticos y animados.",
                ["Concept de curseurs Windows 11 — variante light."] = "Concepto de cursores de Windows 11 — variante clara.",
                ["Concept de curseurs Windows 11 — variante dark."] = "Concepto de cursores de Windows 11 — variante oscura.",
                ["Pack Legend of Zelda animé. 15 rôles Windows standards, avec quelques variantes supplémentaires."] = "Paquete animado de Legend of Zelda. 15 roles estándar de Windows, con algunas variantes adicionales.",
                ["Pack Mario Gant. Archive fournie avec crédit au créateur original Behelit. 15 rôles Windows standards."] = "Paquete Mario Gant. Archivo proporcionado con crédito al creador original Behelit. 15 roles estándar de Windows.",
                ["Clair, net et légèrement bleuté."] = "Claro, limpio y ligeramente azulado.",
                ["Vert lumineux avec contour sombre."] = "Verde brillante con contorno oscuro.",
                ["W11 Tail Cursor Concept Free — variante Light, avec pointeur principal à traîne."] = "W11 Tail Cursor Concept Free — variante Light, con puntero principal con estela.",
                ["Aucun favori valide n’est disponible pour la rotation automatique."] = "No hay ningún favorito válido disponible para la rotación automática.",
                ["Aucun pack valide n’est disponible."] = "No hay ningún paquete válido disponible.",
                ["Curseurs Windows (*.cur;*.ani)|*.cur;*.ani"] = "Cursores de Windows (*.cur;*.ani)|*.cur;*.ani",
                ["Impossible de déterminer le dossier Windows."] = "No se puede determinar la carpeta de Windows.",
                ["La rotation automatique ignore les packs incomplets lorsque le mode manuel est sélectionné."] = "La rotación automática omite los paquetes incompletos cuando está seleccionado el modo manual.",
                ["Le pack contient des références cassées. Utilisez Réparer avant de l’appliquer. Fichiers absents : {0}; références invalides : {1}."] = "El paquete contiene referencias rotas. Usa Reparar antes de aplicarlo. Archivos ausentes: {0}; referencias no válidas: {1}.",
                ["Pack exporté : {0}"] = "Paquete exportado: {0}",
            },
            ["de-DE"] = new(StringComparer.Ordinal)
            {
                ["Créateur original"] = "Originalersteller", ["Créateur original : {0}"] = "Originalersteller: {0}", ["{0} / {1} rôles"] = "{0} / {1} Rollen", ["{0} rôles configurés"] = "{0} Rollen konfiguriert", ["Import local"] = "Lokaler Import", ["Importation locale"] = "Lokaler Import", ["Auteur inconnu (import local)"] = "Unbekannter Autor (lokaler Import)", ["Auteur inconnu (importation locale)"] = "Unbekannter Autor (lokaler Import)", ["Système"] = "System", ["Utilisateur"] = "Benutzer", ["Windows actif"] = "Aktive Windows-Konfiguration", ["Configuration actuelle (personnalisée)"] = "Aktuelle Konfiguration (benutzerdefiniert)", ["Complet"] = "Vollständig", ["Incomplet"] = "Unvollständig", ["Disponible"] = "Verfügbar", ["Manquant"] = "Fehlend", ["Absent"] = "Datei fehlt", ["Invalide"] = "Ungültig", ["Par défaut"] = "Standard", ["(non fourni)"] = "(nicht angegeben)", ["(Windows par défaut)"] = "(Windows-Standard)", ["Configuration personnalisée"] = "Benutzerdefinierte Konfiguration", ["Inconnu"] = "Unbekannt", ["Favori"] = "Favorit", ["Menu Pointeurs"] = "Zeigermenü", ["Dossier Cursors"] = "Cursors-Ordner", ["Menu Windows"] = "Windows-Menü", ["Données"] = "Daten", ["Pack importé"] = "Importiertes Paket",
                ["Sélection normale"] = "Normale Auswahl", ["Sélection d’aide"] = "Hilfeauswahl", ["Travail en arrière-plan"] = "Im Hintergrund beschäftigt", ["Occupé"] = "Ausgelastet", ["Sélection de précision"] = "Präzisionsauswahl", ["Sélection de texte"] = "Textauswahl", ["Écriture manuscrite"] = "Handschrift", ["Non disponible"] = "Nicht verfügbar", ["Redimensionnement vertical"] = "Vertikale Größenänderung", ["Redimensionnement horizontal"] = "Horizontale Größenänderung", ["Redimensionnement diagonal 1"] = "Diagonale Größenänderung 1", ["Redimensionnement diagonal 2"] = "Diagonale Größenänderung 2", ["Déplacer"] = "Verschieben", ["Sélection alternative"] = "Alternative Auswahl", ["Sélection de lien"] = "Linkauswahl", ["Sélection d’emplacement"] = "Standortauswahl", ["Sélection de personne"] = "Personenauswahl",
                ["Créer un pack CursorVault"] = "CursorVault-Paket erstellen", ["Créateur de pack"] = "Paketersteller", ["Le créateur original est obligatoire et restera affiché même si le pack est renommé."] = "Der Originalersteller ist erforderlich und bleibt auch nach dem Umbenennen sichtbar.", ["Nom du pack"] = "Paketname", ["Description"] = "Beschreibung", ["Associer les rôles Windows"] = "Windows-Rollen zuweisen", ["Choisir…"] = "Auswählen…", ["Annuler"] = "Abbrechen", ["CRÉER LE PACK"] = "PAKET ERSTELLEN", ["Non défini"] = "Nicht festgelegt", ["Valider"] = "Bestätigen",
                ["0"] = "0",
                ["PACKS"] = "PAKETE",
                ["Windows"] = "Windows",
                ["Français"] = "Français",
                ["English"] = "English",
                ["Español"] = "Español",
                ["Deutsch"] = "Deutsch",
                ["Italiano"] = "Italiano",
                ["Application annulée."] = "Anwendung abgebrochen.",
                ["Aucun modèle Windows détecté"] = "Kein Windows-Schema erkannt",
                ["Aucun pack"] = "Kein Paket",
                ["Aucun pack importé."] = "Kein Paket importiert.",
                ["Aucun schéma n'a été trouvé dans le Registre Windows."] = "In der Windows-Registrierung wurde kein Schema gefunden.",
                ["Bibliothèque actualisée."] = "Bibliothek aktualisiert.",
                ["Ce modèle est actuellement utilisé par Windows."] = "Dieses Schema wird derzeit von Windows verwendet.",
                ["Ce rôle utilise le curseur Windows par défaut."] = "Diese Rolle verwendet den Windows-Standardcursor.",
                ["Choisir un curseur pour le rôle manquant : {0}"] = "Cursor für die fehlende Rolle auswählen: {0}",
                ["Choisissez un dossier contenant des curseurs"] = "Wähle einen Ordner mit Cursorn",
                ["Comportement des rôles manquants enregistré."] = "Verhalten für fehlende Rollen gespeichert.",
                ["Comportement pour les rôles absents d’un pack."] = "Verhalten für Rollen, die in einem Paket fehlen.",
                ["Configuration de l’apparence, des packs incomplets et de la rotation automatique."] = "Darstellung, Sprache, Systemintegration, unvollständige Pakete und automatischen Wechsel konfigurieren.",
                ["Configuration présente avant le premier lancement de CursorVault restaurée."] = "Konfiguration vor dem ersten Start von CursorVault wiederhergestellt.",
                ["Curseur pour {0}"] = "Cursor für {0}",
                ["Curseurs Windows (*.cur;*.ani)|*.cur;*.ani|Tous les fichiers (*.*)|*.*"] = "Windows-Cursor (*.cur;*.ani)|*.cur;*.ani|Alle Dateien (*.*)|*.*",
                ["Démarrage automatique désactivé."] = "Autostart deaktiviert.",
                ["Dépose ici un .zip, un dossier ou plusieurs fichiers .cur / .ani depuis l’Explorateur Windows."] = "Lege hier eine ZIP-Datei, einen Ordner oder mehrere .cur-/.ani-Dateien aus dem Windows-Explorer ab.",
                ["Exporter le pack CursorVault"] = "CursorVault-Paket exportieren",
                ["Format : {0}"] = "Format: {0}",
                ["Importer des curseurs ou des archives"] = "Cursor oder Archive importieren",
                ["Importez un dossier, un ZIP ou des fichiers .cur / .ani."] = "Importiere einen Ordner, eine ZIP-Datei oder .cur-/.ani-Dateien.",
                ["La rotation horaire ou quotidienne fonctionne pendant que CursorVault est ouvert. Le mode démarrage s’exécute à chaque lancement."] = "Der stündliche oder tägliche Wechsel funktioniert, solange CursorVault geöffnet ist. Der Startmodus wird bei jedem Programmstart ausgeführt.",
                ["Le créateur original est obligatoire."] = "Der Originalersteller ist erforderlich.",
                ["Le fonctionnement en arrière-plan est activé."] = "Hintergrundbetrieb aktiviert.",
                ["Le fonctionnement en arrière-plan est désactivé."] = "Hintergrundbetrieb deaktiviert.",
                ["Le nom du pack est obligatoire."] = "Der Paketname ist erforderlich.",
                ["Les imports sont installés automatiquement dans la bibliothèque locale de CursorVault."] = "Importe werden automatisch in der lokalen CursorVault-Bibliothek installiert.",
                ["Les packs importés sont copiés dans %LocalAppData%\\CursorVault\\Packs. Les fichiers sont vérifiés avant chaque application."] = "Importierte Pakete werden nach %LocalAppData%\\CursorVault\\Packs kopiert. Dateien werden vor jeder Anwendung geprüft.",
                ["Modèle Windows « {0} » appliqué."] = "Windows-Schema „{0}“ angewendet.",
                ["Nouveau nom. Le créateur original « {0} » restera affiché :"] = "Neuer Name. Der Originalersteller „{0}“ bleibt sichtbar:",
                ["Ouvrez le menu Pointeurs Windows pour vérifier les schémas installés."] = "Öffne das Windows-Zeigermenü, um die installierten Schemata zu prüfen.",
                ["Pack aléatoire : « {0} »."] = "Zufälliges Paket: „{0}“.",
                ["Pack complet et prêt à être appliqué."] = "Paket vollständig und bereit zur Anwendung.",
                ["Pack incomplet : {0} rôle(s) manquant(s). Le comportement défini dans Paramètres sera utilisé."] = "Unvollständiges Paket: {0} Rolle(n) fehlen. Das in den Einstellungen festgelegte Verhalten wird verwendet.",
                ["Pack renommé en « {0} ». Créateur original conservé : {1}."] = "Paket in „{0}“ umbenannt. Originalersteller beibehalten: {1}.",
                ["Pack « {0} » appliqué."] = "Paket „{0}“ angewendet.",
                ["Pack « {0} » créé. Créateur original : {1}."] = "Paket „{0}“ erstellt. Originalersteller: {1}.",
                ["Pack « {0} » supprimé de CursorVault."] = "Paket „{0}“ aus CursorVault entfernt.",
                ["Pack à réparer : {0} fichier(s) absent(s), {1} référence(s) invalide(s)."] = "Paket muss repariert werden: {0} Datei(en) fehlen, {1} ungültige Referenz(en).",
                ["Quand cette option est active, Réduire masque la fenêtre. Le bouton Fermer la place aussi en arrière-plan. Utilisez Quitter CursorVault pour arrêter complètement l’application."] = "Wenn diese Option aktiv ist, blendet Minimieren das Fenster aus. Schließen legt es ebenfalls in den Hintergrund. Verwende CursorVault beenden, um die Anwendung vollständig zu beenden.",
                ["Rechercher par nom, créateur ou description"] = "Nach Name, Ersteller oder Beschreibung suchen",
                ["Rechercher un modèle Windows"] = "Windows-Schema suchen",
                ["Renommer le pack"] = "Paket umbenennen",
                ["Rotation activée : {0}{1}."] = "Wechsel aktiviert: {0}{1}.",
                ["Rotation automatique : « {0} »."] = "Automatischer Wechsel: „{0}“.",
                ["Rotation automatique désactivée."] = "Automatischer Wechsel deaktiviert.",
                ["Réparation terminée : {0} référence(s) cassée(s) retirée(s). Le pack contient maintenant {1}/{2} rôles."] = "Reparatur abgeschlossen: {0} defekte Referenz(en) entfernt. Das Paket enthält jetzt {1}/{2} Rollen.",
                ["Supprimer « {0} » de CursorVault ?\n\nCréateur original : {1}\nLes fichiers du pack seront supprimés de la bibliothèque locale."] = "„{0}“ aus CursorVault entfernen?\n\nOriginalersteller: {1}\nDie Paketdateien werden aus der lokalen Bibliothek gelöscht.",
                ["Sélectionnez d'abord un modèle Windows."] = "Wähle zuerst ein Windows-Schema aus.",
                ["Sélectionnez d'abord un pack."] = "Wähle zuerst ein Paket aus.",
                ["Sélectionnez un rôle dans la liste."] = "Wähle eine Rolle aus der Liste.",
                ["Sélectionnez un rôle pour prévisualiser son curseur."] = "Wähle eine Rolle aus, um den Cursor vorzuschauen.",
                ["Sélectionnez une ligne dans la liste."] = "Wähle eine Zeile aus der Liste.",
                ["Taille : {0} • Ajouté : {1}"] = "Größe: {0} • Hinzugefügt: {1}",
                ["favoris uniquement"] = "nur Favoriten",
                ["modèle actuellement actif"] = "derzeit aktives Schema",
                ["{0} modèle(s) enregistré(s) dans Windows"] = "{0} Schema(ta) in Windows registriert",
                ["{0} pack(s) importé(s) et installé(s) dans CursorVault."] = "{0} Paket(e) importiert und in CursorVault installiert.",
                ["{0} pack(s) installé(s) localement"] = "{0} Paket(e) lokal installiert",
                ["« {0} » ajouté aux favoris."] = "„{0}“ zu Favoriten hinzugefügt.",
                ["« {0} » retiré des favoris."] = "„{0}“ aus Favoriten entfernt.",
                ["Rose clair avec contour profond."] = "Helles Rosa mit kräftiger Kontur.",
                ["Pack Minecraft Netherite animé. 11 rôles Windows standards fournis par le schéma original."] = "Animiertes Minecraft-Netherite-Paket. 11 Standard-Windows-Rollen aus dem Originalschema.",
                ["Pack sombre et épais avec animations d’attente. 15 rôles Windows standards."] = "Dunkles, kräftiges Paket mit Warteanimationen. 15 Standard-Windows-Rollen.",
                ["Pack Overwatch complet avec curseurs statiques et animations."] = "Vollständiges Overwatch-Paket mit statischen und animierten Cursorn.",
                ["Concept de curseurs Windows 11 — variante light."] = "Windows-11-Cursor-Konzept — helle Variante.",
                ["Concept de curseurs Windows 11 — variante dark."] = "Windows-11-Cursor-Konzept — dunkle Variante.",
                ["Pack Legend of Zelda animé. 15 rôles Windows standards, avec quelques variantes supplémentaires."] = "Animiertes Legend-of-Zelda-Paket. 15 Standard-Windows-Rollen mit einigen zusätzlichen Varianten.",
                ["Pack Mario Gant. Archive fournie avec crédit au créateur original Behelit. 15 rôles Windows standards."] = "Mario-Gant-Paket. Archiv mit Nennung des Originalerstellers Behelit. 15 Standard-Windows-Rollen.",
                ["Clair, net et légèrement bleuté."] = "Hell, sauber und leicht bläulich.",
                ["Vert lumineux avec contour sombre."] = "Leuchtendes Grün mit dunkler Kontur.",
                ["W11 Tail Cursor Concept Free — variante Light, avec pointeur principal à traîne."] = "W11 Tail Cursor Concept Free — helle Variante mit nachlaufendem Hauptzeiger.",
                ["Aucun favori valide n’est disponible pour la rotation automatique."] = "Für den automatischen Wechsel ist kein gültiger Favorit verfügbar.",
                ["Aucun pack valide n’est disponible."] = "Kein gültiges Paket verfügbar.",
                ["Curseurs Windows (*.cur;*.ani)|*.cur;*.ani"] = "Windows-Cursor (*.cur;*.ani)|*.cur;*.ani",
                ["Impossible de déterminer le dossier Windows."] = "Der Windows-Ordner konnte nicht ermittelt werden.",
                ["La rotation automatique ignore les packs incomplets lorsque le mode manuel est sélectionné."] = "Der automatische Wechsel überspringt unvollständige Pakete, wenn der manuelle Modus gewählt ist.",
                ["Le pack contient des références cassées. Utilisez Réparer avant de l’appliquer. Fichiers absents : {0}; références invalides : {1}."] = "Das Paket enthält defekte Referenzen. Verwende Reparieren vor dem Anwenden. Fehlende Dateien: {0}; ungültige Referenzen: {1}.",
                ["Pack exporté : {0}"] = "Paket exportiert: {0}",
            },
            ["it-IT"] = new(StringComparer.Ordinal)
            {
                ["Créateur original"] = "Creatore originale", ["Créateur original : {0}"] = "Creatore originale: {0}", ["{0} / {1} rôles"] = "{0} / {1} ruoli", ["{0} rôles configurés"] = "{0} ruoli configurati", ["Import local"] = "Importazione locale", ["Importation locale"] = "Importazione locale", ["Auteur inconnu (import local)"] = "Autore sconosciuto (importazione locale)", ["Auteur inconnu (importation locale)"] = "Autore sconosciuto (importazione locale)", ["Système"] = "Sistema", ["Utilisateur"] = "Utente", ["Windows actif"] = "Configurazione Windows attiva", ["Configuration actuelle (personnalisée)"] = "Configurazione attuale (personalizzata)", ["Complet"] = "Completo", ["Incomplet"] = "Incompleto", ["Disponible"] = "Disponibile", ["Manquant"] = "Mancante", ["Absent"] = "File assente", ["Invalide"] = "Non valido", ["Par défaut"] = "Predefinito", ["(non fourni)"] = "(non fornito)", ["(Windows par défaut)"] = "(predefinito di Windows)", ["Configuration personnalisée"] = "Configurazione personalizzata", ["Inconnu"] = "Sconosciuto", ["Favori"] = "Preferito", ["Menu Pointeurs"] = "Menu puntatori", ["Dossier Cursors"] = "Cartella Cursors", ["Menu Windows"] = "Menu Windows", ["Données"] = "Dati", ["Pack importé"] = "Pacchetto importato",
                ["Sélection normale"] = "Selezione normale", ["Sélection d’aide"] = "Selezione guida", ["Travail en arrière-plan"] = "Operazioni in background", ["Occupé"] = "Occupato", ["Sélection de précision"] = "Selezione precisione", ["Sélection de texte"] = "Selezione testo", ["Écriture manuscrite"] = "Scrittura a mano", ["Non disponible"] = "Non disponibile", ["Redimensionnement vertical"] = "Ridimensionamento verticale", ["Redimensionnement horizontal"] = "Ridimensionamento orizzontale", ["Redimensionnement diagonal 1"] = "Ridimensionamento diagonale 1", ["Redimensionnement diagonal 2"] = "Ridimensionamento diagonale 2", ["Déplacer"] = "Sposta", ["Sélection alternative"] = "Selezione alternativa", ["Sélection de lien"] = "Selezione collegamento", ["Sélection d’emplacement"] = "Selezione posizione", ["Sélection de personne"] = "Selezione persona",
                ["Créer un pack CursorVault"] = "Crea un pacchetto CursorVault", ["Créateur de pack"] = "Creatore pacchetto", ["Le créateur original est obligatoire et restera affiché même si le pack est renommé."] = "Il creatore originale è obbligatorio e resterà visibile anche se il pacchetto viene rinominato.", ["Nom du pack"] = "Nome pacchetto", ["Description"] = "Descrizione", ["Associer les rôles Windows"] = "Associa ruoli Windows", ["Choisir…"] = "Scegli…", ["Annuler"] = "Annulla", ["CRÉER LE PACK"] = "CREA PACCHETTO", ["Non défini"] = "Non definito", ["Valider"] = "Conferma",
                ["0"] = "0",
                ["PACKS"] = "PACCHETTI",
                ["Windows"] = "Windows",
                ["Français"] = "Français",
                ["English"] = "English",
                ["Español"] = "Español",
                ["Deutsch"] = "Deutsch",
                ["Italiano"] = "Italiano",
                ["Application annulée."] = "Applicazione annullata.",
                ["Aucun modèle Windows détecté"] = "Nessuno schema Windows rilevato",
                ["Aucun pack"] = "Nessun pacchetto",
                ["Aucun pack importé."] = "Nessun pacchetto importato.",
                ["Aucun schéma n'a été trouvé dans le Registre Windows."] = "Nessuno schema trovato nel Registro di Windows.",
                ["Bibliothèque actualisée."] = "Libreria aggiornata.",
                ["Ce modèle est actuellement utilisé par Windows."] = "Questo schema è attualmente utilizzato da Windows.",
                ["Ce rôle utilise le curseur Windows par défaut."] = "Questo ruolo usa il cursore predefinito di Windows.",
                ["Choisir un curseur pour le rôle manquant : {0}"] = "Scegli un cursore per il ruolo mancante: {0}",
                ["Choisissez un dossier contenant des curseurs"] = "Scegli una cartella contenente cursori",
                ["Comportement des rôles manquants enregistré."] = "Comportamento dei ruoli mancanti salvato.",
                ["Comportement pour les rôles absents d’un pack."] = "Comportamento per i ruoli mancanti in un pacchetto.",
                ["Configuration de l’apparence, des packs incomplets et de la rotation automatique."] = "Configura aspetto, lingua, integrazione di sistema, pacchetti incompleti e rotazione automatica.",
                ["Configuration présente avant le premier lancement de CursorVault restaurée."] = "Ripristinata la configurazione precedente al primo avvio di CursorVault.",
                ["Curseur pour {0}"] = "Cursore per {0}",
                ["Curseurs Windows (*.cur;*.ani)|*.cur;*.ani|Tous les fichiers (*.*)|*.*"] = "Cursori Windows (*.cur;*.ani)|*.cur;*.ani|Tutti i file (*.*)|*.*",
                ["Démarrage automatique désactivé."] = "Avvio automatico disattivato.",
                ["Dépose ici un .zip, un dossier ou plusieurs fichiers .cur / .ani depuis l’Explorateur Windows."] = "Trascina qui un file .zip, una cartella o più file .cur / .ani da Esplora file.",
                ["Exporter le pack CursorVault"] = "Esporta pacchetto CursorVault",
                ["Format : {0}"] = "Formato: {0}",
                ["Importer des curseurs ou des archives"] = "Importa cursori o archivi",
                ["Importez un dossier, un ZIP ou des fichiers .cur / .ani."] = "Importa una cartella, un ZIP o file .cur / .ani.",
                ["La rotation horaire ou quotidienne fonctionne pendant que CursorVault est ouvert. Le mode démarrage s’exécute à chaque lancement."] = "La rotazione oraria o giornaliera funziona mentre CursorVault è aperto. La modalità avvio viene eseguita a ogni lancio.",
                ["Le créateur original est obligatoire."] = "Il creatore originale è obbligatorio.",
                ["Le fonctionnement en arrière-plan est activé."] = "Funzionamento in background attivato.",
                ["Le fonctionnement en arrière-plan est désactivé."] = "Funzionamento in background disattivato.",
                ["Le nom du pack est obligatoire."] = "Il nome del pacchetto è obbligatorio.",
                ["Les imports sont installés automatiquement dans la bibliothèque locale de CursorVault."] = "Le importazioni vengono installate automaticamente nella libreria locale di CursorVault.",
                ["Les packs importés sont copiés dans %LocalAppData%\\CursorVault\\Packs. Les fichiers sont vérifiés avant chaque application."] = "I pacchetti importati vengono copiati in %LocalAppData%\\CursorVault\\Packs. I file vengono verificati prima di ogni applicazione.",
                ["Modèle Windows « {0} » appliqué."] = "Schema Windows “{0}” applicato.",
                ["Nouveau nom. Le créateur original « {0} » restera affiché :"] = "Nuovo nome. Il creatore originale “{0}” resterà visibile:",
                ["Ouvrez le menu Pointeurs Windows pour vérifier les schémas installés."] = "Apri il menu Puntatori di Windows per verificare gli schemi installati.",
                ["Pack aléatoire : « {0} »."] = "Pacchetto casuale: “{0}”.",
                ["Pack complet et prêt à être appliqué."] = "Pacchetto completo e pronto per essere applicato.",
                ["Pack incomplet : {0} rôle(s) manquant(s). Le comportement défini dans Paramètres sera utilisé."] = "Pacchetto incompleto: mancano {0} ruolo/i. Verrà usato il comportamento definito nelle Impostazioni.",
                ["Pack renommé en « {0} ». Créateur original conservé : {1}."] = "Pacchetto rinominato in “{0}”. Creatore originale conservato: {1}.",
                ["Pack « {0} » appliqué."] = "Pacchetto “{0}” applicato.",
                ["Pack « {0} » créé. Créateur original : {1}."] = "Pacchetto “{0}” creato. Creatore originale: {1}.",
                ["Pack « {0} » supprimé de CursorVault."] = "Pacchetto “{0}” rimosso da CursorVault.",
                ["Pack à réparer : {0} fichier(s) absent(s), {1} référence(s) invalide(s)."] = "Pacchetto da riparare: {0} file mancanti, {1} riferimenti non validi.",
                ["Quand cette option est active, Réduire masque la fenêtre. Le bouton Fermer la place aussi en arrière-plan. Utilisez Quitter CursorVault pour arrêter complètement l’application."] = "Quando questa opzione è attiva, Riduci nasconde la finestra. Anche Chiudi la mette in background. Usa Esci da CursorVault per arrestare completamente l’applicazione.",
                ["Rechercher par nom, créateur ou description"] = "Cerca per nome, creatore o descrizione",
                ["Rechercher un modèle Windows"] = "Cerca uno schema Windows",
                ["Renommer le pack"] = "Rinomina pacchetto",
                ["Rotation activée : {0}{1}."] = "Rotazione attivata: {0}{1}.",
                ["Rotation automatique : « {0} »."] = "Rotazione automatica: “{0}”.",
                ["Rotation automatique désactivée."] = "Rotazione automatica disattivata.",
                ["Réparation terminée : {0} référence(s) cassée(s) retirée(s). Le pack contient maintenant {1}/{2} rôles."] = "Riparazione completata: rimossi {0} riferimenti danneggiati. Il pacchetto contiene ora {1}/{2} ruoli.",
                ["Supprimer « {0} » de CursorVault ?\n\nCréateur original : {1}\nLes fichiers du pack seront supprimés de la bibliothèque locale."] = "Rimuovere “{0}” da CursorVault?\n\nCreatore originale: {1}\nI file del pacchetto verranno eliminati dalla libreria locale.",
                ["Sélectionnez d'abord un modèle Windows."] = "Seleziona prima uno schema Windows.",
                ["Sélectionnez d'abord un pack."] = "Seleziona prima un pacchetto.",
                ["Sélectionnez un rôle dans la liste."] = "Seleziona un ruolo dall’elenco.",
                ["Sélectionnez un rôle pour prévisualiser son curseur."] = "Seleziona un ruolo per visualizzare l’anteprima del cursore.",
                ["Sélectionnez une ligne dans la liste."] = "Seleziona una riga dall’elenco.",
                ["Taille : {0} • Ajouté : {1}"] = "Dimensione: {0} • Aggiunto: {1}",
                ["favoris uniquement"] = "solo preferiti",
                ["modèle actuellement actif"] = "schema attualmente attivo",
                ["{0} modèle(s) enregistré(s) dans Windows"] = "{0} schema/i registrato/i in Windows",
                ["{0} pack(s) importé(s) et installé(s) dans CursorVault."] = "{0} pacchetto/i importato/i e installato/i in CursorVault.",
                ["{0} pack(s) installé(s) localement"] = "{0} pacchetto/i installato/i localmente",
                ["« {0} » ajouté aux favoris."] = "“{0}” aggiunto ai preferiti.",
                ["« {0} » retiré des favoris."] = "“{0}” rimosso dai preferiti.",
                ["Rose clair avec contour profond."] = "Rosa chiaro con contorno marcato.",
                ["Pack Minecraft Netherite animé. 11 rôles Windows standards fournis par le schéma original."] = "Pacchetto Minecraft Netherite animato. 11 ruoli Windows standard forniti dallo schema originale.",
                ["Pack sombre et épais avec animations d’attente. 15 rôles Windows standards."] = "Pacchetto scuro e marcato con animazioni di attesa. 15 ruoli Windows standard.",
                ["Pack Overwatch complet avec curseurs statiques et animations."] = "Pacchetto Overwatch completo con cursori statici e animati.",
                ["Concept de curseurs Windows 11 — variante light."] = "Concept di cursori Windows 11 — variante chiara.",
                ["Concept de curseurs Windows 11 — variante dark."] = "Concept di cursori Windows 11 — variante scura.",
                ["Pack Legend of Zelda animé. 15 rôles Windows standards, avec quelques variantes supplémentaires."] = "Pacchetto Legend of Zelda animato. 15 ruoli Windows standard, con alcune varianti aggiuntive.",
                ["Pack Mario Gant. Archive fournie avec crédit au créateur original Behelit. 15 rôles Windows standards."] = "Pacchetto Mario Gant. Archivio fornito con credito al creatore originale Behelit. 15 ruoli Windows standard.",
                ["Clair, net et légèrement bleuté."] = "Chiaro, pulito e leggermente azzurrato.",
                ["Vert lumineux avec contour sombre."] = "Verde brillante con contorno scuro.",
                ["W11 Tail Cursor Concept Free — variante Light, avec pointeur principal à traîne."] = "W11 Tail Cursor Concept Free — variante Light, con puntatore principale con scia.",
                ["Aucun favori valide n’est disponible pour la rotation automatique."] = "Nessun preferito valido è disponibile per la rotazione automatica.",
                ["Aucun pack valide n’est disponible."] = "Nessun pacchetto valido disponibile.",
                ["Curseurs Windows (*.cur;*.ani)|*.cur;*.ani"] = "Cursori Windows (*.cur;*.ani)|*.cur;*.ani",
                ["Impossible de déterminer le dossier Windows."] = "Impossibile determinare la cartella di Windows.",
                ["La rotation automatique ignore les packs incomplets lorsque le mode manuel est sélectionné."] = "La rotazione automatica ignora i pacchetti incompleti quando è selezionata la modalità manuale.",
                ["Le pack contient des références cassées. Utilisez Réparer avant de l’appliquer. Fichiers absents : {0}; références invalides : {1}."] = "Il pacchetto contiene riferimenti danneggiati. Usa Ripara prima di applicarlo. File mancanti: {0}; riferimenti non validi: {1}.",
                ["Pack exporté : {0}"] = "Pacchetto esportato: {0}",
            }
        };

    private static Dictionary<string, string> BuildEnglish() => new(StringComparer.Ordinal)
    {
        ["Gestionnaire de bibliothèque et de schémas de curseurs Windows"] = "Windows cursor library and scheme manager",
        ["PACKS"] = "PACKS", ["FAVORIS"] = "FAVORITES", ["INCOMPLETS"] = "INCOMPLETE", ["SCHÉMA ACTIF"] = "ACTIVE SCHEME",
        ["Actions rapides"] = "Quick actions", ["Les imports sont installés automatiquement dans la bibliothèque locale de CursorVault."] = "Imports are automatically installed in CursorVault's local library.",
        ["Glisser-déposer"] = "Drag and drop", ["Dépose ici un .zip, un dossier ou plusieurs fichiers .cur / .ani depuis l’Explorateur Windows."] = "Drop a .zip, a folder, or multiple .cur / .ani files here from File Explorer.",
        ["Favoris"] = "Favorites", ["Bibliothèque"] = "Library", ["Packs installés localement"] = "Locally installed packs", ["Sélectionnez un pack"] = "Select a pack",
        ["APERÇU"] = "PREVIEW", ["Survolez ici"] = "Hover here", ["Curseur sélectionné"] = "Selected cursor", ["Aucun"] = "None", ["Sélectionnez une ligne dans la liste."] = "Select a row in the list.",
        ["INFORMATIONS"] = "INFORMATION", ["Prêt."] = "Ready.", ["Modèles Windows"] = "Windows schemes", ["Schémas enregistrés dans Windows"] = "Schemes registered in Windows", ["ACTIF"] = "ACTIVE",
        ["Sélectionnez un modèle Windows"] = "Select a Windows scheme", ["Curseur du modèle"] = "Scheme cursor", ["Sélectionnez un rôle dans la liste."] = "Select a role in the list.",
        ["Paramètres"] = "Settings", ["Configuration de l’apparence, des packs incomplets et de la rotation automatique."] = "Configure appearance, language, system integration, incomplete packs and automatic rotation.",
        ["Apparence"] = "Appearance", ["Choisissez le thème sombre, clair ou translucide et la couleur de CursorVault."] = "Choose CursorVault's dark, light or translucent theme and color.",
        ["Thème"] = "Theme", ["Couleur"] = "Color", ["Choisir..."] = "Choose...", ["Police de l’interface"] = "Interface font", ["Police de l’interface appliquée."] = "Interface font applied.",
        ["Langue"] = "Language", ["Langue de l’interface. Le changement est appliqué immédiatement."] = "Interface language. Changes are applied immediately.", ["Automatique (Windows)"] = "Automatic (Windows)",
        ["Intégration Windows"] = "Windows integration", ["Démarrage automatique et fonctionnement en arrière-plan."] = "Automatic startup and background operation.",
        ["Exécuter CursorVault au démarrage de Windows"] = "Run CursorVault when Windows starts", ["Réduire CursorVault en arrière-plan dans la zone de notification"] = "Minimize CursorVault to the notification area",
        ["Quand cette option est active, Réduire masque la fenêtre. Le bouton Fermer la place aussi en arrière-plan. Utilisez Quitter CursorVault pour arrêter complètement l’application."] = "When enabled, Minimize hides the window. Close also sends it to the background. Use Exit CursorVault to stop the application completely.",
        ["Packs incomplets"] = "Incomplete packs", ["Comportement pour les rôles absents d’un pack."] = "Behavior for roles missing from a pack.",
        ["Rotation automatique"] = "Automatic rotation", ["La rotation horaire ou quotidienne fonctionne pendant que CursorVault est ouvert. Le mode démarrage s’exécute à chaque lancement."] = "Hourly or daily rotation works while CursorVault is running. Startup mode runs at each launch.",
        ["Bibliothèque et sécurité"] = "Library and safety", ["Les packs importés sont copiés dans %LocalAppData%\\CursorVault\\Packs. Les fichiers sont vérifiés avant chaque application."] = "Imported packs are copied to %LocalAppData%\\CursorVault\\Packs. Files are validated before each application.",
        ["Accueil"] = "Home", ["Curseurs Windows"] = "Windows cursors", ["Pack aléatoire"] = "Random pack", ["Créer un pack"] = "Create pack", ["Importer fichiers / ZIP"] = "Import files / ZIP", ["Importer un dossier"] = "Import folder",
        ["Tous"] = "All", ["Complets"] = "Complete", ["Incomplets"] = "Incomplete", ["Animés"] = "Animated", ["Statiques"] = "Static", ["Importer"] = "Import", ["Créer"] = "Create", ["Aléatoire"] = "Random",
        ["Dossier des packs"] = "Packs folder", ["Actualiser"] = "Refresh", ["Réparer"] = "Repair", ["Exporter ZIP"] = "Export ZIP", ["Restaurer"] = "Restore", ["APPLIQUER"] = "APPLY",
        ["Menu Pointeurs"] = "Pointers menu", ["Dossier Cursors"] = "Cursors folder", ["Menu Windows"] = "Windows menu", ["APPLIQUER CE MODÈLE"] = "APPLY THIS SCHEME",
        ["Sombre"] = "Dark", ["Clair"] = "Light", ["Translucide"] = "Translucent", ["Bleu"] = "Blue", ["Violet"] = "Purple", ["Vert"] = "Green", ["Cyan"] = "Cyan", ["Orange"] = "Orange", ["Rouge"] = "Red", ["Rose"] = "Pink", ["Or"] = "Gold", ["Personnalisée"] = "Custom", ["Conserver le curseur actuel"] = "Keep current cursor", ["Utiliser le curseur Windows par défaut"] = "Use default Windows cursor", ["Choisir manuellement à l’application"] = "Choose manually when applying",
        ["Activer la rotation"] = "Enable rotation", ["Démarrage"] = "Startup", ["Toutes les heures"] = "Every hour", ["Tous les jours"] = "Every day", ["Utiliser seulement les favoris"] = "Use favorites only",
        ["Ouvrir les données CursorVault"] = "Open CursorVault data", ["Menu Pointeurs Windows"] = "Windows Pointers menu", ["Restaurer la sauvegarde"] = "Restore backup", ["Quitter CursorVault"] = "Exit CursorVault", ["Afficher CursorVault"] = "Show CursorVault",
        ["Appliquer"] = "Apply", ["Ajouter / retirer des favoris"] = "Add / remove favorite", ["Renommer"] = "Rename", ["Ouvrir le dossier"] = "Open folder", ["Exporter en ZIP"] = "Export as ZIP", ["Réparer les références"] = "Repair references", ["Supprimer de CursorVault"] = "Remove from CursorVault",
        ["Rôle Windows"] = "Windows role", ["Fichier"] = "File", ["État"] = "Status", ["Chemin"] = "Path", ["Rechercher par nom, créateur ou description"] = "Search by name, creator or description", ["Favori"] = "Favorite",
        ["Données"] = "Data", ["Thème appliqué."] = "Theme applied.", ["Couleur appliquée."] = "Color applied.", ["Couleur personnalisée appliquée."] = "Custom color applied.", ["Démarrage automatique désactivé."] = "Automatic startup disabled.", ["Le fonctionnement en arrière-plan est activé."] = "Background mode enabled.", ["Le fonctionnement en arrière-plan est désactivé."] = "Background mode disabled.",
        ["Créateur de pack"] = "Pack creator", ["Le créateur original est obligatoire et restera affiché même si le pack est renommé."] = "The original creator is required and will remain visible even if the pack is renamed.",
        ["Nom du pack"] = "Pack name", ["Créateur original"] = "Original creator", ["Description"] = "Description", ["Associer les rôles Windows"] = "Assign Windows roles", ["Choisir…"] = "Choose…", ["Annuler"] = "Cancel", ["CRÉER LE PACK"] = "CREATE PACK", ["Créer un pack CursorVault"] = "Create a CursorVault pack", ["Valider"] = "OK",
        ["Renommer le pack"] = "Rename pack",
        ["Réinitialisation"] = "Reset", ["Réinitialiser les paramètres"] = "Reset settings", ["Réinitialiser CursorVault"] = "Reset CursorVault",
        ["Rétablit les paramètres d’origine de CursorVault : thème sombre, couleur bleue, police Segoe UI, langue automatique de Windows et options système par défaut. Les packs installés et les favoris sont conservés."] = "Restores CursorVault defaults: dark theme, blue accent, Segoe UI font, automatic Windows language, and default system options. Installed packs and favorites are kept.",
        ["Réinitialiser tous les paramètres de CursorVault ? Les packs installés et les favoris seront conservés."] = "Reset all CursorVault settings? Installed packs and favorites will be kept.",
        ["Les paramètres par défaut de CursorVault ont été restaurés."] = "CursorVault default settings have been restored."
    };

    private static Dictionary<string, string> BuildSpanish() => new(BuildEnglish(), StringComparer.Ordinal)
    {
        ["Gestionnaire de bibliothèque et de schémas de curseurs Windows"] = "Gestor de biblioteca y esquemas de cursores de Windows",
        ["FAVORIS"] = "FAVORITOS", ["INCOMPLETS"] = "INCOMPLETOS", ["SCHÉMA ACTIF"] = "ESQUEMA ACTIVO", ["Actions rapides"] = "Acciones rápidas",
        ["Glisser-déposer"] = "Arrastrar y soltar", ["Favoris"] = "Favoritos", ["Bibliothèque"] = "Biblioteca", ["Packs installés localement"] = "Paquetes instalados localmente", ["Sélectionnez un pack"] = "Selecciona un paquete",
        ["APERÇU"] = "VISTA PREVIA", ["Survolez ici"] = "Pasa el cursor aquí", ["Curseur sélectionné"] = "Cursor seleccionado", ["Aucun"] = "Ninguno", ["INFORMATIONS"] = "INFORMACIÓN", ["Prêt."] = "Listo.",
        ["Modèles Windows"] = "Esquemas de Windows", ["Schémas enregistrés dans Windows"] = "Esquemas registrados en Windows", ["ACTIF"] = "ACTIVO", ["Sélectionnez un modèle Windows"] = "Selecciona un esquema de Windows", ["Curseur du modèle"] = "Cursor del esquema",
        ["Paramètres"] = "Configuración", ["Apparence"] = "Apariencia", ["Choisissez le thème sombre, clair ou translucide et la couleur de CursorVault."] = "Elige el tema oscuro, claro o translúcido y el color de CursorVault.", ["Thème"] = "Tema", ["Couleur"] = "Color", ["Choisir..."] = "Elegir...", ["Police de l’interface"] = "Fuente de la interfaz", ["Police de l’interface appliquée."] = "Fuente de la interfaz aplicada.", ["Langue"] = "Idioma", ["Langue de l’interface. Le changement est appliqué immédiatement."] = "Idioma de la interfaz. El cambio se aplica inmediatamente.", ["Automatique (Windows)"] = "Automático (Windows)",
        ["Intégration Windows"] = "Integración con Windows", ["Démarrage automatique et fonctionnement en arrière-plan."] = "Inicio automático y funcionamiento en segundo plano.",
        ["Exécuter CursorVault au démarrage de Windows"] = "Ejecutar CursorVault al iniciar Windows", ["Réduire CursorVault en arrière-plan dans la zone de notification"] = "Minimizar CursorVault al área de notificación",
        ["Packs incomplets"] = "Paquetes incompletos", ["Rotation automatique"] = "Rotación automática", ["Bibliothèque et sécurité"] = "Biblioteca y seguridad", ["Accueil"] = "Inicio", ["Curseurs Windows"] = "Cursores de Windows",
        ["Pack aléatoire"] = "Paquete aleatorio", ["Créer un pack"] = "Crear paquete", ["Importer fichiers / ZIP"] = "Importar archivos / ZIP", ["Importer un dossier"] = "Importar carpeta", ["Tous"] = "Todos", ["Complets"] = "Completos", ["Incomplets"] = "Incompletos", ["Animés"] = "Animados", ["Statiques"] = "Estáticos",
        ["Importer"] = "Importar", ["Créer"] = "Crear", ["Aléatoire"] = "Aleatorio", ["Dossier des packs"] = "Carpeta de paquetes", ["Actualiser"] = "Actualizar", ["Réparer"] = "Reparar", ["Exporter ZIP"] = "Exportar ZIP", ["Restaurer"] = "Restaurar", ["APPLIQUER"] = "APLICAR", ["APPLIQUER CE MODÈLE"] = "APLICAR ESTE ESQUEMA",
        ["Sombre"] = "Oscuro", ["Clair"] = "Claro", ["Translucide"] = "Translúcido", ["Bleu"] = "Azul", ["Violet"] = "Violeta", ["Vert"] = "Verde", ["Cyan"] = "Cian", ["Orange"] = "Naranja", ["Rouge"] = "Rojo", ["Rose"] = "Rosa", ["Or"] = "Dorado", ["Personnalisée"] = "Personalizado", ["Conserver le curseur actuel"] = "Conservar el cursor actual", ["Utiliser le curseur Windows par défaut"] = "Usar el cursor predeterminado de Windows", ["Choisir manuellement à l’application"] = "Elegir manualmente al aplicar", ["Activer la rotation"] = "Activar rotación", ["Démarrage"] = "Inicio", ["Toutes les heures"] = "Cada hora", ["Tous les jours"] = "Cada día", ["Utiliser seulement les favoris"] = "Usar solo favoritos",
        ["Ouvrir les données CursorVault"] = "Abrir datos de CursorVault", ["Menu Pointeurs Windows"] = "Menú de punteros de Windows", ["Restaurer la sauvegarde"] = "Restaurar copia", ["Quitter CursorVault"] = "Salir de CursorVault", ["Afficher CursorVault"] = "Mostrar CursorVault",
        ["Appliquer"] = "Aplicar", ["Ajouter / retirer des favoris"] = "Añadir / quitar favorito", ["Renommer"] = "Renombrar", ["Ouvrir le dossier"] = "Abrir carpeta", ["Exporter en ZIP"] = "Exportar como ZIP", ["Réparer les références"] = "Reparar referencias", ["Supprimer de CursorVault"] = "Eliminar de CursorVault",
        ["Rôle Windows"] = "Rol de Windows", ["Fichier"] = "Archivo", ["État"] = "Estado", ["Chemin"] = "Ruta",
        ["Thème appliqué."] = "Tema aplicado.", ["Couleur appliquée."] = "Color aplicado.", ["Couleur personnalisée appliquée."] = "Color personalizado aplicado.",
        ["Réinitialisation"] = "Restablecimiento", ["Réinitialiser les paramètres"] = "Restablecer configuración", ["Réinitialiser CursorVault"] = "Restablecer CursorVault",
        ["Rétablit les paramètres d’origine de CursorVault : thème sombre, couleur bleue, police Segoe UI, langue automatique de Windows et options système par défaut. Les packs installés et les favoris sont conservés."] = "Restaura los valores predeterminados de CursorVault: tema oscuro, color azul, fuente Segoe UI, idioma automático de Windows y opciones del sistema predeterminadas. Se conservan los paquetes instalados y los favoritos.",
        ["Réinitialiser tous les paramètres de CursorVault ? Les packs installés et les favoris seront conservés."] = "¿Restablecer toda la configuración de CursorVault? Se conservarán los paquetes instalados y los favoritos.",
        ["Les paramètres par défaut de CursorVault ont été restaurés."] = "Se ha restaurado la configuración predeterminada de CursorVault."
    };

    private static Dictionary<string, string> BuildGerman() => new(BuildEnglish(), StringComparer.Ordinal)
    {
        ["Gestionnaire de bibliothèque et de schémas de curseurs Windows"] = "Verwaltung für Windows-Cursorbibliothek und -schemata",
        ["FAVORIS"] = "FAVORITEN", ["INCOMPLETS"] = "UNVOLLSTÄNDIG", ["SCHÉMA ACTIF"] = "AKTIVES SCHEMA", ["Actions rapides"] = "Schnellaktionen", ["Glisser-déposer"] = "Drag & Drop",
        ["Favoris"] = "Favoriten", ["Bibliothèque"] = "Bibliothek", ["Packs installés localement"] = "Lokal installierte Pakete", ["Sélectionnez un pack"] = "Paket auswählen", ["APERÇU"] = "VORSCHAU", ["Survolez ici"] = "Hier mit der Maus darüberfahren", ["Curseur sélectionné"] = "Ausgewählter Cursor", ["Aucun"] = "Keiner", ["INFORMATIONS"] = "INFORMATIONEN", ["Prêt."] = "Bereit.",
        ["Modèles Windows"] = "Windows-Schemata", ["Schémas enregistrés dans Windows"] = "In Windows registrierte Schemata", ["ACTIF"] = "AKTIV", ["Sélectionnez un modèle Windows"] = "Windows-Schema auswählen", ["Curseur du modèle"] = "Schema-Cursor",
        ["Paramètres"] = "Einstellungen", ["Apparence"] = "Darstellung", ["Choisissez le thème sombre, clair ou translucide et la couleur de CursorVault."] = "Wähle das dunkle, helle oder transparente Design und die Farbe von CursorVault.", ["Thème"] = "Design", ["Couleur"] = "Farbe", ["Choisir..."] = "Auswählen...", ["Police de l’interface"] = "Schriftart der Benutzeroberfläche", ["Police de l’interface appliquée."] = "Schriftart der Benutzeroberfläche angewendet.", ["Langue"] = "Sprache", ["Langue de l’interface. Le changement est appliqué immédiatement."] = "Sprache der Benutzeroberfläche. Änderungen werden sofort angewendet.", ["Automatique (Windows)"] = "Automatisch (Windows)",
        ["Intégration Windows"] = "Windows-Integration", ["Démarrage automatique et fonctionnement en arrière-plan."] = "Autostart und Hintergrundbetrieb.", ["Exécuter CursorVault au démarrage de Windows"] = "CursorVault beim Windows-Start ausführen", ["Réduire CursorVault en arrière-plan dans la zone de notification"] = "CursorVault in den Infobereich minimieren",
        ["Packs incomplets"] = "Unvollständige Pakete", ["Rotation automatique"] = "Automatischer Wechsel", ["Bibliothèque et sécurité"] = "Bibliothek und Sicherheit", ["Accueil"] = "Start", ["Curseurs Windows"] = "Windows-Cursor",
        ["Pack aléatoire"] = "Zufälliges Paket", ["Créer un pack"] = "Paket erstellen", ["Importer fichiers / ZIP"] = "Dateien / ZIP importieren", ["Importer un dossier"] = "Ordner importieren", ["Tous"] = "Alle", ["Complets"] = "Vollständig", ["Incomplets"] = "Unvollständig", ["Animés"] = "Animiert", ["Statiques"] = "Statisch",
        ["Importer"] = "Importieren", ["Créer"] = "Erstellen", ["Aléatoire"] = "Zufällig", ["Dossier des packs"] = "Paketordner", ["Actualiser"] = "Aktualisieren", ["Réparer"] = "Reparieren", ["Exporter ZIP"] = "ZIP exportieren", ["Restaurer"] = "Wiederherstellen", ["APPLIQUER"] = "ANWENDEN", ["APPLIQUER CE MODÈLE"] = "DIESES SCHEMA ANWENDEN",
        ["Sombre"] = "Dunkel", ["Clair"] = "Hell", ["Translucide"] = "Transparent", ["Bleu"] = "Blau", ["Violet"] = "Violett", ["Vert"] = "Grün", ["Cyan"] = "Cyan", ["Orange"] = "Orange", ["Rouge"] = "Rot", ["Rose"] = "Rosa", ["Or"] = "Gold", ["Personnalisée"] = "Benutzerdefiniert", ["Conserver le curseur actuel"] = "Aktuellen Cursor beibehalten", ["Utiliser le curseur Windows par défaut"] = "Windows-Standardcursor verwenden", ["Choisir manuellement à l’application"] = "Beim Anwenden manuell auswählen", ["Activer la rotation"] = "Wechsel aktivieren", ["Démarrage"] = "Start", ["Toutes les heures"] = "Stündlich", ["Tous les jours"] = "Täglich", ["Utiliser seulement les favoris"] = "Nur Favoriten verwenden",
        ["Ouvrir les données CursorVault"] = "CursorVault-Daten öffnen", ["Menu Pointeurs Windows"] = "Windows-Zeiger-Menü", ["Restaurer la sauvegarde"] = "Sicherung wiederherstellen", ["Quitter CursorVault"] = "CursorVault beenden", ["Afficher CursorVault"] = "CursorVault anzeigen",
        ["Appliquer"] = "Anwenden", ["Ajouter / retirer des favoris"] = "Favorit hinzufügen / entfernen", ["Renommer"] = "Umbenennen", ["Ouvrir le dossier"] = "Ordner öffnen", ["Exporter en ZIP"] = "Als ZIP exportieren", ["Réparer les références"] = "Referenzen reparieren", ["Supprimer de CursorVault"] = "Aus CursorVault entfernen",
        ["Rôle Windows"] = "Windows-Rolle", ["Fichier"] = "Datei", ["État"] = "Status", ["Chemin"] = "Pfad",
        ["Thème appliqué."] = "Design angewendet.", ["Couleur appliquée."] = "Farbe angewendet.", ["Couleur personnalisée appliquée."] = "Benutzerdefinierte Farbe angewendet.",
        ["Réinitialisation"] = "Zurücksetzen", ["Réinitialiser les paramètres"] = "Einstellungen zurücksetzen", ["Réinitialiser CursorVault"] = "CursorVault zurücksetzen",
        ["Rétablit les paramètres d’origine de CursorVault : thème sombre, couleur bleue, police Segoe UI, langue automatique de Windows et options système par défaut. Les packs installés et les favoris sont conservés."] = "Stellt die CursorVault-Standardwerte wieder her: dunkles Design, blaue Akzentfarbe, Segoe UI, automatische Windows-Sprache und Standard-Systemoptionen. Installierte Pakete und Favoriten bleiben erhalten.",
        ["Réinitialiser tous les paramètres de CursorVault ? Les packs installés et les favoris seront conservés."] = "Alle CursorVault-Einstellungen zurücksetzen? Installierte Pakete und Favoriten bleiben erhalten.",
        ["Les paramètres par défaut de CursorVault ont été restaurés."] = "Die CursorVault-Standardeinstellungen wurden wiederhergestellt."
    };

    private static Dictionary<string, string> BuildItalian() => new(BuildEnglish(), StringComparer.Ordinal)
    {
        ["Gestionnaire de bibliothèque et de schémas de curseurs Windows"] = "Gestore della libreria e degli schemi cursore di Windows",
        ["FAVORIS"] = "PREFERITI", ["INCOMPLETS"] = "INCOMPLETI", ["SCHÉMA ACTIF"] = "SCHEMA ATTIVO", ["Actions rapides"] = "Azioni rapide", ["Glisser-déposer"] = "Trascina e rilascia",
        ["Favoris"] = "Preferiti", ["Bibliothèque"] = "Libreria", ["Packs installés localement"] = "Pacchetti installati localmente", ["Sélectionnez un pack"] = "Seleziona un pacchetto", ["APERÇU"] = "ANTEPRIMA", ["Survolez ici"] = "Passa qui con il mouse", ["Curseur sélectionné"] = "Cursore selezionato", ["Aucun"] = "Nessuno", ["INFORMATIONS"] = "INFORMAZIONI", ["Prêt."] = "Pronto.",
        ["Modèles Windows"] = "Schemi Windows", ["Schémas enregistrés dans Windows"] = "Schemi registrati in Windows", ["ACTIF"] = "ATTIVO", ["Sélectionnez un modèle Windows"] = "Seleziona uno schema Windows", ["Curseur du modèle"] = "Cursore dello schema",
        ["Paramètres"] = "Impostazioni", ["Apparence"] = "Aspetto", ["Choisissez le thème sombre, clair ou translucide et la couleur de CursorVault."] = "Scegli il tema scuro, chiaro o traslucido e il colore di CursorVault.", ["Thème"] = "Tema", ["Couleur"] = "Colore", ["Choisir..."] = "Scegli...", ["Police de l’interface"] = "Carattere dell’interfaccia", ["Police de l’interface appliquée."] = "Carattere dell’interfaccia applicato.", ["Langue"] = "Lingua", ["Langue de l’interface. Le changement est appliqué immédiatement."] = "Lingua dell'interfaccia. La modifica viene applicata immediatamente.", ["Automatique (Windows)"] = "Automatico (Windows)",
        ["Intégration Windows"] = "Integrazione Windows", ["Démarrage automatique et fonctionnement en arrière-plan."] = "Avvio automatico e funzionamento in background.", ["Exécuter CursorVault au démarrage de Windows"] = "Esegui CursorVault all'avvio di Windows", ["Réduire CursorVault en arrière-plan dans la zone de notification"] = "Riduci CursorVault nell'area di notifica",
        ["Packs incomplets"] = "Pacchetti incompleti", ["Rotation automatique"] = "Rotazione automatica", ["Bibliothèque et sécurité"] = "Libreria e sicurezza", ["Accueil"] = "Home", ["Curseurs Windows"] = "Cursori Windows",
        ["Pack aléatoire"] = "Pacchetto casuale", ["Créer un pack"] = "Crea pacchetto", ["Importer fichiers / ZIP"] = "Importa file / ZIP", ["Importer un dossier"] = "Importa cartella", ["Tous"] = "Tutti", ["Complets"] = "Completi", ["Incomplets"] = "Incompleti", ["Animés"] = "Animati", ["Statiques"] = "Statici",
        ["Importer"] = "Importa", ["Créer"] = "Crea", ["Aléatoire"] = "Casuale", ["Dossier des packs"] = "Cartella pacchetti", ["Actualiser"] = "Aggiorna", ["Réparer"] = "Ripara", ["Exporter ZIP"] = "Esporta ZIP", ["Restaurer"] = "Ripristina", ["APPLIQUER"] = "APPLICA", ["APPLIQUER CE MODÈLE"] = "APPLICA QUESTO SCHEMA",
        ["Sombre"] = "Scuro", ["Clair"] = "Chiaro", ["Translucide"] = "Traslucido", ["Bleu"] = "Blu", ["Violet"] = "Viola", ["Vert"] = "Verde", ["Cyan"] = "Ciano", ["Orange"] = "Arancione", ["Rouge"] = "Rosso", ["Rose"] = "Rosa", ["Or"] = "Oro", ["Personnalisée"] = "Personalizzato", ["Conserver le curseur actuel"] = "Mantieni il cursore attuale", ["Utiliser le curseur Windows par défaut"] = "Usa il cursore predefinito di Windows", ["Choisir manuellement à l’application"] = "Scegli manualmente durante l'applicazione", ["Activer la rotation"] = "Attiva rotazione", ["Démarrage"] = "Avvio", ["Toutes les heures"] = "Ogni ora", ["Tous les jours"] = "Ogni giorno", ["Utiliser seulement les favoris"] = "Usa solo i preferiti",
        ["Ouvrir les données CursorVault"] = "Apri dati CursorVault", ["Menu Pointeurs Windows"] = "Menu puntatori Windows", ["Restaurer la sauvegarde"] = "Ripristina backup", ["Quitter CursorVault"] = "Esci da CursorVault", ["Afficher CursorVault"] = "Mostra CursorVault",
        ["Appliquer"] = "Applica", ["Ajouter / retirer des favoris"] = "Aggiungi / rimuovi preferito", ["Renommer"] = "Rinomina", ["Ouvrir le dossier"] = "Apri cartella", ["Exporter en ZIP"] = "Esporta come ZIP", ["Réparer les références"] = "Ripara riferimenti", ["Supprimer de CursorVault"] = "Rimuovi da CursorVault",
        ["Rôle Windows"] = "Ruolo Windows", ["Fichier"] = "File", ["État"] = "Stato", ["Chemin"] = "Percorso",
        ["Thème appliqué."] = "Tema applicato.", ["Couleur appliquée."] = "Colore applicato.", ["Couleur personnalisée appliquée."] = "Colore personalizzato applicato.",
        ["Réinitialisation"] = "Ripristino", ["Réinitialiser les paramètres"] = "Ripristina impostazioni", ["Réinitialiser CursorVault"] = "Ripristina CursorVault",
        ["Rétablit les paramètres d’origine de CursorVault : thème sombre, couleur bleue, police Segoe UI, langue automatique de Windows et options système par défaut. Les packs installés et les favoris sont conservés."] = "Ripristina i valori predefiniti di CursorVault: tema scuro, colore blu, font Segoe UI, lingua automatica di Windows e opzioni di sistema predefinite. I pacchetti installati e i preferiti vengono conservati.",
        ["Réinitialiser tous les paramètres de CursorVault ? Les packs installés et les favoris seront conservés."] = "Ripristinare tutte le impostazioni di CursorVault? I pacchetti installati e i preferiti verranno conservati.",
        ["Les paramètres par défaut de CursorVault ont été restaurés."] = "Le impostazioni predefinite di CursorVault sono state ripristinate."
    };
}
