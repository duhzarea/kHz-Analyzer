using System;
using System.IO;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using KhzAnalyzer.Pages;
using KhzAnalyzer.Utils;

namespace KhzAnalyzer;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        // Glass: acrylic first (spec v2.1), Mica where acrylic is unavailable,
        // and the opaque fallback brush of RootGrid when neither exists.
        if (DesktopAcrylicController.IsSupported())
        {
            SystemBackdrop = new DesktopAcrylicBackdrop();
            RootGrid.Background = null;
        }
        else if (MicaController.IsSupported())
        {
            SystemBackdrop = new MicaBackdrop();
            RootGrid.Background = null;
        }

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;

        // Absolute paths: an unpackaged app can be started with any working directory,
        // and ms-appx:/// URIs need package identity, which it does not have.
        string icone = Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico");
        if (File.Exists(icone)) AppWindow.SetIcon(icone);

        // The title bar needs a PNG: the WinUI image decoder does not read .ico files.
        string iconePng = Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon-64.png");
        if (File.Exists(iconePng))
        {
            AppTitleBar.IconSource = new ImageIconSource
            {
                ImageSource = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new Uri(iconePng)),
            };
        }

        ApplyTheme(AppSettings.Current.Theme);
    }

    /// <summary>Applies "Light" or "Dark" to the whole window, title bar buttons included.</summary>
    public void ApplyTheme(string theme)
    {
        bool dark = string.Equals(theme, "Dark", StringComparison.OrdinalIgnoreCase);
        RootGrid.RequestedTheme = dark ? ElementTheme.Dark : ElementTheme.Light;
        AppWindow.TitleBar.PreferredTheme = dark ? TitleBarTheme.Dark : TitleBarTheme.Light;
    }

    private void TitleBar_PaneToggleRequested(TitleBar sender, object args)
    {
        NavView.IsPaneOpen = !NavView.IsPaneOpen;
    }

    private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.IsSettingsSelected)
        {
            NavFrame.Navigate(typeof(SettingsPage));
            return;
        }

        if (args.SelectedItem is not NavigationViewItem item) return;

        switch (item.Tag as string)
        {
            case "analyzer":
                NavFrame.Navigate(typeof(HomePage));
                break;
            case "download":
                NavFrame.Navigate(typeof(DownloadPage));
                break;
            case "snippets":
                NavFrame.Navigate(typeof(SnippetsPage));
                break;
            case "about":
                NavFrame.Navigate(typeof(AboutPage));
                break;
        }
    }
}
