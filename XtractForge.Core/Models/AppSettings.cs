using System.Text.Json.Serialization;

namespace XtractForge.Core.Models;

[JsonConverter(typeof(JsonStringEnumConverter<Organize>))]
public enum Organize { None, Type, Source }

[JsonConverter(typeof(JsonStringEnumConverter<AppearanceSetting>))]
public enum AppearanceSetting { System, Light, Dark }

/// <summary>
/// Every user setting, persisted as JSON in %LOCALAPPDATA%\XtractForge\config.json
/// (see SettingsService in the app project) and passed into downloaders by reference.
/// </summary>
public sealed class AppSettings
{
    // General
    public string DownloadFolder { get; set; } = DefaultDownloadsFolder();
    public string SpeedLimit { get; set; } = "";
    public bool StageToTemp { get; set; } = true;
    public Organize Organize { get; set; } = Organize.None;
    public bool WatchClipboard { get; set; }
    public AppearanceSetting Appearance { get; set; } = AppearanceSetting.System;
    public List<string> DisabledDownloaders { get; set; } = [];

    // yt-dlp
    public string YtdlpPath { get; set; } = "yt-dlp";
    public bool EmbedSubtitles { get; set; }
    public bool SponsorBlock { get; set; }

    // ffmpeg
    public string FfmpegPath { get; set; } = "ffmpeg";
    public string FfmpegContainer { get; set; } = "mp4";

    // lux
    public string LuxPath { get; set; } = "lux";
    public string LuxCookie { get; set; } = "";
    public bool LuxMultiThread { get; set; }

    // gallery-dl
    public string GalleryDlPath { get; set; } = "gallery-dl";
    public string GalleryDlCookies { get; set; } = "";
    public string GalleryDlConfig { get; set; } = "";

    // spotdl
    public string SpotdlPath { get; set; } = "spotdl";
    public string SpotdlFormat { get; set; } = "mp3";
    public string SpotdlBitrate { get; set; } = "320k";

    // curl
    public string CurlPath { get; set; } = "curl";

    public static string DefaultDownloadsFolder()
    {
        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var downloads = Path.Combine(profile, "Downloads");
        return Directory.Exists(downloads)
            ? downloads
            : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
    }
}
