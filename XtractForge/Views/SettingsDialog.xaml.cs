using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Pickers;
using WinRT.Interop;
using XtractForge.Core.Downloaders;
using XtractForge.Core.Models;

namespace XtractForge.Views;

/// <summary>
/// General + per-downloader settings. The Appearance setting lives in the
/// View menu (System/Light/Dark), not here.
/// </summary>
public sealed partial class SettingsDialog : ContentDialog
{
    // Per-downloader controls, keyed by downloader id.
    private readonly Dictionary<string, CheckBox> _enabledBoxes = [];
    private readonly Dictionary<string, TextBox> _binaryBoxes = [];
    private readonly Dictionary<string, Dictionary<string, FrameworkElement>> _extraControls = [];
    private readonly Dictionary<string, TextBlock> _statusTexts = [];

    public SettingsDialog()
    {
        InitializeComponent();
        LoadGeneral();
        BuildDownloaderSections();
        _ = RefreshDependencyStatusAsync();
    }

    private void LoadGeneral()
    {
        var s = App.Settings.Current;
        DownloadFolderBox.Text = s.DownloadFolder;
        SpeedLimitBox.Text = s.SpeedLimit;
        StageToTempToggle.IsOn = s.StageToTemp;
        WatchClipboardToggle.IsOn = s.WatchClipboard;
        OrganizeBox.SelectedIndex = s.Organize switch
        {
            Organize.Type => 1,
            Organize.Source => 2,
            _ => 0,
        };
    }

    private void BuildDownloaderSections()
    {
        var s = App.Settings.Current;
        foreach (var downloader in DownloaderRegistry.All)
        {
            var expander = new Expander
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
            };

            var headerPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            headerPanel.Children.Add(new TextBlock { Text = downloader.Name });
            var status = new TextBlock { Text = "…", Opacity = 0.6 };
            headerPanel.Children.Add(status);
            _statusTexts[downloader.Id] = status;
            expander.Header = headerPanel;

            var panel = new StackPanel { Spacing = 8 };

            var enabled = new CheckBox
            {
                Content = "Enabled",
                IsChecked = !s.DisabledDownloaders.Contains(downloader.Id),
                IsEnabled = downloader.Id != "yt-dlp", // catch-all stays on
            };
            _enabledBoxes[downloader.Id] = enabled;
            panel.Children.Add(enabled);

            var binary = new TextBox
            {
                Header = "Binary path",
                Text = BinaryPathFor(downloader.Id, s),
                PlaceholderText = downloader.BinaryDefault,
            };
            _binaryBoxes[downloader.Id] = binary;
            panel.Children.Add(binary);

            var extras = new Dictionary<string, FrameworkElement>();
            foreach (var control in ExtraControlsFor(downloader.Id, s))
            {
                extras[control.key] = control.element;
                panel.Children.Add(control.element);
            }
            _extraControls[downloader.Id] = extras;

            panel.Children.Add(new TextBlock
            {
                Text = $"Install: {downloader.InstallHint}",
                Opacity = 0.6,
                TextWrapping = TextWrapping.Wrap,
            });

            expander.Content = panel;
            DownloadersPanel.Children.Add(expander);
        }
    }

    private static string BinaryPathFor(string id, AppSettings s) => id switch
    {
        "yt-dlp" => s.YtdlpPath,
        "ffmpeg" => s.FfmpegPath,
        "lux" => s.LuxPath,
        "gallery-dl" => s.GalleryDlPath,
        "spotdl" => s.SpotdlPath,
        _ => s.CurlPath,
    };

    private static IEnumerable<(string key, FrameworkElement element)> ExtraControlsFor(
        string id, AppSettings s)
    {
        switch (id)
        {
            case "yt-dlp":
                yield return ("embedSubtitles", new ToggleSwitch
                    { Header = "Embed subtitles", IsOn = s.EmbedSubtitles });
                yield return ("sponsorBlock", new ToggleSwitch
                    { Header = "SponsorBlock (skip sponsor segments)", IsOn = s.SponsorBlock });
                break;
            case "ffmpeg":
                yield return ("container", MakeSelect("Stream output container",
                    ["mp4", "mkv", "ts"], s.FfmpegContainer));
                break;
            case "lux":
                yield return ("cookie", new TextBox
                    { Header = "Cookie (optional)", Text = s.LuxCookie });
                yield return ("multiThread", new ToggleSwitch
                    { Header = "Multi-thread download", IsOn = s.LuxMultiThread });
                break;
            case "gallery-dl":
                yield return ("cookies", new TextBox
                {
                    Header = "Cookies file (optional)",
                    Text = s.GalleryDlCookies,
                    PlaceholderText = @"C:\path\to\cookies.txt",
                });
                yield return ("config", new TextBox
                {
                    Header = "Config file (optional)",
                    Text = s.GalleryDlConfig,
                    PlaceholderText = @"C:\path\to\gallery-dl.conf",
                });
                break;
            case "spotdl":
                yield return ("format", MakeSelect("Format",
                    ["mp3", "flac", "ogg", "opus", "m4a", "wav"], s.SpotdlFormat));
                yield return ("bitrate", MakeSelect("Bitrate",
                    ["128k", "192k", "256k", "320k"], s.SpotdlBitrate));
                break;
        }
    }

    private static ComboBox MakeSelect(string header, string[] options, string current)
    {
        var box = new ComboBox { Header = header, HorizontalAlignment = HorizontalAlignment.Stretch };
        var selected = 0;
        for (var i = 0; i < options.Length; i++)
        {
            box.Items.Add(new ComboBoxItem { Content = options[i], Tag = options[i] });
            if (options[i] == current) selected = i;
        }
        box.SelectedIndex = selected;
        return box;
    }

    private async Task RefreshDependencyStatusAsync()
    {
        var settings = App.Settings.Current;
        foreach (var downloader in DownloaderRegistry.All)
        {
            var status = await downloader.CheckDependencyAsync(settings);
            if (_statusTexts.TryGetValue(downloader.Id, out var text))
                text.Text = status.Available ? $"✓ {status.Version}" : "not installed";
        }
    }

    private async void OnBrowseFolder(object sender, RoutedEventArgs e)
    {
        var picker = new FolderPicker();
        picker.FileTypeFilter.Add("*");
        InitializeWithWindow.Initialize(picker,
            WindowNative.GetWindowHandle(App.MainWindow));
        var folder = await picker.PickSingleFolderAsync();
        if (folder is not null)
            DownloadFolderBox.Text = folder.Path;
    }

    private void OnSave(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        App.Settings.Update(s =>
        {
            s.DownloadFolder = DownloadFolderBox.Text.Trim();
            s.SpeedLimit = SpeedLimitBox.Text.Trim();
            s.StageToTemp = StageToTempToggle.IsOn;
            s.WatchClipboard = WatchClipboardToggle.IsOn;
            s.Organize = OrganizeBox.SelectedIndex switch
            {
                1 => Organize.Type,
                2 => Organize.Source,
                _ => Organize.None,
            };

            s.DisabledDownloaders = _enabledBoxes
                .Where(kv => kv.Value.IsChecked != true)
                .Select(kv => kv.Key)
                .ToList();

            s.YtdlpPath = _binaryBoxes["yt-dlp"].Text.Trim();
            s.FfmpegPath = _binaryBoxes["ffmpeg"].Text.Trim();
            s.LuxPath = _binaryBoxes["lux"].Text.Trim();
            s.GalleryDlPath = _binaryBoxes["gallery-dl"].Text.Trim();
            s.SpotdlPath = _binaryBoxes["spotdl"].Text.Trim();
            s.CurlPath = _binaryBoxes["curl"].Text.Trim();

            s.EmbedSubtitles = ((ToggleSwitch)_extraControls["yt-dlp"]["embedSubtitles"]).IsOn;
            s.SponsorBlock = ((ToggleSwitch)_extraControls["yt-dlp"]["sponsorBlock"]).IsOn;
            s.FfmpegContainer = SelectedTag(_extraControls["ffmpeg"]["container"], "mp4");
            s.LuxCookie = ((TextBox)_extraControls["lux"]["cookie"]).Text.Trim();
            s.LuxMultiThread = ((ToggleSwitch)_extraControls["lux"]["multiThread"]).IsOn;
            s.GalleryDlCookies = ((TextBox)_extraControls["gallery-dl"]["cookies"]).Text.Trim();
            s.GalleryDlConfig = ((TextBox)_extraControls["gallery-dl"]["config"]).Text.Trim();
            s.SpotdlFormat = SelectedTag(_extraControls["spotdl"]["format"], "mp3");
            s.SpotdlBitrate = SelectedTag(_extraControls["spotdl"]["bitrate"], "320k");
        });
    }

    private static string SelectedTag(FrameworkElement element, string fallback) =>
        (element as ComboBox)?.SelectedItem is ComboBoxItem { Tag: string tag } ? tag : fallback;
}
