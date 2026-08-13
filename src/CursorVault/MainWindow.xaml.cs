using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using CursorVault.Models;
using CursorVault.Services;
using CursorVault.Windows;
using Microsoft.Win32;

namespace CursorVault;

public partial class MainWindow : Window
{
    private readonly PackService _packService = new();
    private readonly CursorSystemService _cursorService = new();
    private readonly SettingsService _settingsService = new();
    private readonly BackupService _backupService = new();
    private readonly UpdateService _updateService = new();
    private readonly DispatcherTimer _rotationTimer = new();
    private readonly DispatcherTimer _windowsThemeTimer = new();
    private readonly Random _random = new();

    private AppSettings _settings = new();
    private List<CursorPack> _allPacks = new();
    private List<WindowsCursorScheme> _allWindowsSchemes = new();
    private bool _initializingSettings = true;
    private bool _startupRotationHandled;
    private TrayIconService? _trayIcon;
    private bool _forceClose;
    private bool _hidingToTray;
    private bool? _lastWindowsLightTheme;
    private bool _changingVariantSelection;

    // La barre de titre native suit la couleur d’accent choisie dans les paramètres.
    private const int DwmwaBorderColor = 34;
    private const int DwmwaCaptionColor = 35;
    private const int DwmwaTextColor = 36;
    private const int DwmwaSystemBackdropType = 38;
    private const uint DwmSystemBackdropNone = 1;
    private const uint DwmSystemBackdropTransientWindow = 3; // Acrylic sur Windows 11.

    [StructLayout(LayoutKind.Sequential)]
    private struct Margins
    {
        public int Left;
        public int Right;
        public int Top;
        public int Bottom;
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(
        IntPtr hwnd,
        int dwAttribute,
        ref uint pvAttribute,
        int cbAttribute);

    [DllImport("dwmapi.dll")]
    private static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd, ref Margins margins);

    public MainWindow()
    {
        InitializeComponent();
        SourceInitialized += MainWindow_SourceInitialized;
        Loaded += MainWindow_Loaded;
        StateChanged += MainWindow_StateChanged;
        Closing += MainWindow_Closing;
        Closed += MainWindow_Closed;
        PackList.PreviewMouseRightButtonDown += PackList_PreviewMouseRightButtonDown;
        _rotationTimer.Tick += RotationTimer_Tick;
        _windowsThemeTimer.Interval = TimeSpan.FromSeconds(4);
        _windowsThemeTimer.Tick += WindowsThemeTimer_Tick;
    }

    private void MainWindow_SourceInitialized(object? sender, EventArgs e)
    {
        ApplyNativeTitleBarColors();
        ApplyBackdropForTheme();
    }

    private void ApplyNativeTitleBarColors()
    {
        var accent = ParseAccentColor(_settings.AccentColor);
        var titleText = GetContrastTextColor(accent);
        uint captionColor = ToColorRef(accent.R, accent.G, accent.B);
        uint textColor = ToColorRef(titleText.R, titleText.G, titleText.B);
        try
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd == IntPtr.Zero) return;
            _ = DwmSetWindowAttribute(hwnd, DwmwaCaptionColor, ref captionColor, sizeof(uint));
            _ = DwmSetWindowAttribute(hwnd, DwmwaBorderColor, ref captionColor, sizeof(uint));
            _ = DwmSetWindowAttribute(hwnd, DwmwaTextColor, ref textColor, sizeof(uint));
        }
        catch (DllNotFoundException) { }
        catch (EntryPointNotFoundException) { }
    }

    private static uint ToColorRef(byte r, byte g, byte b) => (uint)(r | (g << 8) | (b << 16));

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            AppPaths.Ensure();
            _settings = _settingsService.Load();
            _settings.Language = _settings.UseSystemLanguage
                ? LocalizationService.DetectSystemLanguage()
                : LocalizationService.NormalizeLanguage(_settings.Language);
            LocalizationService.SetLanguage(_settings.Language);
            _settingsService.Save(_settings);
            ApplyTheme(_settings.Theme);
            ApplyApplicationFont(_settings.FontFamily);
            ApplyInterfaceSize(_settings.InterfaceSize);
            InitializeSettingsControls();
            InitializeSystemIntegration();
            ApplyLanguageToUi();
            _packService.EnsureStarterPacks();
            _cursorService.EnsureBackup();
            ReloadPacks();
            ReloadWindowsSchemes();
            RefreshHome();
            ConfigureRotationTimer();
            ConfigureWindowsThemeMonitor(applyNow: true);
            RefreshStorageUsage();
            RefreshDiagnosticPage();

            StatusText.Text = LocalizationService.Translate("Prêt.", _settings.Language) + " " +
                              LocalizationService.Translate("Les imports sont installés automatiquement dans la bibliothèque locale de CursorVault.", _settings.Language);
            SettingsStatusText.Text = $"{LocalizationService.Translate("Données", _settings.Language)} : {AppPaths.DataRoot}";

            if (_settings.RotationEnabled && _settings.RotationMode == "Démarrage" && !_startupRotationHandled)
            {
                _startupRotationHandled = true;
                ApplyRandomPack(automatic: true);
            }

            if (_settings.MinimizeToTray && Environment.GetCommandLineArgs().Any(a => a.Equals("--startup", StringComparison.OrdinalIgnoreCase)))
                Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(HideToTray));
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    private void InitializeSettingsControls()
    {
        _initializingSettings = true;
        ThemeComboBox.SelectedIndex = _settings.Theme.Equals("Light", StringComparison.OrdinalIgnoreCase)
            ? 1
            : _settings.Theme.Equals("Translucent", StringComparison.OrdinalIgnoreCase) ? 2 : 0;
        AccentColorComboBox.SelectedIndex = AccentColorIndexFromValue(_settings.AccentColor);
        InitializeFontChoices();
        LanguageComboBox.SelectedIndex = _settings.UseSystemLanguage ? 0 : LanguageIndexFromCode(_settings.Language);
        StartWithWindowsCheckBox.IsChecked = _settings.StartWithWindows;
        MinimizeToTrayCheckBox.IsChecked = _settings.MinimizeToTray;
        MissingRoleBehaviorComboBox.SelectedIndex = _settings.MissingRoleBehavior switch
        {
            MissingRoleBehavior.WindowsDefault => 1,
            MissingRoleBehavior.ChooseManually => 2,
            _ => 0
        };
        RotationEnabledCheckBox.IsChecked = _settings.RotationEnabled;
        RotationModeComboBox.SelectedIndex = _settings.RotationMode switch
        {
            "Toutes les heures" => 1,
            "Tous les jours" => 2,
            _ => 0
        };
        RotationFavoritesOnlyCheckBox.IsChecked = _settings.RotationFavoritesOnly;
        PackFilterComboBox.SelectedIndex = FilterIndexFromName(_settings.PackFilter);
        LibrarySortComboBox.SelectedIndex = SortIndexFromName(_settings.LibrarySort);
        SettingsSortComboBox.SelectedIndex = SortIndexFromName(_settings.LibrarySort);
        InterfaceSizeComboBox.SelectedIndex = InterfaceSizeIndexFromName(_settings.InterfaceSize);
        FollowWindowsThemeCheckBox.IsChecked = _settings.FollowWindowsTheme;
        PortableModeCheckBox.IsChecked = AppPaths.IsPortable;
        CurrentVersionText.Text = LF("Version actuelle : {0}", typeof(MainWindow).Assembly.GetName().Version);
        _initializingSettings = false;
    }

    private void ReloadPacks(string? selectId = null)
    {
        _allPacks = _packService.LoadPacks();
        foreach (var pack in _allPacks)
            pack.IsFavorite = _settings.FavoritePackIds.Contains(pack.Id);

        RefreshThemePackChoices();
        ApplyFilter(selectId);
        LibraryCountText.Text = LF("{0} pack(s) installé(s) localement", _allPacks.Count);
        RefreshHome();

        if (_allPacks.Count == 0)
        {
            PackNameText.Text = L("Aucun pack");
            PackMetaText.Text = L("Importez un dossier, un ZIP ou des fichiers .cur / .ani.");
            CursorGrid.ItemsSource = null;
        }
    }

    private void ApplyFilter(string? selectId = null)
    {
        var query = (SearchBox.Text ?? "").Trim();
        var filter = PackFilterNameFromIndex(PackFilterComboBox.SelectedIndex);
        IEnumerable<CursorPack> filtered = _allPacks;

        if (!string.IsNullOrWhiteSpace(query))
        {
            filtered = filtered.Where(p =>
                p.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
                p.CreatorDisplay.Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
                p.Author.Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
                p.Description.Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
                p.Cursors.Keys.Any(r => r.Contains(query, StringComparison.CurrentCultureIgnoreCase)));
        }

        filtered = filter switch
        {
            "Complets" => filtered.Where(p => p.IsComplete),
            "Incomplets" => filtered.Where(p => !p.IsComplete),
            "Animés" => filtered.Where(p => p.HasAnimated),
            "Statiques" => filtered.Where(p => p.HasStatic),
            "Favoris" => filtered.Where(p => p.IsFavorite),
            _ => filtered
        };

        var list = _settings.LibrarySort switch
        {
            "Nom A-Z" => filtered.OrderBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase).ToList(),
            "Nom Z-A" => filtered.OrderByDescending(p => p.Name, StringComparer.CurrentCultureIgnoreCase).ToList(),
            "Créateur" => filtered.OrderBy(p => p.CreatorDisplay, StringComparer.CurrentCultureIgnoreCase).ThenBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase).ToList(),
            "Complets d'abord" => filtered.OrderByDescending(p => p.IsComplete).ThenBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase).ToList(),
            "Plus récemment ajoutés" => filtered.OrderByDescending(p => p.AddedAt).ThenBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase).ToList(),
            _ => filtered.OrderByDescending(p => p.IsFavorite).ThenBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase).ToList()
        };
        var previous = selectId ?? (PackList.SelectedItem as CursorPack)?.Id;
        PackList.ItemsSource = list;
        PackList.SelectedItem = list.FirstOrDefault(p => p.Id.Equals(previous, StringComparison.OrdinalIgnoreCase)) ?? list.FirstOrDefault();
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!IsLoaded) return;
        ApplyFilter();
    }

    private void PackFilterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_initializingSettings || !IsLoaded) return;
        _settings.PackFilter = PackFilterNameFromIndex(PackFilterComboBox.SelectedIndex);
        _settingsService.Save(_settings);
        ApplyFilter();
    }

    private void LibrarySortComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_initializingSettings || !IsLoaded) return;
        _settings.LibrarySort = SortNameFromIndex(LibrarySortComboBox.SelectedIndex);
        _settingsService.Save(_settings);
        _initializingSettings = true;
        SettingsSortComboBox.SelectedIndex = LibrarySortComboBox.SelectedIndex;
        _initializingSettings = false;
        ApplyFilter();
    }

    private void PackList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (PackList.SelectedItem is not CursorPack pack)
        {
            CursorGrid.ItemsSource = null;
            return;
        }

        PackNameText.Text = pack.Name;
        PackMetaText.Text = LF("Créateur original : {0}", LM(pack.CreatorDisplay)) + (string.IsNullOrWhiteSpace(pack.Description) ? "" : $"  •  {LM(pack.Description)}");
        PackInfoCreatorText.Text = LM(pack.CreatorDisplay);
        PackInfoRolesText.Text = LF("{0} / {1} rôles", pack.CursorCount, CursorSystemService.KnownRoles.Length) + " • " + L(pack.IsComplete ? "Complet" : "Incomplet");
        PackInfoFormatText.Text = LF("Format : {0}", pack.FormatText);
        var linkedVariants = string.IsNullOrWhiteSpace(pack.VariantGroup)
            ? new List<CursorPack>()
            : _allPacks.Where(p => p.Id != pack.Id && p.VariantGroup.Equals(pack.VariantGroup, StringComparison.CurrentCultureIgnoreCase)).ToList();
        PackInfoVariantText.Text = string.IsNullOrWhiteSpace(pack.VariantName)
            ? ""
            : LF("Variante : {0}", pack.VariantName) + (linkedVariants.Count > 0 ? " • " + LF("{0} liée(s)", linkedVariants.Count) : "");
        var variantChoices = string.IsNullOrWhiteSpace(pack.VariantGroup)
            ? new List<CursorPack>()
            : _allPacks.Where(p => p.VariantGroup.Equals(pack.VariantGroup, StringComparison.CurrentCultureIgnoreCase)).OrderBy(p => p.VariantName).ToList();
        _changingVariantSelection = true;
        PackVariantComboBox.ItemsSource = variantChoices;
        PackVariantComboBox.SelectedItem = variantChoices.FirstOrDefault(p => p.Id.Equals(pack.Id, StringComparison.OrdinalIgnoreCase));
        PackVariantComboBox.Visibility = variantChoices.Count > 1 ? Visibility.Visible : Visibility.Collapsed;
        PackVariantSelectorLabel.Visibility = variantChoices.Count > 1 ? Visibility.Visible : Visibility.Collapsed;
        _changingVariantSelection = false;
        PackInfoSizeText.Text = LF("Taille : {0} • Ajouté : {1}", FormatBytes(pack.SizeBytes), pack.AddedAt.ToString("d", System.Globalization.CultureInfo.CurrentCulture));

        var rows = CursorSystemService.KnownRoles.Select(role =>
        {
            if (!pack.Cursors.TryGetValue(role, out var relative) || string.IsNullOrWhiteSpace(relative))
            {
                return new CursorRoleRow
                {
                    Role = role,
                    DisplayRole = LocalizationService.TranslateRole(role, _settings.Language),
                    File = L("(non fourni)"),
                    FullPath = "",
                    Status = L("Manquant")
                };
            }

            var full = SafeCombine(pack.FolderPath, relative);
            return new CursorRoleRow
            {
                Role = role,
                DisplayRole = LocalizationService.TranslateRole(role, _settings.Language),
                File = relative,
                FullPath = full,
                Status = !IsCursorExtension(full) ? L("Invalide") : !File.Exists(full) ? L("Absent") : PackService.IsCursorBinaryValid(full) ? L("Disponible") : L("Invalide")
            };
        }).ToList();

        CursorGrid.ItemsSource = rows;
        CursorGrid.SelectedIndex = rows.Count > 0 ? 0 : -1;
        var validation = _packService.ValidatePack(pack);
        StatusText.Text = validation.IsValid
            ? validation.IsComplete
                ? L("Pack complet et prêt à être appliqué.")
                : LF("Pack incomplet : {0} rôle(s) manquant(s). Le comportement défini dans Paramètres sera utilisé.", validation.MissingRoles.Count)
            : LF("Pack à réparer : {0} fichier(s) absent(s), {1} référence(s) invalide(s).", validation.MissingFiles.Count, validation.InvalidFiles.Count);
    }

    private void PackVariantComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_changingVariantSelection || PackVariantComboBox.SelectedItem is not CursorPack pack) return;
        var match = PackList.Items.Cast<object>().OfType<CursorPack>().FirstOrDefault(p => p.Id.Equals(pack.Id, StringComparison.OrdinalIgnoreCase));
        if (match is not null)
        {
            PackList.SelectedItem = match;
            PackList.ScrollIntoView(match);
        }
        else
        {
            SearchBox.Text = "";
            PackFilterComboBox.SelectedIndex = 0;
            ApplyFilter(pack.Id);
        }
    }

    private void CursorGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CursorGrid.SelectedItem is not CursorRoleRow row) return;
        PreviewRoleText.Text = string.IsNullOrWhiteSpace(row.DisplayRole) ? row.Role : row.DisplayRole;
        PreviewFileText.Text = row.File;

        if (string.IsNullOrWhiteSpace(row.FullPath) || !File.Exists(row.FullPath))
        {
            PreviewHost.Cursor = System.Windows.Input.Cursors.Arrow;
            return;
        }

        try
        {
            PreviewHost.Cursor = new System.Windows.Input.Cursor(row.FullPath);
        }
        catch
        {
            PreviewHost.Cursor = System.Windows.Input.Cursors.Arrow;
        }
    }

    private void ApplyButton_Click(object sender, RoutedEventArgs e) => ApplySelectedPack();
    private void ContextApplyPack_Click(object sender, RoutedEventArgs e) => ApplySelectedPack();

    private void ApplySelectedPack()
    {
        if (PackList.SelectedItem is not CursorPack pack)
        {
            StatusText.Text = L("Sélectionnez d'abord un pack.");
            return;
        }

        try
        {
            ApplyPack(pack, automatic: false);
            StatusText.Text = LF("Pack « {0} » appliqué.", pack.Name);
        }
        catch (OperationCanceledException)
        {
            StatusText.Text = L("Application annulée.");
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    private void ApplyPack(CursorPack pack, bool automatic)
    {
        var validation = _packService.ValidatePack(pack);
        if (!validation.IsValid)
            throw new InvalidOperationException(LF("Le pack contient des références cassées. Utilisez Réparer avant de l’appliquer. Fichiers absents : {0}; références invalides : {1}.", validation.MissingFiles.Count, validation.InvalidFiles.Count));

        IReadOnlyDictionary<string, string>? manualMappings = null;
        var behavior = _settings.MissingRoleBehavior;

        if (validation.MissingRoles.Count > 0 && behavior == MissingRoleBehavior.ChooseManually)
        {
            if (automatic)
                throw new InvalidOperationException(L("La rotation automatique ignore les packs incomplets lorsque le mode manuel est sélectionné."));
            manualMappings = CollectManualMappings(validation.MissingRoles);
        }

        _cursorService.ApplyPack(pack, behavior, manualMappings);
        ReloadWindowsSchemes();
        RefreshHome();
    }

    private IReadOnlyDictionary<string, string> CollectManualMappings(IEnumerable<string> roles)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var role in roles)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = LF("Choisir un curseur pour le rôle manquant : {0}", LocalizationService.TranslateRole(role, _settings.Language)),
                Filter = L("Curseurs Windows (*.cur;*.ani)|*.cur;*.ani")
            };
            if (dialog.ShowDialog(this) != true)
                throw new OperationCanceledException();
            result[role] = dialog.FileName;
        }
        return result;
    }

    private void RestoreButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _cursorService.RestoreBackup();
            ReloadWindowsSchemes();
            RefreshHome();
            StatusText.Text = L("Configuration présente avant le premier lancement de CursorVault restaurée.");
            SettingsStatusText.Text = StatusText.Text;
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    private void FavoriteButton_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not CursorPack pack) return;
        ToggleFavorite(pack);
        e.Handled = true;
    }

    private void ContextFavoritePack_Click(object sender, RoutedEventArgs e)
    {
        if (PackList.SelectedItem is CursorPack pack)
            ToggleFavorite(pack);
    }

    private void ToggleFavorite(CursorPack pack)
    {
        if (_settings.FavoritePackIds.Contains(pack.Id))
            _settings.FavoritePackIds.Remove(pack.Id);
        else
            _settings.FavoritePackIds.Add(pack.Id);
        _settingsService.Save(_settings);
        ReloadPacks(pack.Id);
        StatusText.Text = _settings.FavoritePackIds.Contains(pack.Id)
            ? LF("« {0} » ajouté aux favoris.", pack.Name)
            : LF("« {0} » retiré des favoris.", pack.Name);
    }

    private void ImportFilesButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = L("Importer des curseurs ou des archives"),
            Filter = "CursorVault (*.zip;*.cur;*.ani)|*.zip;*.cur;*.ani|Archives ZIP (*.zip)|*.zip|Curseurs (*.cur;*.ani)|*.cur;*.ani",
            Multiselect = true
        };
        if (dialog.ShowDialog(this) != true) return;
        ImportPaths(dialog.FileNames);
    }

    private void ImportFolderButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = L("Choisissez un dossier contenant des curseurs"),
            Multiselect = false
        };
        if (dialog.ShowDialog(this) != true) return;
        ImportPaths(new[] { dialog.FolderName });
    }

    private void ImportPaths(IEnumerable<string> paths)
    {
        try
        {
            var existing = _allPacks
                .GroupBy(p => _packService.ComputePackFingerprint(p), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
            var imported = _packService.ImportDroppedFiles(paths).ToList();
            var kept = new List<CursorPack>();

            foreach (var pack in imported)
            {
                var fingerprint = _packService.ComputePackFingerprint(pack);
                if (existing.TryGetValue(fingerprint, out var duplicate) && !duplicate.Id.Equals(pack.Id, StringComparison.OrdinalIgnoreCase))
                {
                    var answer = System.Windows.MessageBox.Show(this,
                        $"Le pack « {pack.Name} » contient les mêmes curseurs que « {duplicate.Name} ».\n\nOui : remplacer l'ancien pack\nNon : conserver les deux\nAnnuler : annuler ce nouvel import",
                        "CursorVault - Doublon", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
                    if (answer == MessageBoxResult.Yes)
                    {
                        var wasFavorite = _settings.FavoritePackIds.Remove(duplicate.Id);
                        if (wasFavorite) _settings.FavoritePackIds.Add(pack.Id);
                        if (_settings.LightThemePackId.Equals(duplicate.Id, StringComparison.OrdinalIgnoreCase)) _settings.LightThemePackId = pack.Id;
                        if (_settings.DarkThemePackId.Equals(duplicate.Id, StringComparison.OrdinalIgnoreCase)) _settings.DarkThemePackId = pack.Id;
                        _packService.DeletePack(duplicate);
                        kept.Add(pack);
                    }
                    else if (answer == MessageBoxResult.No)
                    {
                        kept.Add(pack);
                    }
                    else
                    {
                        _packService.DeletePack(pack);
                    }
                }
                else
                {
                    kept.Add(pack);
                    existing[fingerprint] = pack;
                }
            }

            _settingsService.Save(_settings);
            var last = kept.LastOrDefault();
            ReloadPacks(last?.Id);
            StatusText.Text = kept.Count == 0
                ? L("Aucun pack importé.")
                : LF("{0} pack(s) importé(s) et installé(s) dans CursorVault.", kept.Count);
            HomeStatusText.Text = StatusText.Text;
            RefreshStorageUsage();
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    private void Window_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop) ? System.Windows.DragDropEffects.Copy : System.Windows.DragDropEffects.None;
        e.Handled = true;
    }

    private void Window_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(System.Windows.DataFormats.FileDrop) is string[] paths)
            ImportPaths(paths);
    }

    private void CreatePackButton_Click(object sender, RoutedEventArgs e)
    {
        var window = new PackCreatorWindow(_packService) { Owner = this };
        LocalizationService.Apply(window, _settings.Language);
        if (window.ShowDialog() == true && window.CreatedPack is not null)
        {
            ReloadPacks(window.CreatedPack.Id);
            NavigateTo(1);
            StatusText.Text = LF("Pack « {0} » créé. Créateur original : {1}.", window.CreatedPack.Name, LM(window.CreatedPack.CreatorDisplay));
        }
    }

    private void ContextRenamePack_Click(object sender, RoutedEventArgs e)
    {
        if (PackList.SelectedItem is not CursorPack pack) return;
        var dialog = new TextPromptWindow(
            L("Renommer le pack"),
            LF("Nouveau nom. Le créateur original « {0} » restera affiché :", LM(pack.CreatorDisplay)),
            pack.Name) { Owner = this };
        LocalizationService.Apply(dialog, _settings.Language);
        if (dialog.ShowDialog() != true) return;

        try
        {
            _packService.RenamePack(pack, dialog.Value);
            ReloadPacks(pack.Id);
            StatusText.Text = LF("Pack renommé en « {0} ». Créateur original conservé : {1}.", dialog.Value, LM(pack.CreatorDisplay));
        }
        catch (Exception ex) { ShowError(ex); }
    }

    private void ContextOpenPackFolder_Click(object sender, RoutedEventArgs e)
    {
        if (PackList.SelectedItem is CursorPack pack && Directory.Exists(pack.FolderPath))
            Process.Start(new ProcessStartInfo(pack.FolderPath) { UseShellExecute = true });
    }

    private void ExportButton_Click(object sender, RoutedEventArgs e) => ExportSelectedPack();
    private void ContextExportPack_Click(object sender, RoutedEventArgs e) => ExportSelectedPack();

    private void ExportSelectedPack()
    {
        if (PackList.SelectedItem is not CursorPack pack) return;
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = L("Exporter le pack CursorVault"),
            Filter = "Archive ZIP (*.zip)|*.zip",
            FileName = MakeSafeFileName(pack.Name) + ".zip"
        };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            _packService.ExportPack(pack, dialog.FileName);
            StatusText.Text = LF("Pack exporté : {0}", dialog.FileName);
        }
        catch (Exception ex) { ShowError(ex); }
    }

    private void ExportInfButton_Click(object sender, RoutedEventArgs e) => ExportSelectedPackInf();
    private void ContextExportInfPack_Click(object sender, RoutedEventArgs e) => ExportSelectedPackInf();

    private void ExportSelectedPackInf()
    {
        if (PackList.SelectedItem is not CursorPack pack) return;
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Exporter install.inf",
            Filter = "Fichier INF (*.inf)|*.inf",
            FileName = MakeSafeFileName(pack.Name) + "-install.inf"
        };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            _packService.GenerateInstallInf(pack, dialog.FileName);
            StatusText.Text = $"install.inf exporté : {dialog.FileName}";
        }
        catch (Exception ex) { ShowError(ex); }
    }

    private void AnalyzePackButton_Click(object sender, RoutedEventArgs e)
    {
        if (PackList.SelectedItem is not CursorPack pack) return;
        try
        {
            var a = _packService.AnalyzePack(pack);
            var message = $"{pack.Name}\n\nRôles : {a.RoleCount}/17\nCUR : {a.CurCount}\nANI : {a.AniCount}\nFichiers absents : {a.MissingFiles}\nFichiers invalides : {a.InvalidFiles}\nFichiers dupliqués : {a.DuplicateFiles}\nTaille des curseurs : {FormatBytes(a.SizeBytes)}";
            System.Windows.MessageBox.Show(this, message, "Analyse du pack", MessageBoxButton.OK, a.InvalidFiles == 0 && a.MissingFiles == 0 ? MessageBoxImage.Information : MessageBoxImage.Warning);
        }
        catch (Exception ex) { ShowError(ex); }
    }

    private void RepairButton_Click(object sender, RoutedEventArgs e) => RepairSelectedPack();
    private void ContextRepairPack_Click(object sender, RoutedEventArgs e) => RepairSelectedPack();

    private void RepairSelectedPack()
    {
        if (PackList.SelectedItem is not CursorPack pack) return;
        try
        {
            var before = _packService.ValidatePack(pack);
            var after = _packService.RepairPack(pack);
            ReloadPacks(pack.Id);
            StatusText.Text = LF("Réparation terminée : {0} référence(s) cassée(s) retirée(s). Le pack contient maintenant {1}/{2} rôles.", before.MissingFiles.Count + before.InvalidFiles.Count, CursorSystemService.KnownRoles.Length - after.MissingRoles.Count, CursorSystemService.KnownRoles.Length);
        }
        catch (Exception ex) { ShowError(ex); }
    }

    private void ContextDeletePack_Click(object sender, RoutedEventArgs e)
    {
        if (PackList.SelectedItem is not CursorPack pack) return;
        var answer = System.Windows.MessageBox.Show(
            this,
            LF("Supprimer « {0} » de CursorVault ?\n\nCréateur original : {1}\nLes fichiers du pack seront supprimés de la bibliothèque locale.", pack.Name, LM(pack.CreatorDisplay)),
            "CursorVault",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (answer != MessageBoxResult.Yes) return;
        try
        {
            _settings.FavoritePackIds.Remove(pack.Id);
            if (_settings.LightThemePackId.Equals(pack.Id, StringComparison.OrdinalIgnoreCase)) _settings.LightThemePackId = "";
            if (_settings.DarkThemePackId.Equals(pack.Id, StringComparison.OrdinalIgnoreCase)) _settings.DarkThemePackId = "";
            _settingsService.Save(_settings);
            _packService.DeletePack(pack);
            ReloadPacks();
            StatusText.Text = LF("Pack « {0} » supprimé de CursorVault.", pack.Name);
        }
        catch (Exception ex) { ShowError(ex); }
    }

    private void PackList_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        var source = e.OriginalSource as DependencyObject;
        while (source is not null && source is not ListBoxItem)
            source = VisualTreeHelper.GetParent(source);
        if (source is ListBoxItem item)
            item.IsSelected = true;
    }

    private void RandomPackButton_Click(object sender, RoutedEventArgs e) => ApplyRandomPack(automatic: false);

    private void ApplyRandomPack(bool automatic)
    {
        try
        {
            IEnumerable<CursorPack> candidates = _allPacks;
            if (automatic && _settings.RotationFavoritesOnly)
                candidates = candidates.Where(p => p.IsFavorite);

            var valid = candidates
                .Where(p => _packService.ValidatePack(p).IsValid)
                .Where(p => !automatic || _settings.MissingRoleBehavior != MissingRoleBehavior.ChooseManually || p.IsComplete)
                .ToList();

            if (valid.Count == 0)
                throw new InvalidOperationException(automatic && _settings.RotationFavoritesOnly
                    ? L("Aucun favori valide n’est disponible pour la rotation automatique.")
                    : L("Aucun pack valide n’est disponible."));

            var pack = valid[_random.Next(valid.Count)];
            ApplyPack(pack, automatic);
            ReloadPacks(pack.Id);
            StatusText.Text = automatic ? LF("Rotation automatique : « {0} ».", pack.Name) : LF("Pack aléatoire : « {0} ».", pack.Name);
            HomeStatusText.Text = StatusText.Text;
        }
        catch (Exception ex)
        {
            if (automatic)
            {
                HomeStatusText.Text = L("Rotation automatique") + " : " + LocalizeErrorMessage(ex.Message);
                SettingsStatusText.Text = HomeStatusText.Text;
            }
            else
            {
                ShowError(ex);
            }
        }
    }

    private void RotationTimer_Tick(object? sender, EventArgs e) => ApplyRandomPack(automatic: true);

    private void ConfigureRotationTimer()
    {
        _rotationTimer.Stop();
        if (!_settings.RotationEnabled) return;
        if (_settings.RotationMode == "Toutes les heures")
            _rotationTimer.Interval = TimeSpan.FromHours(1);
        else if (_settings.RotationMode == "Tous les jours")
            _rotationTimer.Interval = TimeSpan.FromDays(1);
        else
            return;
        _rotationTimer.Start();
    }

    private void OpenPacksButton_Click(object sender, RoutedEventArgs e)
    {
        AppPaths.Ensure();
        Process.Start(new ProcessStartInfo(AppPaths.PacksRoot) { UseShellExecute = true });
    }

    private void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        var selected = (PackList.SelectedItem as CursorPack)?.Id;
        ReloadPacks(selected);
        StatusText.Text = L("Bibliothèque actualisée.");
    }

    private void OpenWindowsPointerMenu_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "control.exe",
                Arguments = "main.cpl,,1",
                UseShellExecute = true
            });
        }
        catch (Exception ex) { ShowError(ex); }
    }

    private void ReloadWindowsSchemes(string? selectName = null)
    {
        try
        {
            _allWindowsSchemes = _cursorService.GetInstalledSchemes();
            ApplyWindowsSchemeFilter(selectName);
            WindowsSchemeCountText.Text = LF("{0} modèle(s) enregistré(s) dans Windows", _allWindowsSchemes.Count);
            if (_allWindowsSchemes.Count == 0)
            {
                WindowsSchemeNameText.Text = L("Aucun modèle Windows détecté");
                WindowsSchemeMetaText.Text = L("Ouvrez le menu Pointeurs Windows pour vérifier les schémas installés.");
                WindowsSchemeCursorGrid.ItemsSource = null;
                WindowsStatusText.Text = L("Aucun schéma n'a été trouvé dans le Registre Windows.");
            }
            RefreshHome();
        }
        catch (Exception ex)
        {
            WindowsStatusText.Text = LocalizeErrorMessage(ex.Message);
        }
    }

    private void ApplyWindowsSchemeFilter(string? selectName = null)
    {
        var query = (WindowsSchemeSearchBox.Text ?? "").Trim();
        var filtered = string.IsNullOrWhiteSpace(query)
            ? _allWindowsSchemes
            : _allWindowsSchemes.Where(s =>
                s.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
                s.Source.Contains(query, StringComparison.CurrentCultureIgnoreCase)).ToList();

        WindowsSchemeList.ItemsSource = filtered;
        WindowsCursorScheme? target = null;
        if (!string.IsNullOrWhiteSpace(selectName))
            target = filtered.FirstOrDefault(s => s.Name.Equals(selectName, StringComparison.CurrentCultureIgnoreCase));
        target ??= filtered.FirstOrDefault(s => s.IsActive);
        target ??= filtered.FirstOrDefault();
        WindowsSchemeList.SelectedItem = target;
    }

    private void WindowsSchemeSearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!IsLoaded) return;
        var selected = (WindowsSchemeList.SelectedItem as WindowsCursorScheme)?.Name;
        ApplyWindowsSchemeFilter(selected);
    }

    private void WindowsSchemeList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (WindowsSchemeList.SelectedItem is not WindowsCursorScheme scheme)
        {
            WindowsSchemeCursorGrid.ItemsSource = null;
            return;
        }

        WindowsSchemeNameText.Text = scheme.Name;
        WindowsSchemeMetaText.Text = scheme.IsActive
            ? $"{LM(scheme.Source)}  •  {L("modèle actuellement actif")}  •  {LF("{0} rôles configurés", scheme.CursorCount)}"
            : $"{LM(scheme.Source)}  •  {LF("{0} rôles configurés", scheme.CursorCount)}";

        var rows = CursorSystemService.KnownRoles.Select(role =>
        {
            scheme.Cursors.TryGetValue(role, out var rawPath);
            var fullPath = rawPath ?? "";
            return new CursorRoleRow
            {
                Role = role,
                DisplayRole = LocalizationService.TranslateRole(role, _settings.Language),
                File = string.IsNullOrWhiteSpace(fullPath) ? L("(Windows par défaut)") : Path.GetFileName(fullPath),
                FullPath = fullPath,
                Status = string.IsNullOrWhiteSpace(fullPath) ? L("Par défaut") : File.Exists(fullPath) ? L("Disponible") : L("Absent")
            };
        }).ToList();

        WindowsSchemeCursorGrid.ItemsSource = rows;
        WindowsSchemeCursorGrid.SelectedIndex = rows.Count > 0 ? 0 : -1;
        WindowsStatusText.Text = scheme.IsActive
            ? L("Ce modèle est actuellement utilisé par Windows.")
            : L("Sélectionnez un rôle pour prévisualiser son curseur.");
    }

    private void WindowsSchemeCursorGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (WindowsSchemeCursorGrid.SelectedItem is not CursorRoleRow row) return;
        WindowsSchemePreviewRoleText.Text = string.IsNullOrWhiteSpace(row.DisplayRole) ? row.Role : row.DisplayRole;
        WindowsSchemePreviewPathText.Text = string.IsNullOrWhiteSpace(row.FullPath)
            ? L("Ce rôle utilise le curseur Windows par défaut.")
            : row.FullPath;

        if (string.IsNullOrWhiteSpace(row.FullPath) || !File.Exists(row.FullPath))
        {
            WindowsSchemePreviewHost.Cursor = System.Windows.Input.Cursors.Arrow;
            return;
        }

        try { WindowsSchemePreviewHost.Cursor = new System.Windows.Input.Cursor(row.FullPath); }
        catch { WindowsSchemePreviewHost.Cursor = System.Windows.Input.Cursors.Arrow; }
    }

    private void ApplyWindowsSchemeButton_Click(object sender, RoutedEventArgs e)
    {
        if (WindowsSchemeList.SelectedItem is not WindowsCursorScheme scheme)
        {
            WindowsStatusText.Text = L("Sélectionnez d'abord un modèle Windows.");
            return;
        }
        try
        {
            _cursorService.ApplyInstalledScheme(scheme);
            ReloadWindowsSchemes(scheme.Name);
            WindowsStatusText.Text = LF("Modèle Windows « {0} » appliqué.", scheme.Name);
        }
        catch (Exception ex) { ShowError(ex); }
    }

    private void RefreshWindowsSchemesButton_Click(object sender, RoutedEventArgs e)
    {
        var selected = (WindowsSchemeList.SelectedItem as WindowsCursorScheme)?.Name;
        ReloadWindowsSchemes(selected);
    }

    private void OpenWindowsCursorFolder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var folder = GetWindowsCursorFolder();
            if (!Directory.Exists(folder)) throw new DirectoryNotFoundException(folder);
            Process.Start(new ProcessStartInfo(folder) { UseShellExecute = true });
        }
        catch (Exception ex) { ShowError(ex); }
    }

    private string GetWindowsCursorFolder()
    {
        var windows = Environment.GetEnvironmentVariable("WINDIR");
        if (string.IsNullOrWhiteSpace(windows))
            windows = Directory.GetParent(Environment.SystemDirectory)?.FullName;
        if (string.IsNullOrWhiteSpace(windows))
            throw new DirectoryNotFoundException(L("Impossible de déterminer le dossier Windows."));
        return Path.Combine(windows, "Cursors");
    }

    private void RefreshHome()
    {
        if (HomePackCountText is null) return;
        HomePackCountText.Text = _allPacks.Count.ToString();
        HomeFavoriteCountText.Text = _allPacks.Count(p => p.IsFavorite).ToString();
        HomeIncompleteCountText.Text = _allPacks.Count(p => !p.IsComplete).ToString();
        HomeFavoriteList.ItemsSource = _allPacks.Where(p => p.IsFavorite).OrderBy(p => p.Name).ToList();

        try
        {
            var current = _cursorService.GetCurrentCursorValues(out var schemeName);
            var matchingPack = _allPacks.FirstOrDefault(pack => PackMatchesCurrent(pack, current));
            HomeActiveSchemeText.Text = matchingPack?.Name ?? (!string.IsNullOrWhiteSpace(schemeName) ? schemeName : L("Configuration personnalisée"));
        }
        catch
        {
            HomeActiveSchemeText.Text = L("Inconnu");
        }
    }

    private static bool PackMatchesCurrent(CursorPack pack, IReadOnlyDictionary<string, string> current)
    {
        if (pack.Cursors.Count == 0) return false;
        foreach (var (role, relative) in pack.Cursors)
        {
            if (!current.TryGetValue(role, out var currentPath)) return false;
            var packPath = SafeCombine(pack.FolderPath, relative);
            if (!NormalizePath(packPath).Equals(NormalizePath(currentPath), StringComparison.OrdinalIgnoreCase))
                return false;
        }
        return true;
    }

    private void HomeFavoriteList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (HomeFavoriteList.SelectedItem is not CursorPack pack) return;
        NavigateTo(1);
        ApplyFilter(pack.Id);
    }

    private void RefreshThemePackChoices()
    {
        if (LightThemePackComboBox is null || DarkThemePackComboBox is null) return;
        var previousInit = _initializingSettings;
        _initializingSettings = true;
        var choices = _allPacks.OrderBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
        LightThemePackComboBox.ItemsSource = choices.ToList();
        DarkThemePackComboBox.ItemsSource = choices.ToList();
        LightThemePackComboBox.SelectedValue = _settings.LightThemePackId;
        DarkThemePackComboBox.SelectedValue = _settings.DarkThemePackId;
        _initializingSettings = previousInit;
    }

    private void FollowWindowsThemeCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (_initializingSettings) return;
        _settings.FollowWindowsTheme = FollowWindowsThemeCheckBox.IsChecked == true;
        _settingsService.Save(_settings);
        ConfigureWindowsThemeMonitor(applyNow: _settings.FollowWindowsTheme);
        SettingsStatusText.Text = _settings.FollowWindowsTheme
            ? "Synchronisation avec le thème Windows activée."
            : "Synchronisation avec le thème Windows désactivée.";
    }

    private void ThemePackComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_initializingSettings) return;
        _settings.LightThemePackId = LightThemePackComboBox.SelectedValue?.ToString() ?? "";
        _settings.DarkThemePackId = DarkThemePackComboBox.SelectedValue?.ToString() ?? "";
        _settingsService.Save(_settings);
        if (_settings.FollowWindowsTheme) ConfigureWindowsThemeMonitor(applyNow: true);
    }

    private void ConfigureWindowsThemeMonitor(bool applyNow)
    {
        _windowsThemeTimer.Stop();
        _lastWindowsLightTheme = null;
        if (!_settings.FollowWindowsTheme) return;
        _windowsThemeTimer.Start();
        if (applyNow) ApplyPackForWindowsTheme(force: true);
    }

    private void WindowsThemeTimer_Tick(object? sender, EventArgs e) => ApplyPackForWindowsTheme(force: false);

    private void ApplyPackForWindowsTheme(bool force)
    {
        try
        {
            var light = WindowsThemeService.IsLightTheme();
            if (!force && _lastWindowsLightTheme == light) return;
            _lastWindowsLightTheme = light;
            var id = light ? _settings.LightThemePackId : _settings.DarkThemePackId;
            if (string.IsNullOrWhiteSpace(id)) return;
            var pack = _allPacks.FirstOrDefault(p => p.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
            if (pack is null) return;
            ApplyPack(pack, automatic: true);
            HomeStatusText.Text = $"Thème Windows {(light ? "clair" : "sombre")} : « {pack.Name} » appliqué.";
        }
        catch (Exception ex)
        {
            SettingsStatusText.Text = "Thème Windows : " + ex.Message;
        }
    }

    private void InitializeSystemIntegration()
    {
        try
        {
            // Si l'option est déjà enregistrée, réécrit le chemin courant afin de survivre à un déplacement du dossier publié.
            if (_settings.StartWithWindows)
                StartupService.SetEnabled(true);
            else if (StartupService.IsEnabled())
                _settings.StartWithWindows = true;

            _initializingSettings = true;
            StartWithWindowsCheckBox.IsChecked = _settings.StartWithWindows;
            MinimizeToTrayCheckBox.IsChecked = _settings.MinimizeToTray;
            _initializingSettings = false;
            _settingsService.Save(_settings);
        }
        catch
        {
            _initializingSettings = true;
            StartWithWindowsCheckBox.IsChecked = StartupService.IsEnabled();
            _initializingSettings = false;
        }

        _trayIcon ??= new TrayIconService(ShowFromTray, ExitApplication);
        _trayIcon.UpdateLanguage(_settings.Language);
        _trayIcon.Visible = _settings.MinimizeToTray;
    }

    private void PackContextMenu_Opened(object sender, RoutedEventArgs e)
    {
        var pack = PackList.SelectedItem as CursorPack;
        if (pack is null)
            return;

        if (ContextFavoriteIconText is not null)
        {
            ContextFavoriteIconText.Text = pack.IsFavorite ? "★" : "☆";
            ContextFavoriteIconText.Foreground = pack.IsFavorite
                ? new SolidColorBrush(Color.FromRgb(246, 209, 72))
                : new SolidColorBrush(Color.FromRgb(232, 232, 232));
        }
    }

    private void ApplyLanguageToUi()
    {
        LocalizationService.Apply(this, _settings.Language);
        if (PackList.ContextMenu is not null)
            LocalizationService.Apply(PackList.ContextMenu, _settings.Language);
        if (CurrentVersionText is not null)
            CurrentVersionText.Text = LF("Version actuelle : {0}", typeof(MainWindow).Assembly.GetName().Version);
        _trayIcon?.UpdateLanguage(_settings.Language);
    }

    private void LanguageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_initializingSettings || LanguageComboBox.SelectedItem is not ComboBoxItem item) return;

        var selectedLanguage = item.Tag?.ToString();
        _settings.UseSystemLanguage = string.Equals(selectedLanguage, "auto", StringComparison.OrdinalIgnoreCase);
        _settings.Language = _settings.UseSystemLanguage
            ? LocalizationService.DetectSystemLanguage()
            : LocalizationService.NormalizeLanguage(selectedLanguage);

        LocalizationService.SetLanguage(_settings.Language);
        _settingsService.Save(_settings);
        ApplyLanguageToUi();
        var selectedPackId = (PackList.SelectedItem as CursorPack)?.Id;
        var selectedSchemeName = (WindowsSchemeList.SelectedItem as WindowsCursorScheme)?.Name;
        ReloadPacks(selectedPackId);
        ReloadWindowsSchemes(selectedSchemeName);
        RefreshHome();
        SettingsStatusText.Text = L("Langue de l’interface. Le changement est appliqué immédiatement.");
    }

    private void StartupCheck_Changed(object sender, RoutedEventArgs e)
    {
        if (_initializingSettings) return;
        var enabled = StartWithWindowsCheckBox.IsChecked == true;
        try
        {
            StartupService.SetEnabled(enabled);
            _settings.StartWithWindows = enabled;
            _settingsService.Save(_settings);
            SettingsStatusText.Text = enabled
                ? LocalizationService.Translate("Exécuter CursorVault au démarrage de Windows", _settings.Language)
                : LocalizationService.Translate("Démarrage automatique désactivé.", _settings.Language);
        }
        catch (Exception ex)
        {
            _initializingSettings = true;
            StartWithWindowsCheckBox.IsChecked = !enabled;
            _initializingSettings = false;
            ShowError(ex);
        }
    }

    private void MinimizeToTrayCheck_Changed(object sender, RoutedEventArgs e)
    {
        if (_initializingSettings) return;
        _settings.MinimizeToTray = MinimizeToTrayCheckBox.IsChecked == true;
        _settingsService.Save(_settings);
        _trayIcon ??= new TrayIconService(ShowFromTray, ExitApplication);
        _trayIcon.UpdateLanguage(_settings.Language);
        _trayIcon.Visible = _settings.MinimizeToTray;
        SettingsStatusText.Text = _settings.MinimizeToTray
            ? LocalizationService.Translate("Le fonctionnement en arrière-plan est activé.", _settings.Language)
            : LocalizationService.Translate("Le fonctionnement en arrière-plan est désactivé.", _settings.Language);
    }

    private void MainWindow_StateChanged(object? sender, EventArgs e)
    {
        if (_settings.MinimizeToTray && WindowState == WindowState.Minimized)
            Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(HideToTray));
    }

    private void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        if (_forceClose || !_settings.MinimizeToTray) return;
        e.Cancel = true;
        HideToTray();
    }

    private void MainWindow_Closed(object? sender, EventArgs e)
    {
        _rotationTimer.Stop();
        _windowsThemeTimer.Stop();
        _trayIcon?.Dispose();
        _trayIcon = null;
    }

    private void HideToTray()
    {
        if (_hidingToTray || !_settings.MinimizeToTray) return;
        _hidingToTray = true;
        try
        {
            _trayIcon ??= new TrayIconService(ShowFromTray, ExitApplication);
            _trayIcon.UpdateLanguage(_settings.Language);
            _trayIcon.Visible = true;
            ShowInTaskbar = false;
            Hide();
            WindowState = WindowState.Normal;
        }
        finally
        {
            _hidingToTray = false;
        }
    }

    private void ShowFromTray()
    {
        Dispatcher.Invoke(() =>
        {
            ShowInTaskbar = true;
            Show();
            WindowState = WindowState.Normal;
            Activate();
        });
    }

    private void ExitApplication()
    {
        Dispatcher.Invoke(() =>
        {
            _forceClose = true;
            _trayIcon?.Dispose();
            _trayIcon = null;
            Close();
            System.Windows.Application.Current.Shutdown();
        });
    }

    private void QuitApplicationButton_Click(object sender, RoutedEventArgs e) => ExitApplication();

    private void InitializeFontChoices()
    {
        var installedFonts = Fonts.SystemFontFamilies
            .Select(font => font.Source)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.CurrentCultureIgnoreCase)
            .OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        if (!installedFonts.Any(name => name.Equals("Segoe UI", StringComparison.OrdinalIgnoreCase)))
            installedFonts.Insert(0, "Segoe UI");

        FontFamilyComboBox.ItemsSource = installedFonts;
        var selected = installedFonts.FirstOrDefault(name =>
            name.Equals(_settings.FontFamily, StringComparison.OrdinalIgnoreCase))
            ?? installedFonts.FirstOrDefault(name => name.Equals("Segoe UI", StringComparison.OrdinalIgnoreCase))
            ?? installedFonts.FirstOrDefault();

        if (!string.IsNullOrWhiteSpace(selected))
        {
            _settings.FontFamily = selected;
            FontFamilyComboBox.SelectedItem = selected;
        }
    }

    private void FontFamilyComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_initializingSettings || FontFamilyComboBox.SelectedItem is not string fontName || string.IsNullOrWhiteSpace(fontName)) return;

        _settings.FontFamily = fontName;
        _settingsService.Save(_settings);
        ApplyApplicationFont(fontName);
        SettingsStatusText.Text = LocalizationService.Translate("Police de l’interface appliquée.", _settings.Language);
    }

    private void ApplyApplicationFont(string? fontName)
    {
        var requested = string.IsNullOrWhiteSpace(fontName) ? "Segoe UI" : fontName.Trim();
        var installed = Fonts.SystemFontFamilies.FirstOrDefault(font =>
            font.Source.Equals(requested, StringComparison.OrdinalIgnoreCase));
        var family = installed ?? new FontFamily("Segoe UI");

        _settings.FontFamily = family.Source;
        System.Windows.Application.Current.Resources["AppFontFamily"] = family;
    }

    private void ThemeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_initializingSettings) return;
        _settings.Theme = ThemeComboBox.SelectedIndex switch
        {
            1 => "Light",
            2 => "Translucent",
            _ => "Dark"
        };
        _settingsService.Save(_settings);
        ApplyTheme(_settings.Theme);
        SettingsStatusText.Text = LocalizationService.Translate("Thème appliqué.", _settings.Language);
    }

    private void AccentColorComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_initializingSettings || AccentColorComboBox.SelectedItem is not ComboBoxItem item) return;
        var value = item.Tag?.ToString();
        if (string.IsNullOrWhiteSpace(value) || value.Equals("custom", StringComparison.OrdinalIgnoreCase)) return;

        _settings.AccentColor = NormalizeAccentColor(value);
        _settingsService.Save(_settings);
        ApplyTheme(_settings.Theme);
        SettingsStatusText.Text = LocalizationService.Translate("Couleur appliquée.", _settings.Language);
    }

    private void CustomAccentColorButton_Click(object sender, RoutedEventArgs e)
    {
        var current = ParseAccentColor(_settings.AccentColor);
        using var dialog = new System.Windows.Forms.ColorDialog
        {
            FullOpen = true,
            AnyColor = true,
            SolidColorOnly = true,
            Color = System.Drawing.Color.FromArgb(current.R, current.G, current.B)
        };

        if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;

        _settings.AccentColor = $"#{dialog.Color.R:X2}{dialog.Color.G:X2}{dialog.Color.B:X2}";
        _initializingSettings = true;
        AccentColorComboBox.SelectedIndex = AccentColorIndexFromValue(_settings.AccentColor);
        _initializingSettings = false;
        _settingsService.Save(_settings);
        ApplyTheme(_settings.Theme);
        SettingsStatusText.Text = LocalizationService.Translate("Couleur personnalisée appliquée.", _settings.Language);
    }

    private void ResetSettingsButton_Click(object sender, RoutedEventArgs e)
    {
        var result = System.Windows.MessageBox.Show(
            this,
            L("Réinitialiser tous les paramètres de CursorVault ? Les packs installés et les favoris seront conservés."),
            L("Réinitialiser CursorVault"),
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes) return;

        var selectedPackId = (PackList.SelectedItem as CursorPack)?.Id;
        var selectedSchemeName = (WindowsSchemeList.SelectedItem as WindowsCursorScheme)?.Name;
        var favorites = new HashSet<string>(_settings.FavoritePackIds, StringComparer.OrdinalIgnoreCase);

        // AppSettings contient les valeurs d'origine de CursorVault.
        // La langue par défaut suit automatiquement la langue d'affichage de Windows.
        _settings = new AppSettings
        {
            FavoritePackIds = favorites
        };
        _settings.Language = LocalizationService.DetectSystemLanguage();

        try
        {
            StartupService.SetEnabled(false);
        }
        catch
        {
            // Le fichier de paramètres reste la source de vérité même si Windows refuse
            // exceptionnellement la modification de la clé de démarrage.
        }

        _rotationTimer.Stop();
        _startupRotationHandled = false;

        if (_trayIcon is not null)
        {
            _trayIcon.Visible = false;
            _trayIcon.UpdateLanguage(_settings.Language);
        }

        ShowInTaskbar = true;
        LocalizationService.SetLanguage(_settings.Language);
        ApplyApplicationFont(_settings.FontFamily);
        ApplyTheme(_settings.Theme);
        InitializeSettingsControls();
        ApplyLanguageToUi();
        _settingsService.Save(_settings);

        ReloadPacks(selectedPackId);
        ReloadWindowsSchemes(selectedSchemeName);
        RefreshHome();
        ConfigureRotationTimer();
        ConfigureWindowsThemeMonitor(applyNow: false);
        ApplyInterfaceSize(_settings.InterfaceSize);
        RefreshStorageUsage();

        SettingsStatusText.Text = L("Les paramètres par défaut de CursorVault ont été restaurés.");
    }

    private void SettingsSortComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_initializingSettings) return;
        _settings.LibrarySort = SortNameFromIndex(SettingsSortComboBox.SelectedIndex);
        _settingsService.Save(_settings);
        _initializingSettings = true;
        LibrarySortComboBox.SelectedIndex = SettingsSortComboBox.SelectedIndex;
        _initializingSettings = false;
        ApplyFilter();
        SettingsStatusText.Text = "Tri de la bibliothèque enregistré.";
    }

    private void InterfaceSizeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_initializingSettings) return;
        _settings.InterfaceSize = InterfaceSizeNameFromIndex(InterfaceSizeComboBox.SelectedIndex);
        _settingsService.Save(_settings);
        ApplyInterfaceSize(_settings.InterfaceSize);
        SettingsStatusText.Text = "Taille de l'interface appliquée.";
    }

    private void ApplyInterfaceSize(string size)
    {
        switch (size)
        {
            case "Compacte":
                Width = 1260; Height = 760; MinWidth = 1040; MinHeight = 660;
                break;
            case "Grande":
                Width = 1580; Height = 960; MinWidth = 1240; MinHeight = 760;
                break;
            default:
                Width = 1420; Height = 860; MinWidth = 1120; MinHeight = 700;
                break;
        }
    }

    private void ExportFullBackupButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Exporter une sauvegarde CursorVault",
            Filter = "Sauvegarde CursorVault (*.cvb)|*.cvb",
            FileName = $"CursorVault-{DateTime.Now:yyyy-MM-dd}.cvb"
        };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            _settingsService.Save(_settings);
            _backupService.Export(dialog.FileName);
            SettingsStatusText.Text = $"Sauvegarde créée : {dialog.FileName}";
        }
        catch (Exception ex) { ShowError(ex); }
    }

    private void ImportFullBackupButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Importer une sauvegarde CursorVault",
            Filter = "Sauvegarde CursorVault (*.cvb)|*.cvb"
        };
        if (dialog.ShowDialog(this) != true) return;
        var confirm = System.Windows.MessageBox.Show(this,
            "Importer cette sauvegarde ? Les packs portant le même nom seront remplacés et les paramètres sauvegardés seront restaurés au prochain démarrage.",
            "CursorVault", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes) return;
        try
        {
            _backupService.Import(dialog.FileName);
            ReloadPacks();
            ReloadWindowsSchemes();
            RefreshHome();
            RefreshStorageUsage();
            SettingsStatusText.Text = "Sauvegarde importée. Redémarrez CursorVault pour appliquer tous les paramètres restaurés.";
        }
        catch (Exception ex) { ShowError(ex); }
    }

    private void PortableModeCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (_initializingSettings) return;
        var enable = PortableModeCheckBox.IsChecked == true;
        try
        {
            PortableModeService.RequestMode(enable);
            SettingsStatusText.Text = enable
                ? "Mode portable préparé. Redémarrez CursorVault pour utiliser le dossier Data à côté de l'exécutable."
                : "Mode portable désactivé pour le prochain démarrage. Les données ont été copiées vers LocalAppData.";
        }
        catch (Exception ex)
        {
            _initializingSettings = true;
            PortableModeCheckBox.IsChecked = AppPaths.IsPortable;
            _initializingSettings = false;
            ShowError(ex);
        }
    }

    private void RefreshStorageUsage()
    {
        if (StorageUsageText is null) return;
        var u = StorageService.GetUsage();
        StorageUsageText.Text = $"Packs : {FormatBytes(u.PacksBytes)}  •  Cache : {FormatBytes(u.TempBytes)}  •  Sauvegarde : {FormatBytes(u.BackupBytes)}  •  Total : {FormatBytes(u.TotalBytes)}";
    }

    private void ShowDiagnosticButton_Click(object sender, RoutedEventArgs e)
    {
        NavigateTo(3);
        RefreshDiagnosticPage();
    }

    private void RefreshDiagnosticButton_Click(object sender, RoutedEventArgs e) => RefreshDiagnosticPage();

    private void RefreshDiagnosticPage()
    {
        if (DiagnosticReportTextBox is null) return;
        DiagnosticReportTextBox.Text = DiagnosticService.BuildReport(_settings, _allPacks.Count, _settings.FavoritePackIds.Count);
        var u = StorageService.GetUsage();
        DiagnosticStorageText.Text = $"Packs : {FormatBytes(u.PacksBytes)} • Cache : {FormatBytes(u.TempBytes)} • Total : {FormatBytes(u.TotalBytes)}";
    }

    private void CopyDiagnosticButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var report = DiagnosticService.BuildReport(_settings, _allPacks.Count, _settings.FavoritePackIds.Count);
            System.Windows.Clipboard.SetText(report);
            if (SettingsStatusText is not null) SettingsStatusText.Text = "Diagnostic copié dans le presse-papiers.";
            RefreshDiagnosticPage();
        }
        catch (Exception ex) { ShowError(ex); }
    }

    private void ClearCacheButton_Click(object sender, RoutedEventArgs e)
    {
        StorageService.ClearTemp();
        RefreshStorageUsage();
        RefreshDiagnosticPage();
        if (SettingsStatusText is not null) SettingsStatusText.Text = "Cache temporaire nettoyé.";
    }

    private async void CheckUpdatesButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            SettingsStatusText.Text = "Recherche de mise à jour...";
            var result = await _updateService.CheckAsync();
            if (!result.UpdateAvailable)
            {
                SettingsStatusText.Text = $"CursorVault est à jour ({result.CurrentVersion}).";
                return;
            }

            var message = $"Une nouvelle version est disponible.\n\nActuelle : {result.CurrentVersion}\nDisponible : {result.LatestVersion}";
            if (!string.IsNullOrWhiteSpace(result.Notes)) message += $"\n\n{result.Notes}";
            if (!string.IsNullOrWhiteSpace(result.DownloadUrl))
            {
                message += "\n\nOuvrir la page de téléchargement ?";
                if (System.Windows.MessageBox.Show(this, message, "Mise à jour CursorVault", MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.Yes)
                    Process.Start(new ProcessStartInfo(result.DownloadUrl) { UseShellExecute = true });
            }
            else
            {
                System.Windows.MessageBox.Show(this, message, "Mise à jour CursorVault", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            SettingsStatusText.Text = $"Version {result.LatestVersion} disponible.";
        }
        catch (Exception ex) { ShowError(ex); }
    }

    private void MissingRoleBehaviorComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_initializingSettings) return;
        _settings.MissingRoleBehavior = MissingRoleBehaviorComboBox.SelectedIndex switch
        {
            1 => MissingRoleBehavior.WindowsDefault,
            2 => MissingRoleBehavior.ChooseManually,
            _ => MissingRoleBehavior.KeepCurrent
        };
        _settingsService.Save(_settings);
        SettingsStatusText.Text = L("Comportement des rôles manquants enregistré.");
    }

    private void RotationCheck_Changed(object sender, RoutedEventArgs e) => SaveRotationSettings();
    private void RotationMode_SelectionChanged(object sender, SelectionChangedEventArgs e) => SaveRotationSettings();

    private void SaveRotationSettings()
    {
        if (_initializingSettings) return;
        _settings.RotationEnabled = RotationEnabledCheckBox.IsChecked == true;
        _settings.RotationMode = RotationModeNameFromIndex(RotationModeComboBox.SelectedIndex);
        _settings.RotationFavoritesOnly = RotationFavoritesOnlyCheckBox.IsChecked == true;
        _settingsService.Save(_settings);
        ConfigureRotationTimer();
        SettingsStatusText.Text = _settings.RotationEnabled
            ? LF("Rotation activée : {0}{1}.", L(_settings.RotationMode), _settings.RotationFavoritesOnly ? " • " + L("favoris uniquement") : "")
            : L("Rotation automatique désactivée.");
    }

    private void ApplyTheme(string theme)
    {
        var light = theme.Equals("Light", StringComparison.OrdinalIgnoreCase);
        var translucent = theme.Equals("Translucent", StringComparison.OrdinalIgnoreCase);
        var accent = ParseAccentColor(_settings.AccentColor);

        if (translucent)
        {
            // Verre plus léger : la couleur choisie teinte le fond et les halos,
            // tandis que les cartes gardent assez de densité pour rester lisibles.
            var windowTint = Blend(Color.FromRgb(8, 11, 17), accent, 0.11);
            var panelTint = Blend(Color.FromRgb(17, 21, 29), accent, 0.10);
            var panelAltTint = Blend(Color.FromRgb(24, 29, 39), accent, 0.13);
            var borderTint = Blend(Color.FromRgb(102, 115, 136), accent, 0.34);
            var hoverTint = Blend(Color.FromRgb(39, 47, 61), accent, 0.19);
            var selectedTint = Blend(Color.FromRgb(27, 34, 47), accent, 0.30);
            var glowSecondary = Blend(accent, Colors.White, 0.26);

            SetBrush("WindowBrush", Color.FromArgb(92, windowTint.R, windowTint.G, windowTint.B));
            SetBrush("PanelBrush", Color.FromArgb(184, panelTint.R, panelTint.G, panelTint.B));
            SetBrush("PanelAltBrush", Color.FromArgb(169, panelAltTint.R, panelAltTint.G, panelAltTint.B));
            SetBrush("BorderBrush", Color.FromArgb(116, borderTint.R, borderTint.G, borderTint.B));
            SetBrush("TextBrush", Color.FromRgb(248, 250, 253));
            SetBrush("MutedBrush", Color.FromRgb(192, 201, 214));
            SetBrush("HoverBrush", Color.FromArgb(190, hoverTint.R, hoverTint.G, hoverTint.B));
            SetBrush("SelectedBrush", Color.FromArgb(208, selectedTint.R, selectedTint.G, selectedTint.B));
            SetBrush("SelectedBorderBrush", Color.FromArgb(210, accent.R, accent.G, accent.B));

            // Deux halos diffus donnent de la profondeur au verre sans saturer l'interface.
            SetBrush("GlassGlowBrush", Color.FromArgb(54, accent.R, accent.G, accent.B));
            SetBrush("GlassGlowSecondaryBrush", Color.FromArgb(34, glowSecondary.R, glowSecondary.G, glowSecondary.B));
            SetBrush("GlassSheenBrush", Color.FromArgb(70, 255, 255, 255));
        }
        else
        {
            var panel = light ? Colors.White : Color.FromRgb(25, 28, 35);
            SetBrush("WindowBrush", light ? Color.FromRgb(243, 246, 250) : Color.FromRgb(17, 19, 24));
            SetBrush("PanelBrush", panel);
            SetBrush("PanelAltBrush", light ? Color.FromRgb(238, 242, 247) : Color.FromRgb(32, 36, 45));
            SetBrush("BorderBrush", light ? Color.FromRgb(204, 212, 223) : Color.FromRgb(48, 54, 66));
            SetBrush("TextBrush", light ? Color.FromRgb(23, 32, 43) : Color.FromRgb(242, 244, 248));
            SetBrush("MutedBrush", light ? Color.FromRgb(102, 115, 134) : Color.FromRgb(154, 164, 178));
            SetBrush("HoverBrush", light ? Color.FromRgb(231, 237, 245) : Color.FromRgb(39, 44, 54));
            SetBrush("SelectedBrush", Blend(panel, accent, light ? 0.16 : 0.22));
            SetBrush("SelectedBorderBrush", Blend(accent, light ? Color.FromRgb(25, 35, 50) : Colors.White, light ? 0.04 : 0.16));
            SetBrush("GlassGlowBrush", Colors.Transparent);
            SetBrush("GlassGlowSecondaryBrush", Colors.Transparent);
            SetBrush("GlassSheenBrush", Colors.Transparent);
        }

        SetBrush("AccentBrush", accent);
        SetBrush("AccentTextBrush", GetContrastTextColor(accent));

        // Les zones d’aperçu restent translucides mais suivent toujours la couleur choisie.
        // Le fond est volontairement léger pour ne pas gêner la lecture du curseur.
        var previewAlpha = translucent ? (byte)38 : light ? (byte)34 : (byte)42;
        var previewBorderAlpha = translucent ? (byte)118 : (byte)120;
        SetBrush("PreviewTintBrush", Color.FromArgb(previewAlpha, accent.R, accent.G, accent.B));
        SetBrush("PreviewBorderBrush", Color.FromArgb(previewBorderAlpha, accent.R, accent.G, accent.B));

        if (AccentColorPreview is not null)
            AccentColorPreview.Background = new SolidColorBrush(accent);

        ApplyNativeTitleBarColors();
        ApplyBackdropForTheme();
    }

    private void ApplyBackdropForTheme()
    {
        try
        {
            var source = PresentationSource.FromVisual(this) as HwndSource;
            if (source is null || source.Handle == IntPtr.Zero) return;

            var translucent = _settings.Theme.Equals("Translucent", StringComparison.OrdinalIgnoreCase);
            uint backdrop = translucent ? DwmSystemBackdropTransientWindow : DwmSystemBackdropNone;
            _ = DwmSetWindowAttribute(source.Handle, DwmwaSystemBackdropType, ref backdrop, sizeof(uint));

            var margins = translucent
                ? new Margins { Left = -1, Right = -1, Top = -1, Bottom = -1 }
                : new Margins { Left = 0, Right = 0, Top = 0, Bottom = 0 };
            _ = DwmExtendFrameIntoClientArea(source.Handle, ref margins);

            source.CompositionTarget.BackgroundColor = translucent
                ? Colors.Transparent
                : (_settings.Theme.Equals("Light", StringComparison.OrdinalIgnoreCase)
                    ? Color.FromRgb(243, 246, 250)
                    : Color.FromRgb(17, 19, 24));
        }
        catch (DllNotFoundException) { }
        catch (EntryPointNotFoundException) { }
    }

    private static Color ParseAccentColor(string? value)
    {
        var normalized = NormalizeAccentColor(value);
        try
        {
            return (Color)ColorConverter.ConvertFromString(normalized);
        }
        catch
        {
            return Color.FromRgb(110, 168, 254);
        }
    }

    private static string NormalizeAccentColor(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "#6EA8FE";
        var text = value.Trim();
        if (!text.StartsWith("#", StringComparison.Ordinal)) text = "#" + text;
        if (text.Length != 7) return "#6EA8FE";
        for (var i = 1; i < text.Length; i++)
            if (!Uri.IsHexDigit(text[i])) return "#6EA8FE";
        return text.ToUpperInvariant();
    }

    private static Color GetContrastTextColor(Color background)
    {
        var luminance = (0.2126 * background.R + 0.7152 * background.G + 0.0722 * background.B) / 255.0;
        return luminance > 0.58 ? Color.FromRgb(8, 17, 31) : Colors.White;
    }

    private static Color Blend(Color baseColor, Color overlay, double amount)
    {
        amount = Math.Clamp(amount, 0, 1);
        byte Mix(byte a, byte b) => (byte)Math.Round(a + ((b - a) * amount));
        return Color.FromRgb(Mix(baseColor.R, overlay.R), Mix(baseColor.G, overlay.G), Mix(baseColor.B, overlay.B));
    }

    private static void SetBrush(string key, Color color)
    {
        if (System.Windows.Application.Current.Resources[key] is SolidColorBrush brush && !brush.IsFrozen)
        {
            brush.Color = color;
            return;
        }
        System.Windows.Application.Current.Resources[key] = new SolidColorBrush(color);
    }

    private void OpenDataFolderButton_Click(object sender, RoutedEventArgs e)
    {
        AppPaths.Ensure();
        Process.Start(new ProcessStartInfo(AppPaths.DataRoot) { UseShellExecute = true });
    }

    private void HomeNavigationButton_Click(object sender, RoutedEventArgs e) => NavigateTo(0);
    private void LibraryNavigationButton_Click(object sender, RoutedEventArgs e) => NavigateTo(1);
    private void WindowsNavigationButton_Click(object sender, RoutedEventArgs e) => NavigateTo(2);
    private void DiagnosticNavigationButton_Click(object sender, RoutedEventArgs e)
    {
        NavigateTo(3);
        RefreshDiagnosticPage();
    }
    private void SettingsNavigationButton_Click(object sender, RoutedEventArgs e) => NavigateTo(4);

    private void NavigateTo(int index)
    {
        MainTabs.SelectedIndex = index;
        HomeNavigationButton.IsChecked = index == 0;
        LibraryNavigationButton.IsChecked = index == 1;
        WindowsNavigationButton.IsChecked = index == 2;
        DiagnosticNavigationButton.IsChecked = index == 3;
        SettingsNavigationButton.IsChecked = index == 4;
    }

    private static int AccentColorIndexFromValue(string? value)
    {
        var normalized = NormalizeAccentColor(value);
        return normalized switch
        {
            "#6EA8FE" => 0,
            "#A78BFA" => 1,
            "#55D69E" => 2,
            "#55C7F3" => 3,
            "#FFB15C" => 4,
            "#FF6B75" => 5,
            "#F277B8" => 6,
            "#E7C35A" => 7,
            _ => 8
        };
    }

    private static int LanguageIndexFromCode(string code) => LocalizationService.NormalizeLanguage(code) switch
    {
        "fr-FR" => 1,
        "en-US" => 2,
        "es-ES" => 3,
        "de-DE" => 4,
        "it-IT" => 5,
        _ => 1
    };

    private static string SortNameFromIndex(int index) => index switch
    {
        1 => "Nom A-Z",
        2 => "Nom Z-A",
        3 => "Créateur",
        4 => "Complets d'abord",
        5 => "Plus récemment ajoutés",
        _ => "Favoris d'abord"
    };

    private static int SortIndexFromName(string? value) => value switch
    {
        "Nom A-Z" => 1,
        "Nom Z-A" => 2,
        "Créateur" => 3,
        "Complets d'abord" => 4,
        "Plus récemment ajoutés" => 5,
        _ => 0
    };

    private static string InterfaceSizeNameFromIndex(int index) => index switch
    {
        0 => "Compacte",
        2 => "Grande",
        _ => "Normale"
    };

    private static int InterfaceSizeIndexFromName(string? value) => value switch
    {
        "Compacte" => 0,
        "Grande" => 2,
        _ => 1
    };

    private static string PackFilterNameFromIndex(int index) => index switch
    {
        1 => "Complets",
        2 => "Incomplets",
        3 => "Animés",
        4 => "Statiques",
        5 => "Favoris",
        _ => "Tous"
    };

    private static string RotationModeNameFromIndex(int index) => index switch
    {
        1 => "Toutes les heures",
        2 => "Tous les jours",
        _ => "Démarrage"
    };

    private static int FilterIndexFromName(string filter) => filter switch
    {
        "Complets" => 1,
        "Incomplets" => 2,
        "Animés" => 3,
        "Statiques" => 4,
        "Favoris" => 5,
        _ => 0
    };

    private static string SafeCombine(string root, string relative)
    {
        try { return Path.GetFullPath(Path.Combine(root, relative)); }
        catch { return Path.Combine(root, relative); }
    }

    private static string NormalizePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return "";
        try { return Path.GetFullPath(Environment.ExpandEnvironmentVariables(path.Trim().Trim('"'))); }
        catch { return path; }
    }

    private static bool IsCursorExtension(string path) =>
        path.EndsWith(".cur", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".ani", StringComparison.OrdinalIgnoreCase);

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024) return $"{bytes} o";
        if (bytes < 1024 * 1024) return $"{bytes / 1024d:0.0} Ko";
        return $"{bytes / 1024d / 1024d:0.0} Mo";
    }

    private static string MakeSafeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return new string(value.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
    }

    private string L(string french) => LocalizationService.Translate(french, _settings.Language);

    private string LF(string frenchFormat, params object?[] args)
        => LocalizationService.Format(frenchFormat, _settings.Language, args);

    private string LM(string value) => LocalizationService.TranslateMetadata(value, _settings.Language);

    private string LocalizeErrorMessage(string message)
        => LocalizationService.Translate(message, _settings.Language);

    private void ShowError(Exception ex)
    {
        var message = LocalizeErrorMessage(ex.Message);
        if (StatusText is not null) StatusText.Text = message;
        if (WindowsStatusText is not null) WindowsStatusText.Text = message;
        if (SettingsStatusText is not null) SettingsStatusText.Text = message;
        if (HomeStatusText is not null) HomeStatusText.Text = message;
        System.Windows.MessageBox.Show(this, message, "CursorVault", MessageBoxButton.OK, MessageBoxImage.Error);
    }
}
