using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using CursorVault.Models;
using CursorVault.Services;
using Microsoft.Win32;

namespace CursorVault.Windows;

public partial class PackCreatorWindow : Window
{
    private readonly PackService _packService;
    private readonly List<CreatorRoleItem> _roles = new();

    public CursorPack? CreatedPack { get; private set; }

    public PackCreatorWindow(PackService packService)
    {
        InitializeComponent();
        _packService = packService;
        foreach (var role in CursorSystemService.KnownRoles)
            _roles.Add(new CreatorRoleItem(role));
        RoleList.ItemsSource = _roles;
    }

    private void BrowseRole_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not CreatorRoleItem item) return;
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = LocalizationService.Format("Curseur pour {0}", LocalizationService.CurrentLanguage, item.DisplayRole),
            Filter = LocalizationService.Translate("Curseurs Windows (*.cur;*.ani)|*.cur;*.ani|Tous les fichiers (*.*)|*.*", LocalizationService.CurrentLanguage)
        };
        if (dialog.ShowDialog(this) == true)
            item.FilePath = dialog.FileName;
    }

    private void Create_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(NameBox.Text))
                throw new InvalidOperationException(LocalizationService.Translate("Le nom du pack est obligatoire.", LocalizationService.CurrentLanguage));
            if (string.IsNullOrWhiteSpace(AuthorBox.Text))
                throw new InvalidOperationException(LocalizationService.Translate("Le créateur original est obligatoire.", LocalizationService.CurrentLanguage));

            var mappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in _roles)
            {
                if (!string.IsNullOrWhiteSpace(item.FilePath))
                    mappings[item.Role] = item.FilePath;
            }

            CreatedPack = _packService.CreatePack(NameBox.Text, AuthorBox.Text, DescriptionBox.Text, mappings);
            DialogResult = true;
        }
        catch (Exception ex)
        {
            ErrorText.Text = ex.Message;
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private sealed class CreatorRoleItem : INotifyPropertyChanged
    {
        private string _filePath = "";
        public string Role { get; }
        public string DisplayRole => LocalizationService.TranslateRole(Role, LocalizationService.CurrentLanguage);
        public string ChooseText => LocalizationService.Translate("Choisir…", LocalizationService.CurrentLanguage);
        public string FilePath
        {
            get => _filePath;
            set
            {
                _filePath = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(FileDisplay));
            }
        }
        public string FileDisplay => string.IsNullOrWhiteSpace(FilePath) ? LocalizationService.Translate("Non défini", LocalizationService.CurrentLanguage) : Path.GetFileName(FilePath);

        public CreatorRoleItem(string role) => Role = role;
        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
