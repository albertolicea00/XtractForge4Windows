using Xunit;
using XtractForge.Core.Engine;
using XtractForge.Core.Models;

namespace XtractForge.Tests;

public class StagingTests : IDisposable
{
    private readonly string _tempRoot =
        Path.Combine(Path.GetTempPath(), $"xf-tests-{Guid.NewGuid():N}");

    public StagingTests() => Directory.CreateDirectory(_tempRoot);

    public void Dispose()
    {
        try { Directory.Delete(_tempRoot, recursive: true); } catch (IOException) { }
    }

    [Fact]
    public void UrlHashIsStableAndShort()
    {
        Assert.Equal(Staging.UrlHash("https://youtu.be/x"), Staging.UrlHash("https://youtu.be/x"));
        Assert.NotEqual(Staging.UrlHash("https://youtu.be/x"), Staging.UrlHash("https://youtu.be/y"));
        Assert.Equal(16, Staging.UrlHash("https://youtu.be/x").Length);
    }

    [Fact]
    public void StagingDirLayout()
    {
        var dir = Staging.StagingDir("https://youtu.be/x", _tempRoot);
        Assert.Equal(Staging.TempDirName, Path.GetFileName(Path.GetDirectoryName(dir)));
        Assert.StartsWith(_tempRoot, dir);
    }

    [Theory]
    [InlineData("mp4", "Video")]
    [InlineData(".MP3", "Audio")]
    [InlineData("png", "Images")]
    [InlineData("zip", "Files")]
    [InlineData("", "Files")]
    public void Categories(string ext, string expected) =>
        Assert.Equal(expected, Staging.Category(ext));

    [Fact]
    public void FinalizeMovesFilesAndCleansUp()
    {
        var staging = Staging.StagingDir("https://example.com/v", _tempRoot);
        Directory.CreateDirectory(staging);
        File.WriteAllText(Path.Combine(staging, "movie.mp4"), "video");

        var moved = Staging.Finalize(staging, _tempRoot, Organize.None, "example.com");

        Assert.Single(moved);
        Assert.True(File.Exists(Path.Combine(_tempRoot, "movie.mp4")));
        Assert.False(Directory.Exists(staging));
        Assert.False(Directory.Exists(Path.Combine(_tempRoot, Staging.TempDirName)));
    }

    [Fact]
    public void FinalizeOrganizesByType()
    {
        var staging = Staging.StagingDir("u", _tempRoot);
        Directory.CreateDirectory(staging);
        File.WriteAllText(Path.Combine(staging, "a.mp4"), "");
        File.WriteAllText(Path.Combine(staging, "b.mp3"), "");

        var moved = Staging.Finalize(staging, _tempRoot, Organize.Type, "x");

        var folders = moved.Select(m => Path.GetFileName(Path.GetDirectoryName(m))!).ToHashSet();
        Assert.True(folders.SetEquals(["Video", "Audio"]));
    }

    [Fact]
    public void FinalizeOrganizesBySource()
    {
        var staging = Staging.StagingDir("u", _tempRoot);
        Directory.CreateDirectory(staging);
        File.WriteAllText(Path.Combine(staging, "a.mp4"), "");

        var moved = Staging.Finalize(staging, _tempRoot, Organize.Source, "youtube.com");
        Assert.Equal("youtube.com", Path.GetFileName(Path.GetDirectoryName(moved[0])));
    }

    [Fact]
    public void FinalizeNeverOverwrites()
    {
        var staging = Staging.StagingDir("u", _tempRoot);
        Directory.CreateDirectory(staging);
        File.WriteAllText(Path.Combine(staging, "movie.mp4"), "new");
        File.WriteAllText(Path.Combine(_tempRoot, "movie.mp4"), "old");

        var moved = Staging.Finalize(staging, _tempRoot, Organize.None, "x");

        Assert.Equal("movie (2).mp4", Path.GetFileName(moved[0]));
        Assert.Equal("old", File.ReadAllText(Path.Combine(_tempRoot, "movie.mp4")));
    }
}
