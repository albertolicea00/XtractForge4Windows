using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using XtractForge.Core.Engine;
using XtractForge.ViewModels;

namespace XtractForge.Views;

public sealed partial class MainPage : UserControl
{
    public MainViewModel ViewModel { get; }

    private readonly Queue<DownloadItem> _pendingOptions = [];
    private bool _dialogOpen;

    public MainPage()
    {
        InitializeComponent();
        ViewModel = new MainViewModel(DispatcherQueue);
        ViewModel.OptionsRequested += OnOptionsRequested;
    }

    // MARK: drag & drop

    private void OnDragOver(object sender, DragEventArgs e)
    {
        var view = e.DataView;
        if (view.Contains(StandardDataFormats.Text)
            || view.Contains(StandardDataFormats.WebLink)
            || view.Contains(StandardDataFormats.StorageItems))
        {
            e.AcceptedOperation = DataPackageOperation.Link;
        }
    }

    private async void OnDrop(object sender, DragEventArgs e)
    {
        var view = e.DataView;
        if (view.Contains(StandardDataFormats.WebLink))
        {
            var uri = await view.GetWebLinkAsync();
            App.Intake.Submit(uri.AbsoluteUri);
        }
        else if (view.Contains(StandardDataFormats.Text))
        {
            App.Intake.Submit(await view.GetTextAsync());
        }
        else if (view.Contains(StandardDataFormats.StorageItems))
        {
            foreach (var item in await view.GetStorageItemsAsync())
                App.Intake.Submit(item.Path);
        }
    }

    private void OnSuggestionDismissed(InfoBar sender, object args) =>
        ViewModel.DismissClipboardSuggestionCommand.Execute(null);

    // MARK: options dialog (one ContentDialog at a time)

    private void OnOptionsRequested(DownloadItem item)
    {
        _pendingOptions.Enqueue(item);
        _ = ShowNextOptionsDialogAsync();
    }

    private async Task ShowNextOptionsDialogAsync()
    {
        if (_dialogOpen) return;
        _dialogOpen = true;
        try
        {
            while (_pendingOptions.Count > 0)
            {
                var item = _pendingOptions.Dequeue();
                if (item.State != DownloadState.AwaitingOptions) continue;
                var dialog = new OptionsDialog(item) { XamlRoot = XamlRoot };
                await dialog.ShowAsync();
            }
        }
        finally
        {
            _dialogOpen = false;
        }
    }
}
