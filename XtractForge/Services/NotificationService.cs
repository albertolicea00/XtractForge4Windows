using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;

namespace XtractForge.Services;

/// <summary>Completion/failure toasts. Best-effort: never let a toast failure break a download.</summary>
public static class NotificationService
{
    private static bool _registered;

    private static bool EnsureRegistered()
    {
        if (_registered) return true;
        try
        {
            AppNotificationManager.Default.Register();
            _registered = true;
        }
        catch (Exception)
        {
            // Unpackaged/unsupported environment — run without toasts.
        }
        return _registered;
    }

    public static void NotifyCompleted(string title, string? destination)
    {
        if (!EnsureRegistered()) return;
        try
        {
            var builder = new AppNotificationBuilder()
                .AddText("Download complete")
                .AddText(title);
            if (!string.IsNullOrEmpty(destination))
                builder.AddText(destination);
            AppNotificationManager.Default.Show(builder.BuildNotification());
        }
        catch (Exception) { /* best effort */ }
    }

    public static void NotifyFailed(string title, string error)
    {
        if (!EnsureRegistered()) return;
        try
        {
            var notification = new AppNotificationBuilder()
                .AddText("Download failed")
                .AddText(title)
                .AddText(error)
                .BuildNotification();
            AppNotificationManager.Default.Show(notification);
        }
        catch (Exception) { /* best effort */ }
    }
}
