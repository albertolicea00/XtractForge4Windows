using XtractForge.Core.Models;

namespace XtractForge.Core.Downloaders;

/// <summary>Spotify tracks/albums/playlists as audio (via YouTube Music match).</summary>
public sealed class SpotDl : DownloaderBase
{
    public override string Id => "spotdl";
    public override string Name => "spotDL";
    public override string Summary => "Spotify tracks, albums, and playlists as audio";
    public override string BinaryDefault => "spotdl";
    public override string InstallHint => "pip install spotdl";

    public override string BinaryPath(AppSettings settings) => PickPath(settings.SpotdlPath, BinaryDefault);

    public override bool CanHandle(string url) =>
        url.Contains("open.spotify.com") || url.StartsWith("spotify:");

    public override Task<MediaInfo> GetInfoAsync(string url, AppSettings settings)
    {
        var contentType = "Track";
        if (url.Contains("/playlist/")) contentType = "Playlist";
        if (url.Contains("/album/")) contentType = "Album";
        if (url.Contains("/artist/")) contentType = "Artist discography";

        var fmt = PickPath(settings.SpotdlFormat, "mp3");
        var bitrate = PickPath(settings.SpotdlBitrate, "320k");

        return Task.FromResult(new MediaInfo
        {
            Title = $"Spotify {contentType}",
            Uploader = "Spotify",
            Formats =
            [
                new MediaFormat(fmt, fmt, bitrate,
                    Note: $"{fmt.ToUpperInvariant()} @ {bitrate} — downloaded via YouTube Music match",
                    Vcodec: "none"),
            ],
            DownloaderId = Id,
            SimpleDownload = true,
        });
    }

    public override Command BuildArgs(string url, DownloadOptions options, AppSettings settings)
    {
        var fmt = PickPath(settings.SpotdlFormat, "mp3");
        var bitrate = PickPath(settings.SpotdlBitrate, "320k");

        return new Command(BinaryPath(settings),
        [
            "download", url,
            "--output", Path.Combine(options.DownloadFolder, "{artist} - {title}.{output-ext}"),
            "--format", fmt,
            "--bitrate", bitrate,
        ]);
    }

    public override ProgressUpdate? ParseProgress(string line)
    {
        if (line.Contains("Downloaded") || line.Contains("Skipping"))
            return new ProgressUpdate(Percent: 100);
        if (line.Contains("Downloading"))
            return new ProgressUpdate(Percent: 50);
        return null;
    }
}
