using Microsoft.Win32;

namespace CursorVault.Services;

public static class WindowsThemeService
{
    public static bool IsLightTheme()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", false);
        var value = key?.GetValue("AppsUseLightTheme");
        return value is null || System.Convert.ToInt32(value) != 0;
    }
}
