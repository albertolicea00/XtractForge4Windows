using System.Text.RegularExpressions;

namespace XtractForge.Core.Engine;

/// <summary>
/// URL extraction for everything that enters the app (drop, paste, and — later —
/// the xtractforge:// protocol). Pure logic; IntakeService in the app project
/// feeds results into the DownloadManager.
/// </summary>
public static partial class Intake
{
    [GeneratedRegex(@"^([a-z0-9-]+\.)+[a-z]{2,}(/\S*)?$", RegexOptions.IgnoreCase)]
    private static partial Regex BareDomainRegex();

    /// <summary>
    /// Extract downloadable targets from arbitrary text: http(s) links,
    /// spotify: URIs, rtmp/rtsp streams, and absolute local media paths.
    /// </summary>
    public static List<string> ExtractUrls(string text)
    {
        var found = new List<string>();
        var seen = new HashSet<string>();

        void Add(string s)
        {
            var trimmed = s.TrimEnd('.', ',', ';', ')', ']', '}', '>', '"', '\'');
            if (trimmed.Length == 0 || !seen.Add(trimmed)) return;
            found.Add(trimmed);
        }

        var tokens = text.Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        foreach (var token in tokens)
        {
            if (token.StartsWith("http://") || token.StartsWith("https://"))
                Add(token);
            else if (token.StartsWith("spotify:") && token.Length > "spotify:".Length)
                Add(token);
            else if (token.StartsWith("rtmp://") || token.StartsWith("rtmps://") || token.StartsWith("rtsp://"))
                Add(token);
            else if (token.StartsWith("file://"))
                Add(token);
            else if (IsAbsoluteLocalPath(token) && (File.Exists(token) || Directory.Exists(token)))
                Add(token);
            else if (!token.Contains("://") && LooksLikeBareDomainUrl(token))
                Add("https://" + token);
        }
        return found;
    }

    private static bool IsAbsoluteLocalPath(string token) =>
        token.StartsWith('/')
        || (token.Length > 2 && char.IsAsciiLetter(token[0]) && token[1] == ':' && token[2] == '\\');

    /// <summary>"youtube.com/watch?v=x" style input without a scheme.</summary>
    private static bool LooksLikeBareDomainUrl(string token)
    {
        if (!BareDomainRegex().IsMatch(token)) return false;
        // Require a path or www. to avoid swallowing plain words like "e.g".
        return token.Contains('/') || token.Contains("www.");
    }
}
