using System.Security.Cryptography;
using System.Text;
using XtractForge.Core.Models;

namespace XtractForge.Core.Engine;

/// <summary>
/// Download staging: tools write into a hidden temp dir; on success files move
/// to the final folder (applying organize); on failure the temp dir stays so
/// the tool can resume later.
/// </summary>
public static class Staging
{
    public const string TempDirName = ".xtractforge-tmp";

    private static readonly HashSet<string> VideoExts =
        ["mp4", "mkv", "webm", "mov", "avi", "flv", "ts", "m4v"];
    private static readonly HashSet<string> AudioExts =
        ["mp3", "m4a", "aac", "flac", "wav", "ogg", "opus"];
    private static readonly HashSet<string> ImageExts =
        ["jpg", "jpeg", "png", "gif", "webp", "bmp", "tiff", "heic"];

    public static string UrlHash(string url)
    {
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(url));
        return Convert.ToHexString(digest.AsSpan(0, 8)).ToLowerInvariant();
    }

    /// <summary>&lt;downloadFolder&gt;\.xtractforge-tmp\&lt;urlHash&gt;\</summary>
    public static string StagingDir(string url, string downloadFolder) =>
        Path.Combine(downloadFolder, TempDirName, UrlHash(url));

    public static string Category(string extension)
    {
        var ext = extension.TrimStart('.').ToLowerInvariant();
        if (VideoExts.Contains(ext)) return "Video";
        if (AudioExts.Contains(ext)) return "Audio";
        if (ImageExts.Contains(ext)) return "Images";
        return "Files";
    }

    /// <summary>Destination folder for a file, applying the organize mode.</summary>
    public static string DestinationFolder(string finalFolder, Organize organize,
                                           string fileExtension, string source) =>
        organize switch
        {
            Organize.Type => Path.Combine(finalFolder, Category(fileExtension)),
            Organize.Source => Path.Combine(finalFolder, source),
            _ => finalFolder,
        };

    /// <summary>
    /// Move everything out of the staging dir into the final folder (applying
    /// organize), delete the staging dir, and return the moved paths.
    /// </summary>
    public static List<string> Finalize(string stagingDir, string finalFolder,
                                        Organize organize, string source)
    {
        var moved = new List<string>();
        if (!Directory.Exists(stagingDir)) return moved;

        foreach (var entry in Directory.EnumerateFileSystemEntries(stagingDir))
        {
            var name = Path.GetFileName(entry);
            if (name.StartsWith('.')) continue;

            var ext = Path.GetExtension(entry);
            var destFolder = DestinationFolder(finalFolder, organize, ext, source);
            Directory.CreateDirectory(destFolder);

            // Never overwrite: append " (n)" like Explorer does.
            var dest = Path.Combine(destFolder, name);
            var baseName = Path.GetFileNameWithoutExtension(name);
            var counter = 1;
            while (File.Exists(dest) || Directory.Exists(dest))
            {
                counter++;
                dest = Path.Combine(destFolder, $"{baseName} ({counter}){ext}");
            }

            if (Directory.Exists(entry))
                Directory.Move(entry, dest);
            else
                File.Move(entry, dest);
            moved.Add(dest);
        }

        try
        {
            Directory.Delete(stagingDir, recursive: true);
            // Remove the parent .xtractforge-tmp dir when it's now empty.
            var parent = Path.GetDirectoryName(stagingDir);
            if (parent is not null
                && Path.GetFileName(parent) == TempDirName
                && !Directory.EnumerateFileSystemEntries(parent).Any())
            {
                Directory.Delete(parent);
            }
        }
        catch (IOException) { /* best effort */ }

        return moved;
    }
}
