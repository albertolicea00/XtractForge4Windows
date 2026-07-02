using System.Text.RegularExpressions;
using XtractForge.Core.Engine;
using XtractForge.Core.Models;

namespace XtractForge.Core.Downloaders;

/// <summary>Records HLS/DASH/RTMP/RTSP streams to a file, and converts local media files.</summary>
public sealed partial class FFmpegTool : DownloaderBase
{
    internal static readonly string[] MediaExts =
    [
        "mp4", "mkv", "webm", "mov", "avi", "flv", "ts", "m4v",
        "mp3", "m4a", "aac", "flac", "wav", "ogg", "opus",
    ];

    public override string Id => "ffmpeg";
    public override string Name => "FFmpeg";
    public override string Summary => "HLS/DASH/RTMP streams and local file conversion";
    public override string BinaryDefault => "ffmpeg";
    public override string InstallHint => "winget install Gyan.FFmpeg";

    [GeneratedRegex(@"\.(m3u8|mpd)(\?|#|$)|^rtmps?://|^rtsp://", RegexOptions.IgnoreCase)]
    internal static partial Regex StreamRegex();

    [GeneratedRegex(@"time=(\d{2}:\d{2}:\d{2})")]
    private static partial Regex TimeRegex();

    [GeneratedRegex(@"speed=\s*([\d.]+x)")]
    private static partial Regex SpeedRegex();

    [GeneratedRegex(@"ffmpeg version (\S+)", RegexOptions.IgnoreCase)]
    private static partial Regex VersionRegex();

    public override string BinaryPath(AppSettings settings) => PickPath(settings.FfmpegPath, BinaryDefault);

    public override async Task<DependencyStatus> CheckDependencyAsync(AppSettings settings)
    {
        var result = await ProcessRunner.CaptureAsync(BinaryPath(settings), "-version");
        var match = VersionRegex().Match(result.Stdout);
        var version = match.Success
            ? match.Groups[1].Value
            : result.Stdout.Split('\n').FirstOrDefault()?.Trim() ?? "";
        return new DependencyStatus(result.Success, version);
    }

    public override bool CanHandle(string url)
    {
        if (StreamRegex().IsMatch(url)) return true;
        if (UrlHelpers.IsLocalPath(url))
        {
            var ext = Path.GetExtension(UrlHelpers.LocalPath(url)).TrimStart('.').ToLowerInvariant();
            return MediaExts.Contains(ext);
        }
        return false;
    }

    public override Task<MediaInfo> GetInfoAsync(string url, AppSettings settings)
    {
        if (UrlHelpers.IsLocalPath(url))
        {
            var path = UrlHelpers.LocalPath(url);
            var baseName = Path.GetFileNameWithoutExtension(path);
            var ext = Path.GetExtension(path).TrimStart('.').ToLowerInvariant();
            string[] audioExts = ["mp3", "m4a", "wav"];

            return Task.FromResult(new MediaInfo
            {
                Title = baseName.Length == 0 ? Path.GetFileName(path) : baseName,
                Uploader = "Local File",
                DownloaderId = Id,
                OptionFields =
                [
                    new OptionField("action", "Action", OptionKind.Select, "convert",
                        ["convert", "extract_audio"],
                        Help: "Convert the video to another format, or extract its audio."),
                    new OptionField("container", "Output format", OptionKind.Select,
                        audioExts.Contains(ext) ? ext : "mp4",
                        ["mp4", "mkv", "mp3", "m4a", "wav"],
                        Help: "The file container/format to convert to."),
                    new OptionField("videoCodec", "Video codec", OptionKind.Select, "copy",
                        ["copy", "h264", "h265"],
                        Help: "\"copy\" is extremely fast as it does not re-encode."),
                    new OptionField("audioCodec", "Audio codec", OptionKind.Select, "copy",
                        ["copy", "aac", "mp3"],
                        Help: "Codec for the audio track."),
                ],
            });
        }

        return Task.FromResult(new MediaInfo
        {
            Title = NameFromStreamUrl(url),
            DownloaderId = Id,
            OptionFields =
            [
                new OptionField("container", "Output container", OptionKind.Select,
                    PickPath(settings.FfmpegContainer, "mp4"), ["mp4", "mkv", "ts"],
                    Help: "File format for the recorded stream. mp4 is the most compatible."),
            ],
        });
    }

    public override Command BuildArgs(string url, DownloadOptions options, AppSettings settings)
    {
        if (UrlHelpers.IsLocalPath(url))
        {
            var input = UrlHelpers.LocalPath(url);
            var action = options.PluginOptions.GetValueOrDefault("action", "convert");
            var container = options.PluginOptions.GetValueOrDefault("container", "mp4");
            var videoCodec = options.PluginOptions.GetValueOrDefault("videoCodec", "copy");
            var audioCodec = options.PluginOptions.GetValueOrDefault("audioCodec", "copy");

            var baseName = Path.GetFileNameWithoutExtension(input);
            var output = Path.Combine(options.DownloadFolder, $"{baseName}_converted.{container}");

            var args = new List<string> { "-y", "-stats", "-i", input };
            if (action == "extract_audio")
            {
                args.Add("-vn");
                args.AddRange(AudioCodecArgs(audioCodec));
            }
            else
            {
                args.AddRange(videoCodec switch
                {
                    "h264" => new[] { "-vcodec", "libx264" },
                    "h265" => ["-vcodec", "libx265"],
                    _ => ["-vcodec", "copy"],
                });
                args.AddRange(AudioCodecArgs(audioCodec));
            }
            args.Add(output);
            return new Command(BinaryPath(settings), args);
        }

        var streamContainer = options.PluginOptions.GetValueOrDefault(
            "container", PickPath(settings.FfmpegContainer, "mp4"));
        var streamOutput = Path.Combine(options.DownloadFolder,
            $"{NameFromStreamUrl(url)}.{streamContainer}");

        var streamArgs = new List<string> { "-y", "-stats", "-i", url, "-c", "copy" };
        if (streamContainer == "mp4")
            streamArgs.AddRange(["-bsf:a", "aac_adtstoasc"]);
        streamArgs.Add(streamOutput);
        return new Command(BinaryPath(settings), streamArgs);
    }

    public override ProgressUpdate? ParseProgress(string line)
    {
        var time = TimeRegex().Match(line);
        var speed = SpeedRegex().Match(line);
        if (!time.Success && !speed.Success) return null;
        return new ProgressUpdate(
            Size: time.Success ? time.Groups[1].Value : "",
            Speed: speed.Success ? speed.Groups[1].Value : "");
    }

    private static string[] AudioCodecArgs(string codec) => codec switch
    {
        "aac" => ["-acodec", "aac"],
        "mp3" => ["-acodec", "libmp3lame"],
        _ => ["-acodec", "copy"],
    };

    internal static string NameFromStreamUrl(string url)
    {
        var name = UrlHelpers.FilenameFromUrl(url, "stream");
        name = Regex.Replace(name, @"\.(m3u8|mpd)$", "", RegexOptions.IgnoreCase);
        return name.Length == 0 ? "stream" : name;
    }
}
