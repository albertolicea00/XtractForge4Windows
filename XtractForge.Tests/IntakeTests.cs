using Xunit;
using XtractForge.Core.Engine;

namespace XtractForge.Tests;

public class IntakeTests
{
    [Fact]
    public void ExtractsHttpUrls() =>
        Assert.Equal(["https://youtu.be/x", "http://a.com/b.mp4"],
            Intake.ExtractUrls("check https://youtu.be/x and http://a.com/b.mp4 out"));

    [Fact]
    public void ExtractsMultilineAndDeduplicates()
    {
        var text = "https://youtu.be/x\nhttps://youtu.be/x\nhttps://open.spotify.com/track/y";
        Assert.Equal(["https://youtu.be/x", "https://open.spotify.com/track/y"],
            Intake.ExtractUrls(text));
    }

    [Theory]
    [InlineData("spotify:track:abc")]
    [InlineData("rtmp://live.x.com/key")]
    public void ExtractsSpecialSchemes(string url) =>
        Assert.Equal([url], Intake.ExtractUrls(url));

    [Fact]
    public void TrimsTrailingPunctuation() =>
        Assert.Equal(["https://a.com/v.mp4"], Intake.ExtractUrls("look: https://a.com/v.mp4."));

    [Fact]
    public void BareDomainWithPathGetsScheme() =>
        Assert.Equal(["https://youtube.com/watch?v=x"],
            Intake.ExtractUrls("youtube.com/watch?v=x"));

    [Theory]
    [InlineData("just some words. e.g nothing here")]
    [InlineData("")]
    public void PlainTextYieldsNothing(string text) =>
        Assert.Empty(Intake.ExtractUrls(text));
}
