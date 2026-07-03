using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using XtractForge.Core.Models;
using XtractForge.Services;
using XtractForge.Views;

namespace XtractForge;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(TitleBarArea);
        AppWindow.Resize(new Windows.Graphics.SizeInt32(640, 760));

        var appearance = App.Settings.Current.Appearance;
        ThemeSystem.IsChecked = appearance == AppearanceSetting.System;
        ThemeLight.IsChecked = appearance == AppearanceSetting.Light;
        ThemeDark.IsChecked = appearance == AppearanceSetting.Dark;

        Activated += (_, args) =>
        {
            if (args.WindowActivationState != WindowActivationState.Deactivated)
                _ = Main.ViewModel.CheckClipboardAsync();
        };
    }

    public void BringToFront()
    {
        AppWindow.Show();
        Activate();
    }

    private void OnPasteUrl(object sender, RoutedEventArgs e) =>
        Main.ViewModel.PasteCommand.Execute(null);

    private void OnPasteAccelerator(KeyboardAccelerator sender,
                                    KeyboardAcceleratorInvokedEventArgs args)
    {
        // Let Ctrl+V behave normally inside text boxes.
        if (args.Element is TextBox || FocusManager.GetFocusedElement(Content.XamlRoot) is TextBox)
            return;
        args.Handled = true;
        Main.ViewModel.PasteCommand.Execute(null);
    }

    private void OnOpenDownloadsFolder(object sender, RoutedEventArgs e) =>
        Main.ViewModel.OpenDownloadsFolderCommand.Execute(null);

    private async void OnOpenSettings(object sender, RoutedEventArgs e)
    {
        var dialog = new SettingsDialog { XamlRoot = Content.XamlRoot };
        await dialog.ShowAsync();
    }

    private void OnExit(object sender, RoutedEventArgs e) => Application.Current.Exit();

    private void OnThemeChanged(object sender, RoutedEventArgs e)
    {
        var appearance = sender == ThemeLight ? AppearanceSetting.Light
            : sender == ThemeDark ? AppearanceSetting.Dark
            : AppearanceSetting.System;
        App.Settings.Update(s => s.Appearance = appearance);
        ThemeService.Apply(this, appearance);
    }

    private async void OnAbout(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            Title = "XtractForge",
            Content = "Native media downloader.\n\nDrop or paste a link — XtractForge routes it "
                    + "to yt-dlp, lux, gallery-dl, spotDL, FFmpeg, or curl and downloads it.",
            CloseButtonText = "OK",
            XamlRoot = Content.XamlRoot,
        };
        await dialog.ShowAsync();
    }
}
