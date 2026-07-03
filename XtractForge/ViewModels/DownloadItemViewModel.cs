using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XtractForge.Core.Engine;

namespace XtractForge.ViewModels;

/// <summary>UI mirror of a Core DownloadItem; refreshed on manager events.</summary>
public partial class DownloadItemViewModel(DownloadItem model) : ObservableObject
{
    public DownloadItem Model { get; } = model;
    public Guid Id => Model.Id;

    [ObservableProperty] private string _title = model.Title;
    [ObservableProperty] private string _subtitle = "";
    [ObservableProperty] private string _stateGlyph = ""; // Download
    [ObservableProperty] private double _percent;
    [ObservableProperty] private bool _isProgressVisible;
    [ObservableProperty] private bool _isIndeterminate;

    [ObservableProperty] private bool _canPause;
    [ObservableProperty] private bool _canResume;
    [ObservableProperty] private bool _canCancel;
    [ObservableProperty] private bool _canReveal;
    [ObservableProperty] private bool _canRetry;
    [ObservableProperty] private bool _canRemove;

    /// <summary>Recompute all bindable state from the model. Must run on the UI thread.</summary>
    public void Refresh()
    {
        Title = Model.Title;
        Percent = Model.Progress.Percent ?? 0;
        IsProgressVisible = Model.State is DownloadState.Downloading or DownloadState.Paused;
        IsIndeterminate = Model.State == DownloadState.Downloading && Model.Progress.Percent is null;

        StateGlyph = Model.State switch
        {
            DownloadState.FetchingInfo => "",    // Search
            DownloadState.AwaitingOptions => "", // Settings
            DownloadState.Downloading => "",     // Download
            DownloadState.Paused => "",          // Pause
            DownloadState.Completed => "",       // CheckMark
            DownloadState.Failed => "",          // Warning
            _ => "",                             // Cancel
        };

        Subtitle = Model.State switch
        {
            DownloadState.FetchingInfo => "Fetching info…",
            DownloadState.AwaitingOptions => "Choosing options…",
            DownloadState.Downloading => ProgressText(),
            DownloadState.Paused => $"Paused · {Model.DownloaderId}",
            DownloadState.Completed => Model.Destination ?? "Completed",
            DownloadState.Failed => Model.FailureReason,
            _ => "Cancelled",
        };

        CanPause = Model.State == DownloadState.Downloading && Model.SupportsPause;
        CanResume = Model.State == DownloadState.Paused;
        CanCancel = Model.State is DownloadState.Downloading or DownloadState.Paused
            or DownloadState.FetchingInfo;
        CanReveal = Model.State == DownloadState.Completed && Model.Destination is not null;
        CanRetry = Model.State == DownloadState.Failed;
        CanRemove = Model.State is DownloadState.Completed or DownloadState.Failed
            or DownloadState.Cancelled;
    }

    private string ProgressText()
    {
        var parts = new List<string>();
        var p = Model.Progress;
        if (p.Percent is { } pct) parts.Add($"{pct:0.#}%");
        if (p.Size.Length > 0) parts.Add(p.Size);
        if (p.Speed.Length > 0) parts.Add(p.Speed);
        if (p.Eta.Length > 0) parts.Add($"ETA {p.Eta}");
        if (p.FileCount is { } count) parts.Add($"{count} files");
        return parts.Count == 0
            ? $"Downloading… · {Model.DownloaderId}"
            : string.Join(" · ", parts) + $" · {Model.DownloaderId}";
    }

    [RelayCommand] private void Pause() => App.Manager.Pause(Model);
    [RelayCommand] private void Resume() => App.Manager.Resume(Model);
    [RelayCommand] private void Cancel() => App.Manager.Cancel(Model);
    [RelayCommand] private void Retry() => App.Manager.Retry(Model);
    [RelayCommand] private void Remove() => App.Manager.Remove(Model);

    [RelayCommand]
    private void Reveal()
    {
        if (Model.Destination is not { } destination) return;
        if (File.Exists(destination))
            Process.Start("explorer.exe", $"/select,\"{destination}\"");
        else if (Directory.Exists(destination))
            Process.Start("explorer.exe", $"\"{destination}\"");
    }
}
