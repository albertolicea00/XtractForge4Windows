using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using XtractForge.Core.Engine;
using XtractForge.Core.Models;

namespace XtractForge.Views;

/// <summary>
/// Per-download options: format picker, audio-only (yt-dlp), plus whatever
/// OptionFields the downloader declared. Fields are built in code because the
/// schema is dynamic.
/// </summary>
public sealed partial class OptionsDialog : ContentDialog
{
    private const string BestTag = "best";

    private readonly DownloadItem _item;
    private ComboBox? _formatBox;
    private CheckBox? _audioOnlyBox;
    private ComboBox? _audioFormatBox;
    private readonly Dictionary<string, FrameworkElement> _fieldControls = [];
    private bool _started;

    public OptionsDialog(DownloadItem item)
    {
        InitializeComponent();
        _item = item;
        Title = item.Title.Length > 60 ? item.Title[..60] + "…" : item.Title;
        BuildFields();
    }

    private void BuildFields()
    {
        var info = _item.Info;
        if (info is null) return;

        var badge = new TextBlock
        {
            Text = info.Uploader.Length > 0
                ? $"{_item.DownloaderId} · {info.Uploader}"
                : _item.DownloaderId,
            Opacity = 0.7,
        };
        FieldsPanel.Children.Add(badge);

        if (info.IsPlaylist)
        {
            FieldsPanel.Children.Add(new TextBlock
            {
                Text = $"Playlist · {info.EntryCount} items",
                Opacity = 0.7,
            });
        }

        if (info.Formats.Count > 0)
        {
            _formatBox = new ComboBox
            {
                Header = "Format",
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };
            _formatBox.Items.Add(new ComboBoxItem { Content = "Best available", Tag = BestTag });
            foreach (var format in info.Formats)
            {
                _formatBox.Items.Add(new ComboBoxItem
                {
                    Content = FormatLabel(format),
                    Tag = format.FormatId,
                });
            }
            _formatBox.SelectedIndex = 0;
            FieldsPanel.Children.Add(_formatBox);
        }

        if (_item.DownloaderId == "yt-dlp")
        {
            _audioOnlyBox = new CheckBox { Content = "Audio only" };
            _audioFormatBox = new ComboBox
            {
                Header = "Audio format",
                Visibility = Visibility.Collapsed,
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };
            foreach (var fmt in new[] { "mp3", "m4a", "opus", "flac", "wav" })
                _audioFormatBox.Items.Add(new ComboBoxItem { Content = fmt.ToUpperInvariant(), Tag = fmt });
            _audioFormatBox.SelectedIndex = 0;

            _audioOnlyBox.Checked += (_, _) =>
            {
                _audioFormatBox.Visibility = Visibility.Visible;
                if (_formatBox is not null) _formatBox.IsEnabled = false;
            };
            _audioOnlyBox.Unchecked += (_, _) =>
            {
                _audioFormatBox.Visibility = Visibility.Collapsed;
                if (_formatBox is not null) _formatBox.IsEnabled = true;
            };

            FieldsPanel.Children.Add(_audioOnlyBox);
            FieldsPanel.Children.Add(_audioFormatBox);
        }

        foreach (var field in info.OptionFields)
        {
            FrameworkElement control = field.Kind switch
            {
                OptionKind.Toggle => new ToggleSwitch
                {
                    Header = field.Label,
                    IsOn = field.DefaultValue == "true",
                },
                OptionKind.Select => BuildSelect(field),
                _ => new TextBox
                {
                    Header = field.Label,
                    Text = field.DefaultValue,
                    PlaceholderText = field.Placeholder,
                },
            };
            if (field.Help.Length > 0)
                ToolTipService.SetToolTip(control, field.Help);
            _fieldControls[field.Key] = control;
            FieldsPanel.Children.Add(control);
        }
    }

    private static ComboBox BuildSelect(OptionField field)
    {
        var box = new ComboBox
        {
            Header = field.Label,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        var selected = 0;
        var options = field.Options ?? [];
        for (var i = 0; i < options.Count; i++)
        {
            box.Items.Add(new ComboBoxItem { Content = options[i], Tag = options[i] });
            if (options[i] == field.DefaultValue) selected = i;
        }
        box.SelectedIndex = options.Count > 0 ? selected : -1;
        return box;
    }

    private static string FormatLabel(MediaFormat format)
    {
        var parts = new List<string>
        {
            format.Resolution.Length > 0 ? format.Resolution : format.FormatId,
        };
        if (format.Ext.Length > 0) parts.Add(format.Ext);
        if (format.Note.Length > 0) parts.Add(format.Note);
        if (format.Filesize is { } size and > 0)
            parts.Add($"{size / 1024.0 / 1024.0:0.#} MB");
        return string.Join(" · ", parts);
    }

    private void OnDownload(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        _started = true;

        var pluginOptions = new Dictionary<string, string>();
        foreach (var (key, control) in _fieldControls)
        {
            pluginOptions[key] = control switch
            {
                ToggleSwitch toggle => toggle.IsOn ? "true" : "false",
                ComboBox box => (box.SelectedItem as ComboBoxItem)?.Tag as string ?? "",
                TextBox text => text.Text,
                _ => "",
            };
        }

        var audioOnly = _audioOnlyBox?.IsChecked == true;
        var formatId = (_formatBox?.SelectedItem as ComboBoxItem)?.Tag as string;
        if (formatId == BestTag || audioOnly) formatId = null;

        var audioFormat = (_audioFormatBox?.SelectedItem as ComboBoxItem)?.Tag as string ?? "mp3";
        App.Manager.Start(_item, pluginOptions, formatId, audioOnly, audioFormat);
    }

    private void OnCancelled(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        if (!_started)
            App.Manager.Remove(_item);
    }
}
