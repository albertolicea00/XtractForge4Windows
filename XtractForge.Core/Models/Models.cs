namespace XtractForge.Core.Models;

/// <summary>A resolved child-process invocation: binary + arguments.</summary>
public sealed record Command(string Binary, IReadOnlyList<string> Args);

public sealed record DependencyStatus(bool Available, string Version);

public sealed record MediaFormat(
    string FormatId,
    string Ext = "",
    string Resolution = "",
    long? Filesize = null,
    double? Fps = null,
    string Note = "",
    string Vcodec = "");

public enum OptionKind { Text, Toggle, Select }

/// <summary>
/// Declarative per-download option rendered by the options dialog
/// (the old `_downloadOptions` idea, strongly typed). Toggles use "true"/"false".
/// </summary>
public sealed record OptionField(
    string Key,
    string Label,
    OptionKind Kind,
    string DefaultValue,
    IReadOnlyList<string>? Options = null,
    string Placeholder = "",
    string Help = "");

public sealed class MediaInfo
{
    public string Title { get; init; } = "";
    public string Thumbnail { get; init; } = "";
    public double Duration { get; init; }
    public string Uploader { get; init; } = "";
    public IReadOnlyList<MediaFormat> Formats { get; init; } = [];
    public required string DownloaderId { get; init; }
    public bool IsPlaylist { get; init; }
    public int EntryCount { get; init; }
    public bool IsGallery { get; init; }
    /// <summary>Extra fields the options dialog renders; values land in <see cref="DownloadOptions.PluginOptions"/>.</summary>
    public IReadOnlyList<OptionField> OptionFields { get; init; } = [];
    /// <summary>True → skip the options dialog entirely and download directly.</summary>
    public bool SimpleDownload { get; init; }
}

public sealed class DownloadOptions
{
    /// <summary>Folder the tool writes into (the staging dir when staging is on).</summary>
    public required string DownloadFolder { get; init; }
    public string? FormatId { get; init; }
    public bool AudioOnly { get; init; }
    public string AudioFormat { get; init; } = "mp3";
    public bool IsPlaylist { get; init; }
    /// <summary>Set when resuming a paused/failed download (adds the tool's continue flag).</summary>
    public bool Resume { get; init; }
    /// <summary>Values collected from <see cref="MediaInfo.OptionFields"/> ("true"/"false" for toggles).</summary>
    public IReadOnlyDictionary<string, string> PluginOptions { get; init; } =
        new Dictionary<string, string>();
}

public sealed record ProgressUpdate(
    double? Percent = null,
    string Size = "",
    string Speed = "",
    string Eta = "",
    int? FileCount = null);

public class DownloadException(string message) : Exception(message);
