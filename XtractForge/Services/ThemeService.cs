using Microsoft.UI.Xaml;
using XtractForge.Core.Models;

namespace XtractForge.Services;

/// <summary>System / Light / Dark only — the OS owns the palette.</summary>
public static class ThemeService
{
    public static void Apply(Window window, AppearanceSetting appearance)
    {
        if (window.Content is FrameworkElement root)
        {
            root.RequestedTheme = appearance switch
            {
                AppearanceSetting.Light => ElementTheme.Light,
                AppearanceSetting.Dark => ElementTheme.Dark,
                _ => ElementTheme.Default,
            };
        }
    }
}
