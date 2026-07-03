using Xunit;
using XtractForge.Core.Downloaders;

namespace XtractForge.Tests;

public class ProgressParsingTests
{
    [Fact]
    public void YtDlpProgressLine()
    {
        var update = new YtDlp().ParseProgress("[download]  42.5% of ~120.5MiB at 3.2MiB/s ETA 00:42");
        Assert.NotNull(update);
        Assert.Equal(42.5, update.Percent);
        Assert.Equal("120.5MiB", update.Size);
        Assert.Equal("3.2MiB/s", update.Speed);
        Assert.Equal("00:42", update.Eta);
    }

    [Fact]
    public void YtDlpProgressWithoutTilde()
    {
        var update = new YtDlp().ParseProgress("[download] 100.0% of 55.3MiB at 10.1MiB/s ETA 00:00");
        Assert.Equal(100.0, update?.Percent);
    }

    [Theory]
    [InlineData("[youtube] dQw4: Downloading webpage")]
    [InlineData("[download] Destination: video.mp4")]
    public void YtDlpIgnoresNonProgressLines(string line) =>
        Assert.Null(new YtDlp().ParseProgress(line));

    [Fact]
    public void LuxProgress()
    {
        var update = new Lux().ParseProgress(" 2.34 MiB / 10.00 MiB [====>-----] 23.40% 4.31 MiB/s");
        Assert.Equal(23.40, update?.Percent);
        Assert.Equal("4.31 MiB/s", update?.Speed);
    }

    [Fact]
    public void GalleryDlFileCount()
    {
        var update = new GalleryDl().ParseProgress("#12 https://i.pximg.net/img/a.png");
        Assert.Null(update?.Percent);
        Assert.Equal(12, update?.FileCount);
        Assert.Null(new GalleryDl().ParseProgress("no counter here"));
    }

    [Theory]
    [InlineData("Downloaded \"Artist - Song\"", 100)]
    [InlineData("Skipping Song (already exists)", 100)]
    [InlineData("Downloading Song", 50)]
    public void SpotDlHeuristics(string line, double expected) =>
        Assert.Equal(expected, new SpotDl().ParseProgress(line)?.Percent);

    [Fact]
    public void SpotDlIgnoresOtherLines() =>
        Assert.Null(new SpotDl().ParseProgress("Processing query"));

    [Fact]
    public void FFmpegProgress()
    {
        var update = new FFmpegTool().ParseProgress(
            "frame=  100 fps= 25 q=-1.0 size=  2048kB time=00:01:02.50 bitrate=1000.0kbits/s speed=1.02x");
        Assert.NotNull(update);
        Assert.Null(update.Percent);
        Assert.Equal("00:01:02", update.Size);
        Assert.Equal("1.02x", update.Speed);
        Assert.Null(new FFmpegTool().ParseProgress("Stream mapping:"));
    }

    [Theory]
    [InlineData(" 42  120M   42  50M    0     0  3319k      0  0:00:37  0:00:15  0:00:22 3800k", 42)]
    [InlineData("100  120M  100  120M    0     0  4000k      0  0:00:30  0:00:30 --:--:-- 4100k", 100)]
    public void CurlProgress(string line, double expected) =>
        Assert.Equal(expected, new Curl().ParseProgress(line)?.Percent);

    [Theory]
    [InlineData("curl: (6) Could not resolve host")]
    [InlineData("  % Total    % Received % Xferd")]
    public void CurlIgnoresNonProgressLines(string line) =>
        Assert.Null(new Curl().ParseProgress(line));
}
