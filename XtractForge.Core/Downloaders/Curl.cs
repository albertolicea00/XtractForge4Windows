using System.Text.RegularExpressions;
using XtractForge.Core.Engine;
using XtractForge.Core.Models;

namespace XtractForge.Core.Downloaders;

/// <summary>Direct file URLs straight to disk (no extraction). Pre-installed on Windows 10+.</summary>
public sealed partial class Curl : DownloaderBase
{
    public override string Id => "curl";
    public override string Name => "curl";
    public override string Summary => "Direct file URLs straight to disk";
    public override string BinaryDefault => "curl";
    public override string InstallHint => "Pre-installed on Windows 10 1803+";
    public override bool SupportsResume => true;

    [GeneratedRegex(@"\.(mp4|mkv|webm|mov|avi|flv|mp3|m4a|aac|flac|wav|ogg|opus|zip|rar|7z|pdf|jpg|jpeg|png|gif|webp|apk|dmg|exe|iso|gz|tar)(\?|#|$)", RegexOptions.IgnoreCase)]
    private static partial Regex FileExtRegex();

    [GeneratedRegex(@"^\s*(\d{1,3})\b")]
    private static partial Regex PercentRegex();

    [GeneratedRegex(@"curl\s+([\d.]+)", RegexOptions.IgnoreCase)]
    private static partial Regex VersionRegex();

    public override string BinaryPath(AppSettings settings) => PickPath(settings.CurlPath, BinaryDefault);

    public override async Task<DependencyStatus> CheckDependencyAsync(AppSettings settings)
    {
        var result = await ProcessRunner.CaptureAsync(BinaryPath(settings), "--version");
        var match = VersionRegex().Match(result.Stdout);
        var version = match.Success
            ? match.Groups[1].Value
            : result.Stdout.Split('\n').FirstOrDefault()?.Trim() ?? "";
        return new DependencyStatus(result.Success, version);
    }

    public override bool CanHandle(string url)
    {
        if (FFmpegTool.StreamRegex().IsMatch(url)) return false;
        return FileExtRegex().IsMatch(url);
    }

    public override Task<MediaInfo> GetInfoAsync(string url, AppSettings settings)
    {
        var name = UrlHelpers.FilenameFromUrl(url, "download");
        return Task.FromResult(new MediaInfo
        {
            Title = name,
            DownloaderId = Id,
            OptionFields =
            [
                new OptionField("filename", "Save as", OptionKind.Text, name, Placeholder: name,
                    Help: "curl saves the file as-is; it does not convert formats."),
            ],
        });
    }

    public override Command BuildArgs(string url, DownloadOptions options, AppSettings settings)
    {
        var name = options.PluginOptions.TryGetValue("filename", out var custom)
            && !string.IsNullOrWhiteSpace(custom)
                ? custom
                : UrlHelpers.FilenameFromUrl(url, "download");
        var output = Path.Combine(options.DownloadFolder, name);

        var args = new List<string> { "-L", "--create-dirs" };
        if (options.Resume)
            args.AddRange(["-C", "-"]);
        args.AddRange(["-o", output, url]);
        if (!string.IsNullOrWhiteSpace(settings.SpeedLimit))
            args.AddRange(["--limit-rate", settings.SpeedLimit]);

        return new Command(BinaryPath(settings), args);
    }

    public override ProgressUpdate? ParseProgress(string line)
    {
        var match = PercentRegex().Match(line);
        if (!match.Success) return null;
        var pct = int.Parse(match.Groups[1].Value);
        if (pct > 100) return null;
        return new ProgressUpdate(Percent: pct);
    }
}
