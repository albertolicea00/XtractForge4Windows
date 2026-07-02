using System.Text.Json;
using System.Text.RegularExpressions;
using XtractForge.Core.Engine;
using XtractForge.Core.Models;

namespace XtractForge.Core.Downloaders;

/// <summary>Catch-all engine: YouTube, Vimeo, TikTok, Twitter/X, and 1000+ sites.</summary>
public sealed partial class YtDlp : DownloaderBase
{
    public override string Id => "yt-dlp";
    public override string Name => "yt-dlp";
    public override string Summary => "YouTube, Vimeo, TikTok, and 1000+ sites";
    public override string BinaryDefault => "yt-dlp";
    public override string InstallHint => "winget install yt-dlp.yt-dlp";
    public override bool SupportsResume => true;

    [GeneratedRegex(@"\[download\]\s+([\d.]+)% of\s+(?:~\s*)?([\d.]+\w+) at\s+([\d.]+\w+/s) ETA\s+([\d:]+)")]
    private static partial Regex ProgressRegex();

    public override string BinaryPath(AppSettings settings) => PickPath(settings.YtdlpPath, BinaryDefault);

    public override bool CanHandle(string url) => true;

    public override async Task<MediaInfo> GetInfoAsync(string url, AppSettings settings)
    {
        var result = await ProcessRunner.CaptureAsync(
            BinaryPath(settings), "--dump-single-json", "--flat-playlist", url);
        if (!result.Success)
            throw new DownloadException($"yt-dlp failed: {LastLine(result.Stderr)}");

        using var json = JsonDocument.Parse(result.Stdout);
        var root = json.RootElement;

        var entryCount = root.TryGetProperty("entries", out var entries)
            && entries.ValueKind == JsonValueKind.Array ? entries.GetArrayLength() : 0;
        var isPlaylist = entryCount > 0
            || (root.TryGetProperty("_type", out var type) && type.GetString() == "playlist");

        var thumbnail = GetString(root, "thumbnail");
        if (thumbnail.Length == 0
            && root.TryGetProperty("thumbnails", out var thumbs)
            && thumbs.ValueKind == JsonValueKind.Array && thumbs.GetArrayLength() > 0)
        {
            thumbnail = GetString(thumbs[thumbs.GetArrayLength() - 1], "url");
        }

        var formats = new List<MediaFormat>();
        if (root.TryGetProperty("formats", out var rawFormats) && rawFormats.ValueKind == JsonValueKind.Array)
        {
            foreach (var f in rawFormats.EnumerateArray())
            {
                formats.Add(new MediaFormat(
                    FormatId: GetString(f, "format_id"),
                    Ext: GetString(f, "ext"),
                    Resolution: GetString(f, "resolution"),
                    Filesize: GetLong(f, "filesize") ?? GetLong(f, "filesize_approx"),
                    Fps: GetDouble(f, "fps"),
                    Note: GetString(f, "format_note"),
                    Vcodec: GetString(f, "vcodec")));
            }
        }

        return new MediaInfo
        {
            Title = GetString(root, "title", "Untitled"),
            Thumbnail = thumbnail,
            Duration = GetDouble(root, "duration") ?? 0,
            Uploader = GetString(root, "uploader", GetString(root, "channel")),
            Formats = formats,
            DownloaderId = Id,
            IsPlaylist = isPlaylist,
            EntryCount = entryCount,
        };
    }

    public override Command BuildArgs(string url, DownloadOptions options, AppSettings settings)
    {
        var args = new List<string>();

        var template = options.IsPlaylist
            ? Path.Combine("%(playlist_title)s", "%(playlist_index)s - %(title)s.%(ext)s")
            : "%(title)s.%(ext)s";
        args.AddRange(["-o", Path.Combine(options.DownloadFolder, template)]);

        if (!string.IsNullOrWhiteSpace(settings.SpeedLimit))
            args.AddRange(["-r", settings.SpeedLimit]);

        if (!string.IsNullOrEmpty(options.FormatId))
            args.AddRange(["-f", options.FormatId]);
        else if (options.AudioOnly)
            args.AddRange(["-x", "--audio-format", options.AudioFormat]);
        else
            args.AddRange(["-f", "bestvideo+bestaudio/best"]);

        if (settings.EmbedSubtitles)
            args.AddRange(["--embed-subs", "--all-subs"]);
        if (settings.SponsorBlock)
            args.AddRange(["--sponsorblock-remove", "all"]);
        if (options.Resume)
            args.Add("-c");

        args.Add(url);
        return new Command(BinaryPath(settings), args);
    }

    public override ProgressUpdate? ParseProgress(string line)
    {
        var match = ProgressRegex().Match(line);
        if (!match.Success) return null;
        return new ProgressUpdate(
            Percent: double.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture),
            Size: match.Groups[2].Value,
            Speed: match.Groups[3].Value,
            Eta: match.Groups[4].Value);
    }

    internal static string LastLine(string text) =>
        text.Trim().Split('\n').LastOrDefault()?.Trim() ?? "";

    private static string GetString(JsonElement el, string prop, string fallback = "") =>
        el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString() ?? fallback : fallback;

    private static long? GetLong(JsonElement el, string prop) =>
        el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.Number
            ? (long)v.GetDouble() : null;

    private static double? GetDouble(JsonElement el, string prop) =>
        el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.Number
            ? v.GetDouble() : null;
}
