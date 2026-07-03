using Xunit;
using XtractForge.Core.Downloaders;

namespace XtractForge.Tests;

public class RoutingTests
{
    [Theory]
    [InlineData("https://open.spotify.com/track/abc", "spotdl")]
    [InlineData("spotify:track:abc", "spotdl")]
    [InlineData("https://www.pixiv.net/en/artworks/1", "gallery-dl")]
    [InlineData("https://www.instagram.com/p/xyz/", "gallery-dl")]
    [InlineData("https://x.com/user/status/1", "gallery-dl")]
    [InlineData("https://www.bilibili.com/video/BV1x", "lux")]
    [InlineData("https://v.douyin.com/abc/", "lux")]
    [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXcQ", "yt-dlp")] // deviation: yt-dlp, not lux
    [InlineData("https://youtu.be/dQw4w9WgXcQ", "yt-dlp")]
    [InlineData("https://cdn.example.com/live/index.m3u8", "ffmpeg")]
    [InlineData("https://cdn.example.com/x.m3u8?token=1", "ffmpeg")]
    [InlineData("rtmp://live.example.com/app/key", "ffmpeg")]
    [InlineData(@"C:\Videos\clip.mp4", "ffmpeg")]
    [InlineData("https://example.com/file.zip", "curl")]
    [InlineData("https://example.com/song.mp3?ref=1", "curl")]
    [InlineData("https://random-video-site.example/watch/1", "yt-dlp")]
    public void RoutesToExpectedDownloader(string url, string expectedId) =>
        Assert.Equal(expectedId, DownloaderRegistry.Route(url)?.Id);

    [Fact]
    public void StreamsNeverRouteToCurl() =>
        Assert.NotEqual("curl", DownloaderRegistry.Route("https://example.com/live.m3u8")?.Id);

    [Fact]
    public void DisabledDownloaderIsSkipped() =>
        Assert.Equal("yt-dlp",
            DownloaderRegistry.Route("https://www.instagram.com/p/xyz/", ["gallery-dl"])?.Id);

    [Fact]
    public void AllDisabledReturnsNull()
    {
        var allIds = DownloaderRegistry.All.Select(d => d.Id).ToList();
        Assert.Null(DownloaderRegistry.Route("https://example.com/file.zip", allIds));
    }

    [Fact]
    public void RegistryOrderIsMostSpecificFirst() =>
        Assert.Equal(["spotdl", "gallery-dl", "lux", "ffmpeg", "curl", "yt-dlp"],
            DownloaderRegistry.All.Select(d => d.Id).ToArray());
}
