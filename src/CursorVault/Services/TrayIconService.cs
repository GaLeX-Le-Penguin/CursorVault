using System;

namespace CursorVault.Services;

public sealed class TrayIconService : IDisposable
{
    private readonly System.Windows.Forms.NotifyIcon _notifyIcon;
    private readonly System.Windows.Forms.ContextMenuStrip _menu;
    private readonly System.Windows.Forms.ToolStripMenuItem _showItem;
    private readonly System.Windows.Forms.ToolStripMenuItem _exitItem;
    private readonly Action _showAction;
    private readonly Action _exitAction;
    private readonly System.Drawing.Icon? _appIcon;
    private bool _disposed;

    public TrayIconService(Action showAction, Action exitAction)
    {
        _showAction = showAction;
        _exitAction = exitAction;

        _showItem = new System.Windows.Forms.ToolStripMenuItem();
        _exitItem = new System.Windows.Forms.ToolStripMenuItem();
        _showItem.Click += (_, _) => _showAction();
        _exitItem.Click += (_, _) => _exitAction();

        _menu = new System.Windows.Forms.ContextMenuStrip();
        _menu.Items.Add(_showItem);
        _menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        _menu.Items.Add(_exitItem);

        try
        {
            var executablePath = Environment.ProcessPath;
            _appIcon = string.IsNullOrWhiteSpace(executablePath)
                ? null
                : System.Drawing.Icon.ExtractAssociatedIcon(executablePath);
        }
        catch
        {
            _appIcon = null;
        }

        _notifyIcon = new System.Windows.Forms.NotifyIcon
        {
            Text = "CursorVault",
            Icon = _appIcon ?? System.Drawing.SystemIcons.Application,
            ContextMenuStrip = _menu,
            Visible = false
        };
        _notifyIcon.DoubleClick += (_, _) => _showAction();
    }

    public bool Visible
    {
        get => _notifyIcon.Visible;
        set
        {
            if (_disposed) return;
            _notifyIcon.Visible = value;
        }
    }

    public void UpdateLanguage(string language)
    {
        if (_disposed) return;
        _showItem.Text = LocalizationService.Translate("Afficher CursorVault", language);
        _exitItem.Text = LocalizationService.Translate("Quitter CursorVault", language);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _appIcon?.Dispose();
        _menu.Dispose();
    }
}
