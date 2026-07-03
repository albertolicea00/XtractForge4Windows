using XtractForge.Core.Engine;

namespace XtractForge.Services;

/// <summary>
/// Single entry point for every URL that enters the app (drop, paste,
/// clipboard watch — and later the xtractforge:// protocol).
/// </summary>
public sealed class IntakeService(DownloadManager manager)
{
    /// <summary>Extracts URLs from arbitrary text and queues each one. Returns how many.</summary>
    public int Submit(string text)
    {
        var urls = Intake.ExtractUrls(text);
        foreach (var url in urls)
            _ = manager.SubmitAsync(url);
        return urls.Count;
    }
}
