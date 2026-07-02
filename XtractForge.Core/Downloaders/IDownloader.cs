using XtractForge.Core.Engine;
using XtractForge.Core.Models;

namespace XtractForge.Core.Downloaders;

/// <summary>A compiled-in download tool. Fixed set — no dynamic loading, ever.</summary>
public interface IDownloader
{
    string Id { get; }
    string Name { get; }
    string Summary { get; }
    string BinaryDefault { get; }
    string InstallHint { get; }
    /// <summary>
    /// True when the underlying tool can continue a partial download. On
    /// Windows pause = kill + relaunch with the continue flag, so only
    /// resumable tools expose pause.
    /// </summary>
    bool SupportsResume { get; }

    string BinaryPath(AppSettings settings);
    Task<DependencyStatus> CheckDependencyAsync(AppSettings settings);
    bool CanHandle(string url);
    Task<MediaInfo> GetInfoAsync(string url, AppSettings settings);
    Command BuildArgs(string url, DownloadOptions options, AppSettings settings);
    ProgressUpdate? ParseProgress(string line);
}

public abstract class DownloaderBase : IDownloader
{
    public abstract string Id { get; }
    public abstract string Name { get; }
    public abstract string Summary { get; }
    public abstract string BinaryDefault { get; }
    public abstract string InstallHint { get; }
    public virtual bool SupportsResume => false;

    public abstract string BinaryPath(AppSettings settings);
    public abstract bool CanHandle(string url);
    public abstract Task<MediaInfo> GetInfoAsync(string url, AppSettings settings);
    public abstract Command BuildArgs(string url, DownloadOptions options, AppSettings settings);
    public abstract ProgressUpdate? ParseProgress(string line);

    public virtual Task<DependencyStatus> CheckDependencyAsync(AppSettings settings) =>
        VersionCheckAsync(BinaryPath(settings), "--version");

    protected static async Task<DependencyStatus> VersionCheckAsync(string binary, string flag)
    {
        var result = await ProcessRunner.CaptureAsync(binary, flag);
        var firstLine = result.Stdout.Trim().Split('\n').FirstOrDefault()?.Trim() ?? "";
        return new DependencyStatus(result.Success, firstLine);
    }

    protected static string PickPath(string configured, string fallback) =>
        string.IsNullOrWhiteSpace(configured) ? fallback : configured;
}

/// <summary>Fixed registry. Routing order: most specific first, yt-dlp catch-all last.</summary>
public static class DownloaderRegistry
{
    public static IReadOnlyList<IDownloader> All { get; } =
    [
        new SpotDl(), new GalleryDl(), new Lux(), new FFmpegTool(), new Curl(), new YtDlp(),
    ];

    public static IDownloader? ById(string id) => All.FirstOrDefault(d => d.Id == id);

    /// <summary>
    /// First enabled downloader whose CanHandle matches; yt-dlp is the
    /// catch-all (unless disabled, in which case null).
    /// </summary>
    public static IDownloader? Route(string url, IReadOnlyCollection<string>? disabled = null) =>
        All.FirstOrDefault(d => (disabled is null || !disabled.Contains(d.Id)) && d.CanHandle(url));
}

internal static class UrlHelpers
{
    /// <summary>Last path component of a URL, decoded; fallback when empty/unparseable.</summary>
    public static string FilenameFromUrl(string url, string fallback)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return fallback;
        var last = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? "";
        var decoded = Uri.UnescapeDataString(last);
        return decoded.Length == 0 ? fallback : decoded;
    }

    public static bool IsLocalPath(string url) =>
        url.StartsWith("file://", StringComparison.OrdinalIgnoreCase)
        || url.StartsWith('/')
        || (url.Length > 2 && char.IsAsciiLetter(url[0]) && url[1] == ':' && (url[2] == '\\' || url[2] == '/'));

    public static string LocalPath(string url) =>
        url.StartsWith("file://", StringComparison.OrdinalIgnoreCase) && Uri.TryCreate(url, UriKind.Absolute, out var uri)
            ? uri.LocalPath
            : url;
}
