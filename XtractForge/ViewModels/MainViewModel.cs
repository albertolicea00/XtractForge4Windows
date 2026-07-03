using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using Windows.ApplicationModel.DataTransfer;
using XtractForge.Core.Engine;
using XtractForge.Services;

namespace XtractForge.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly DispatcherQueue _dispatcher;
    private readonly Dictionary<Guid, DownloadItemViewModel> _byId = [];

    public ObservableCollection<DownloadItemViewModel> Items { get; } = [];

    [ObservableProperty] private bool _isEmpty = true;
    [ObservableProperty] private string _clipboardSuggestion = "";
    [ObservableProperty] private bool _hasClipboardSuggestion;

    /// <summary>Raised on the UI thread when an item needs the options dialog.</summary>
    public event Action<DownloadItem>? OptionsRequested;

    public MainViewModel(DispatcherQueue dispatcher)
    {
        _dispatcher = dispatcher;

        App.Manager.ItemAdded += item => OnUi(() => Add(item));
        App.Manager.ItemRemoved += item => OnUi(() => RemoveVm(item));
        App.Manager.ProgressChanged += item => OnUi(() => _byId.GetValueOrDefault(item.Id)?.Refresh());
        App.Manager.StateChanged += item => OnUi(() => OnStateChanged(item));
    }

    private void OnUi(Action action) => _dispatcher.TryEnqueue(() => action());

    private void Add(DownloadItem item)
    {
        var vm = new DownloadItemViewModel(item);
        vm.Refresh();
        _byId[item.Id] = vm;
        Items.Insert(0, vm);
        IsEmpty = false;
    }

    private void RemoveVm(DownloadItem item)
    {
        if (_byId.Remove(item.Id, out var vm))
            Items.Remove(vm);
        IsEmpty = Items.Count == 0;
    }

    private void OnStateChanged(DownloadItem item)
    {
        _byId.GetValueOrDefault(item.Id)?.Refresh();

        switch (item.State)
        {
            case DownloadState.AwaitingOptions:
                OptionsRequested?.Invoke(item);
                break;
            case DownloadState.Completed:
                NotificationService.NotifyCompleted(item.Title, item.Destination);
                break;
            case DownloadState.Failed:
                NotificationService.NotifyFailed(item.Title, item.FailureReason);
                break;
        }
    }

    [RelayCommand]
    private async Task PasteAsync()
    {
        var view = Clipboard.GetContent();
        if (!view.Contains(StandardDataFormats.Text)) return;
        var text = await view.GetTextAsync();
        App.Intake.Submit(text);
    }

    [RelayCommand]
    private void OpenDownloadsFolder()
    {
        var folder = App.Settings.Current.DownloadFolder;
        Directory.CreateDirectory(folder);
        Process.Start("explorer.exe", $"\"{folder}\"");
    }

    [RelayCommand]
    private void ClearFinished() => App.Manager.ClearFinished();

    [RelayCommand]
    private void AcceptClipboardSuggestion()
    {
        if (ClipboardSuggestion.Length > 0)
            App.Intake.Submit(ClipboardSuggestion);
        DismissClipboardSuggestion();
    }

    [RelayCommand]
    private void DismissClipboardSuggestion()
    {
        ClipboardSuggestion = "";
        HasClipboardSuggestion = false;
    }

    /// <summary>Called when the window activates; offers a clipboard link (opt-in setting).</summary>
    public async Task CheckClipboardAsync()
    {
        if (!App.Settings.Current.WatchClipboard) return;
        try
        {
            var view = Clipboard.GetContent();
            if (!view.Contains(StandardDataFormats.Text)) return;
            var text = await view.GetTextAsync();
            var first = Intake.ExtractUrls(text).FirstOrDefault();
            if (first is null || first == ClipboardSuggestion) return;
            if (App.Manager.Items.Any(i => i.Url == first)) return;
            ClipboardSuggestion = first;
            HasClipboardSuggestion = true;
        }
        catch (Exception)
        {
            // Clipboard can be locked by another process — ignore.
        }
    }
}
