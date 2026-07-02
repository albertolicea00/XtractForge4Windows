using System.Text.RegularExpressions;
using XtractForge.Core.Models;

namespace XtractForge.Core.Downloaders;

/// <summary>Image-gallery downloader: DeviantArt, Pixiv, Reddit, Instagram, and more.</summary>
public sealed partial class GalleryDl : DownloaderBase
{
    internal static readonly string[] HandledSites =
    [
        "deviantart.com", "pixiv.net", "danbooru.donmai.us", "artstation.com",
        "flickr.com", "reddit.com", "instagram.com", "twitter.com", "x.com",
        "tumblr.com", "gelbooru.com", "rule34.xxx", "sankakucomplex.com",
        "nijie.info", "seiga.nicovideo.jp", "pinterest.com", "patreon.com",
        "furaffinity.net", "e621.net", "newgrounds.com", "imgur.com",
    ];

    public override string Id => "gallery-dl";
    public override string Name => "gallery-dl";
    public override string Summary => "Image galleries: DeviantArt, Pixiv, Reddit, and more";
    public override string BinaryDefault => "gallery-dl";
    public override string InstallHint => "pip install gallery-dl";

    [GeneratedRegex(@"#(\d+)")]
    private static partial Regex CountRegex();

    public override string BinaryPath(AppSettings settings) =>
        PickPath(settings.GalleryDlPath, BinaryDefault);

    public override bool CanHandle(string url) => HandledSites.Any(url.Contains);

    public override Task<MediaInfo> GetInfoAsync(string url, AppSettings settings)
    {
        var slug = string.Join("/",
            Regex.Replace(url, @"^https?://", "").TrimEnd('/').Split('/').TakeLast(2));
        var site = HandledSites.FirstOrDefault(url.Contains) ?? "gallery";

        return Task.FromResult(new MediaInfo
        {
            Title = slug.Length == 0 ? "Gallery Download" : slug,
            Uploader = site,
            Formats =
            [
                new MediaFormat("original", "images", "Original Quality",
                    Note: "All images — gallery-dl downloads every item in the gallery",
                    Vcodec: "none"),
            ],
            DownloaderId = Id,
            IsGallery = true,
            SimpleDownload = true,
        });
    }

    public override Command BuildArgs(string url, DownloadOptions options, AppSettings settings)
    {
        var args = new List<string> { "-d", options.DownloadFolder };

        if (!string.IsNullOrWhiteSpace(settings.GalleryDlCookies))
            args.AddRange(["--cookies", settings.GalleryDlCookies]);
        if (!string.IsNullOrWhiteSpace(settings.GalleryDlConfig))
            args.AddRange(["--config", settings.GalleryDlConfig]);

        args.Add(url);
        return new Command(BinaryPath(settings), args);
    }

    public override ProgressUpdate? ParseProgress(string line)
    {
        var match = CountRegex().Match(line);
        if (!match.Success) return null;
        return new ProgressUpdate(FileCount: int.Parse(match.Groups[1].Value));
    }
}
