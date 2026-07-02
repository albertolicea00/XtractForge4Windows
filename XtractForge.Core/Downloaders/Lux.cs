using System.Text.Json;
using System.Text.RegularExpressions;
using XtractForge.Core.Engine;
using XtractForge.Core.Models;

namespace XtractForge.Core.Downloaders;

/// <summary>
/// Fast downloader for Bilibili, Douyin, Kuaishou, and other Asian sites.
/// Unlike the old plugin, youtube/twitter/instagram are NOT claimed here —
/// YouTube belongs to yt-dlp, twitter/instagram to gallery-dl.
/// </summary>
public sealed partial class Lux : DownloaderBase
{
    internal static readonly string[] HandledSites =
    [
        "bilibili.com", "douyin.com", "kuaishou.com", "weibo.com",
        "mgtv.com", "iqiyi.com", "youku.com", "v.qq.com", "acfun.cn",
        "huya.com", "douyu.com",
    ];

    public override string Id => "lux";
    public override string Name => "Lux";
    public override string Summary => "Bilibili, Douyin, Kuaishou, and more";
    public override string BinaryDefault => "lux";
    public override string InstallHint => "scoop install lux";

    [GeneratedRegex(@"([\d.]+)%")]
    private static partial Regex PercentRegex();

    [GeneratedRegex(@"([\d.]+\s*\w+/s)", RegexOptions.IgnoreCase)]
    private static partial Regex SpeedRegex();

    public override string BinaryPath(AppSettings settings) => PickPath(settings.LuxPath, BinaryDefault);

    public override bool CanHandle(string url) => HandledSites.Any(url.Contains);

    public override async Task<MediaInfo> GetInfoAsync(string url, AppSettings settings)
    {
        var result = await ProcessRunner.CaptureAsync(BinaryPath(settings), "-j", url);
        if (!result.Success)
            throw new DownloadException($"lux failed: {YtDlp.LastLine(result.Stderr)}");

        using var json = JsonDocument.Parse(result.Stdout);
        var root = json.RootElement.ValueKind == JsonValueKind.Array && json.RootElement.GetArrayLength() > 0
            ? json.RootElement[0]
            : json.RootElement;

        var formats = new List<MediaFormat>();
        if (root.TryGetProperty("streams", out var streams) && streams.ValueKind == JsonValueKind.Object)
        {
            foreach (var stream in streams.EnumerateObject())
            {
                var s = stream.Value;
                var quality = s.TryGetProperty("quality", out var q) && q.ValueKind == JsonValueKind.String
                    ? q.GetString() ?? "unknown" : "unknown";
                formats.Add(new MediaFormat(
                    FormatId: stream.Name,
                    Ext: s.TryGetProperty("ext", out var e) && e.ValueKind == JsonValueKind.String
                        ? e.GetString() ?? "mp4" : "mp4",
                    Resolution: quality,
                    Filesize: s.TryGetProperty("size", out var size) && size.ValueKind == JsonValueKind.Number
                        ? (long)size.GetDouble() : null,
                    Note: quality));
            }
        }
        formats.Sort((a, b) => (b.Filesize ?? 0).CompareTo(a.Filesize ?? 0));

        return new MediaInfo
        {
            Title = root.TryGetProperty("title", out var t) && t.ValueKind == JsonValueKind.String
                ? t.GetString() ?? "Untitled" : "Untitled",
            Thumbnail = root.TryGetProperty("thumbnail", out var th) && th.ValueKind == JsonValueKind.String
                ? th.GetString() ?? "" : "",
            Uploader = root.TryGetProperty("author", out var a) && a.ValueKind == JsonValueKind.String
                ? a.GetString() ?? "" : "",
            Formats = formats,
            DownloaderId = Id,
        };
    }

    public override Command BuildArgs(string url, DownloadOptions options, AppSettings settings)
    {
        var args = new List<string> { "-o", options.DownloadFolder };

        if (!string.IsNullOrEmpty(options.FormatId) && options.FormatId != "best")
            args.AddRange(["-f", options.FormatId]);
        if (!string.IsNullOrWhiteSpace(settings.LuxCookie))
            args.AddRange(["-c", settings.LuxCookie]);
        if (settings.LuxMultiThread)
            args.Add("-m");

        args.Add(url);
        return new Command(BinaryPath(settings), args);
    }

    public override ProgressUpdate? ParseProgress(string line)
    {
        var pct = PercentRegex().Match(line);
        if (!pct.Success) return null;
        var speed = SpeedRegex().Match(line);
        return new ProgressUpdate(
            Percent: double.Parse(pct.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture),
            Speed: speed.Success ? speed.Groups[1].Value : "");
    }
}
