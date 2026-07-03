using Xunit;
using XtractForge.Core.Downloaders;
using XtractForge.Core.Models;

namespace XtractForge.Tests;

public class BuildArgsTests
{
    private static readonly string Folder = Path.Combine("tmp", "dl");

    private static DownloadOptions Options(
        Dictionary<string, string>? pluginOptions = null, string? formatId = null,
        bool audioOnly = false, bool isPlaylist = false, bool resume = false) => new()
    {
        DownloadFolder = Folder,
        FormatId = formatId,
        AudioOnly = audioOnly,
        IsPlaylist = isPlaylist,
        Resume = resume,
        PluginOptions = pluginOptions ?? [],
    };

    [Fact]
    public void YtDlpDefaults()
    {
        var cmd = new YtDlp().BuildArgs("https://youtu.be/x", Options(), new AppSettings());
        Assert.Equal("yt-dlp", cmd.Binary);
        Assert.Equal(["-o", Path.Combine(Folder, "%(title)s.%(ext)s"),
                      "-f", "bestvideo+bestaudio/best", "https://youtu.be/x"], cmd.Args);
    }

    [Fact]
    public void YtDlpPlaylistTemplate()
    {
        var cmd = new YtDlp().BuildArgs("u", Options(isPlaylist: true), new AppSettings());
        Assert.Contains(
            Path.Combine(Folder, "%(playlist_title)s", "%(playlist_index)s - %(title)s.%(ext)s"),
            cmd.Args);
    }

    [Fact]
    public void YtDlpAudioOnly()
    {
        var cmd = new YtDlp().BuildArgs("u", Options(audioOnly: true), new AppSettings());
        Assert.Contains("-x", cmd.Args);
        var args = cmd.Args.ToList();
        Assert.Equal("mp3", args[args.IndexOf("--audio-format") + 1]);
        Assert.DoesNotContain("-f", cmd.Args);
    }

    [Fact]
    public void YtDlpSettingsFlags()
    {
        var settings = new AppSettings
        {
            SpeedLimit = "5M",
            EmbedSubtitles = true,
            SponsorBlock = true,
        };
        var cmd = new YtDlp().BuildArgs("u", Options(resume: true), settings);
        var args = cmd.Args.ToList();
        Assert.Equal("5M", args[args.IndexOf("-r") + 1]);
        Assert.Contains("--embed-subs", cmd.Args);
        Assert.Contains("--all-subs", cmd.Args);
        Assert.Equal("all", args[args.IndexOf("--sponsorblock-remove") + 1]);
        Assert.Contains("-c", cmd.Args);
        Assert.Equal("u", args[^1]);
    }

    [Fact]
    public void LuxAllOptions()
    {
        var settings = new AppSettings { LuxCookie = "SESSDATA=abc", LuxMultiThread = true };
        var cmd = new Lux().BuildArgs("u", Options(formatId: "dash-flv"), settings);
        Assert.Equal(["-o", Folder, "-f", "dash-flv", "-c", "SESSDATA=abc", "-m", "u"], cmd.Args);
    }

    [Fact]
    public void LuxBestFormatIsOmitted()
    {
        var cmd = new Lux().BuildArgs("u", Options(formatId: "best"), new AppSettings());
        Assert.DoesNotContain("-f", cmd.Args);
    }

    [Fact]
    public void GalleryDlArgs()
    {
        var settings = new AppSettings { GalleryDlCookies = "c.txt", GalleryDlConfig = "g.conf" };
        var cmd = new GalleryDl().BuildArgs("u", Options(), settings);
        Assert.Equal(["-d", Folder, "--cookies", "c.txt", "--config", "g.conf", "u"], cmd.Args);
    }

    [Fact]
    public void SpotDlArgs()
    {
        var cmd = new SpotDl().BuildArgs("https://open.spotify.com/track/x",
                                         Options(), new AppSettings());
        Assert.Equal(["download", "https://open.spotify.com/track/x",
                      "--output", Path.Combine(Folder, "{artist} - {title}.{output-ext}"),
                      "--format", "mp3", "--bitrate", "320k"], cmd.Args);
    }

    [Fact]
    public void FFmpegStreamRecording()
    {
        var cmd = new FFmpegTool().BuildArgs("https://cdn.x.com/live/master.m3u8",
                                             Options(), new AppSettings());
        Assert.Equal(["-y", "-stats", "-i", "https://cdn.x.com/live/master.m3u8",
                      "-c", "copy", "-bsf:a", "aac_adtstoasc",
                      Path.Combine(Folder, "master.mp4")], cmd.Args);
    }

    [Fact]
    public void FFmpegMkvSkipsBitstreamFilter()
    {
        var cmd = new FFmpegTool().BuildArgs("https://x.com/s.m3u8",
            Options(new() { ["container"] = "mkv" }), new AppSettings());
        Assert.DoesNotContain("-bsf:a", cmd.Args);
        Assert.Equal(Path.Combine(Folder, "s.mkv"), cmd.Args[^1]);
    }

    [Fact]
    public void FFmpegLocalConvertCopy()
    {
        var input = OperatingSystem.IsWindows() ? @"C:\v\clip.mov" : "/v/clip.mov";
        var cmd = new FFmpegTool().BuildArgs(input, Options(), new AppSettings());
        Assert.Equal(["-y", "-stats", "-i", input, "-vcodec", "copy", "-acodec", "copy",
                      Path.Combine(Folder, "clip_converted.mp4")], cmd.Args);
    }

    [Fact]
    public void FFmpegExtractAudioMp3()
    {
        var input = OperatingSystem.IsWindows() ? @"C:\v\clip.mp4" : "/v/clip.mp4";
        var cmd = new FFmpegTool().BuildArgs(input, Options(new()
        {
            ["action"] = "extract_audio",
            ["container"] = "mp3",
            ["audioCodec"] = "mp3",
        }), new AppSettings());
        Assert.Contains("-vn", cmd.Args);
        var args = cmd.Args.ToList();
        Assert.Equal("libmp3lame", args[args.IndexOf("-acodec") + 1]);
        Assert.Equal(Path.Combine(Folder, "clip_converted.mp3"), cmd.Args[^1]);
    }

    [Fact]
    public void CurlDefaults()
    {
        var cmd = new Curl().BuildArgs("https://example.com/file.zip", Options(), new AppSettings());
        Assert.Equal(["-L", "--create-dirs", "-o", Path.Combine(Folder, "file.zip"),
                      "https://example.com/file.zip"], cmd.Args);
    }

    [Fact]
    public void CurlResumeSpeedLimitAndRename()
    {
        var settings = new AppSettings { SpeedLimit = "1M" };
        var cmd = new Curl().BuildArgs("https://example.com/file.zip",
            Options(new() { ["filename"] = "renamed.zip" }, resume: true), settings);
        Assert.Equal(["-L", "--create-dirs", "-C", "-",
                      "-o", Path.Combine(Folder, "renamed.zip"),
                      "https://example.com/file.zip", "--limit-rate", "1M"], cmd.Args);
    }
}
