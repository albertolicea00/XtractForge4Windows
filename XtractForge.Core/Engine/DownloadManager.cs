using XtractForge.Core.Downloaders;
using XtractForge.Core.Models;

namespace XtractForge.Core.Engine;

public enum DownloadState
{
    FetchingInfo,
    AwaitingOptions,
    Downloading,
    Paused,
    Completed,
    Failed,
    Cancelled,
}

public sealed class DownloadItem
{
    public Guid Id { get; } = Guid.NewGuid();
    public required string Url { get; init; }
    public required string DownloaderId { get; init; }

    public string Title { get; set; } = "";
    public DownloadState State { get; set; } = DownloadState.FetchingInfo;
    public string FailureReason { get; set; } = "";
    public MediaInfo? Info { get; set; }
    public ProgressUpdate Progress { get; set; } = new();
    /// <summary>Final location after a completed download.</summary>
    public string? Destination { get; set; }
    /// <summary>Last few output lines, for error reporting.</summary>
    public List<string> RecentLines { get; } = [];
    /// <summary>Options chosen in the dialog, kept so pause→resume can rebuild args.</summary>
    public Dictionary<string, string> ChosenPluginOptions { get; set; } = [];
    public string? ChosenFormatId { get; set; }
    public bool ChosenAudioOnly { get; set; }

    public IDownloader? Downloader => DownloaderRegistry.ById(DownloaderId);
    public bool SupportsPause => Downloader?.SupportsResume ?? false;
}

/// <summary>
/// Single source of truth for the download queue. UI-framework-free: raises
/// plain events; the WinUI layer marshals them onto the DispatcherQueue.
/// </summary>
public sealed class DownloadManager(Func<AppSettings> settingsProvider)
{
    private readonly List<DownloadItem> _items = [];
    private readonly Dictionary<Guid, RunningProcess> _running = [];
    private readonly Dictionary<Guid, long> _lastProgressTick = [];
    private readonly object _lock = new();

    public event Action<DownloadItem>? ItemAdded;
    public event Action<DownloadItem>? ItemRemoved;
    /// <summary>State transition (not raw progress).</summary>
    public event Action<DownloadItem>? StateChanged;
    /// <summary>Throttled progress (~10/s per item).</summary>
    public event Action<DownloadItem>? ProgressChanged;

    public IReadOnlyList<DownloadItem> Items
    {
        get { lock (_lock) return _items.ToList(); }
    }

    public int ActiveCount
    {
        get
        {
            lock (_lock)
                return _items.Count(i => i.State is DownloadState.Downloading or DownloadState.FetchingInfo);
        }
    }

    // MARK: intake

    /// <summary>
    /// Entry point for every URL. Routes, fetches info, then either starts
    /// directly (simple downloads) or waits for the options dialog
    /// (AwaitingOptions → the UI calls Start).
    /// </summary>
    public async Task SubmitAsync(string url)
    {
        var settings = settingsProvider();
        var downloader = DownloaderRegistry.Route(url, settings.DisabledDownloaders);

        DownloadItem item;
        if (downloader is null)
        {
            item = new DownloadItem { Url = url, DownloaderId = "yt-dlp", Title = url };
            item.State = DownloadState.Failed;
            item.FailureReason = "No enabled downloader can handle this URL";
            AddItem(item);
            return;
        }

        item = new DownloadItem { Url = url, DownloaderId = downloader.Id, Title = url };
        AddItem(item);

        try
        {
            var info = await downloader.GetInfoAsync(url, settings).ConfigureAwait(false);
            item.Info = info;
            item.Title = info.Title;
            if (info.SimpleDownload || (info.Formats.Count == 0 && info.OptionFields.Count == 0))
            {
                Start(item, [], formatId: null, audioOnly: false);
            }
            else
            {
                item.State = DownloadState.AwaitingOptions;
                StateChanged?.Invoke(item);
            }
        }
        catch (Exception e)
        {
            item.State = DownloadState.Failed;
            item.FailureReason = e.Message;
            StateChanged?.Invoke(item);
        }
    }

    // MARK: lifecycle

    public void Start(DownloadItem item, Dictionary<string, string> pluginOptions,
                      string? formatId, bool audioOnly, bool resume = false)
    {
        var downloader = item.Downloader;
        if (downloader is null) return;
        var settings = settingsProvider();

        item.ChosenPluginOptions = pluginOptions;
        item.ChosenFormatId = formatId;
        item.ChosenAudioOnly = audioOnly;

        var workFolder = settings.StageToTemp
            ? Staging.StagingDir(item.Url, settings.DownloadFolder)
            : settings.DownloadFolder;

        try
        {
            Directory.CreateDirectory(workFolder);
        }
        catch (Exception e)
        {
            item.State = DownloadState.Failed;
            item.FailureReason = $"Cannot create download folder: {e.Message}";
            StateChanged?.Invoke(item);
            return;
        }

        var options = new DownloadOptions
        {
            DownloadFolder = workFolder,
            FormatId = formatId,
            AudioOnly = audioOnly,
            IsPlaylist = item.Info?.IsPlaylist ?? false,
            Resume = resume,
            PluginOptions = pluginOptions,
        };
        var command = downloader.BuildArgs(item.Url, options, settings);

        RunningProcess process;
        try
        {
            process = ProcessRunner.Run(command, workFolder);
        }
        catch (Exception e)
        {
            item.State = DownloadState.Failed;
            item.FailureReason = e.Message;
            StateChanged?.Invoke(item);
            return;
        }

        lock (_lock) _running[item.Id] = process;
        item.State = DownloadState.Downloading;
        StateChanged?.Invoke(item);

        _ = ConsumeAsync(process, item, downloader,
                         settings.StageToTemp ? workFolder : null);
    }

    /// <summary>
    /// Windows pause: kill the process, keep the staging dir. Resume relaunches
    /// with the tool's continue flag. Only offered when SupportsResume.
    /// </summary>
    public void Pause(DownloadItem item)
    {
        if (item.State != DownloadState.Downloading || !item.SupportsPause) return;
        item.State = DownloadState.Paused; // set first so exit handler keeps staging
        GetRunning(item)?.Kill();
        StateChanged?.Invoke(item);
    }

    public void Resume(DownloadItem item)
    {
        if (item.State != DownloadState.Paused) return;
        Start(item, item.ChosenPluginOptions, item.ChosenFormatId, item.ChosenAudioOnly, resume: true);
    }

    public void Cancel(DownloadItem item)
    {
        if (item.State is DownloadState.Downloading or DownloadState.Paused)
        {
            item.State = DownloadState.Cancelled;
            GetRunning(item)?.Kill();
        }
        else
        {
            item.State = DownloadState.Cancelled;
        }
        StateChanged?.Invoke(item);
    }

    /// <summary>Retry a failed download; staging dir was left in place, so resumable tools continue.</summary>
    public void Retry(DownloadItem item)
    {
        if (item.State != DownloadState.Failed) return;
        item.Progress = new ProgressUpdate();
        item.RecentLines.Clear();
        item.FailureReason = "";
        if (item.Info is null)
        {
            Remove(item);
            _ = SubmitAsync(item.Url);
            return;
        }
        Start(item, item.ChosenPluginOptions, item.ChosenFormatId, item.ChosenAudioOnly,
              resume: item.SupportsPause);
    }

    public void Remove(DownloadItem item)
    {
        if (item.State is DownloadState.Downloading or DownloadState.Paused)
            Cancel(item);
        lock (_lock)
        {
            _items.Remove(item);
            _running.Remove(item.Id);
        }
        ItemRemoved?.Invoke(item);
    }

    public void ClearFinished()
    {
        List<DownloadItem> finished;
        lock (_lock)
        {
            finished = _items.Where(i => i.State is DownloadState.Completed
                or DownloadState.Failed or DownloadState.Cancelled).ToList();
            foreach (var item in finished) _items.Remove(item);
        }
        foreach (var item in finished) ItemRemoved?.Invoke(item);
    }

    // MARK: process events

    private void AddItem(DownloadItem item)
    {
        lock (_lock) _items.Insert(0, item);
        ItemAdded?.Invoke(item);
    }

    private RunningProcess? GetRunning(DownloadItem item)
    {
        lock (_lock) return _running.GetValueOrDefault(item.Id);
    }

    private async Task ConsumeAsync(RunningProcess process, DownloadItem item,
                                    IDownloader downloader, string? stagingDir)
    {
        await foreach (var line in process.Lines.ConfigureAwait(false))
            HandleLine(line, item, downloader);

        var exitCode = await process.WaitForExitAsync().ConfigureAwait(false);
        HandleExit(exitCode, item, stagingDir);
    }

    private void HandleLine(string line, DownloadItem item, IDownloader downloader)
    {
        lock (item.RecentLines)
        {
            item.RecentLines.Add(line);
            if (item.RecentLines.Count > 20) item.RecentLines.RemoveAt(0);
        }

        var update = downloader.ParseProgress(line);
        if (update is null) return;

        // Throttle UI-visible progress to ~10/s per item.
        var now = Environment.TickCount64;
        lock (_lock)
        {
            if (_lastProgressTick.TryGetValue(item.Id, out var last)
                && now - last < 100 && update.Percent != 100)
                return;
            _lastProgressTick[item.Id] = now;
        }

        item.Progress = update with
        {
            Percent = update.Percent ?? item.Progress.Percent,
            FileCount = update.FileCount ?? item.Progress.FileCount,
        };
        ProgressChanged?.Invoke(item);
    }

    private void HandleExit(int exitCode, DownloadItem item, string? stagingDir)
    {
        lock (_lock) _running.Remove(item.Id);

        // Pause and cancel keep the staging dir for a future resume/retry.
        if (item.State is DownloadState.Paused or DownloadState.Cancelled)
        {
            StateChanged?.Invoke(item);
            return;
        }

        var settings = settingsProvider();
        if (exitCode == 0)
        {
            var destination = settings.DownloadFolder;
            if (stagingDir is not null)
            {
                var source = Uri.TryCreate(item.Url, UriKind.Absolute, out var uri) && uri.Host.Length > 0
                    ? uri.Host
                    : item.DownloaderId;
                try
                {
                    var moved = Staging.Finalize(stagingDir, settings.DownloadFolder,
                                                 settings.Organize, source);
                    if (moved.Count == 1)
                        destination = moved[0];
                    else if (moved.Count > 1)
                        destination = Path.GetDirectoryName(moved[0]) ?? destination;
                }
                catch (Exception e)
                {
                    item.State = DownloadState.Failed;
                    item.FailureReason = $"Downloaded, but moving files failed: {e.Message}";
                    StateChanged?.Invoke(item);
                    return;
                }
            }
            item.Progress = item.Progress with { Percent = 100 };
            item.Destination = destination;
            item.State = DownloadState.Completed;
        }
        else
        {
            string detail;
            lock (item.RecentLines)
                detail = item.RecentLines.LastOrDefault(l => l.Trim().Length > 0) ?? "";
            item.State = DownloadState.Failed;
            item.FailureReason = detail.Length == 0 ? $"Exited with code {exitCode}" : detail;
        }
        StateChanged?.Invoke(item);
    }
}
