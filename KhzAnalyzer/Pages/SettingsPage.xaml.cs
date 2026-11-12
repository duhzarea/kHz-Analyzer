using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using KhzAnalyzer.Utils;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace KhzAnalyzer.Pages;

public sealed partial class SettingsPage : Page
{
    public SettingsPage()
    {
        InitializeComponent();
        SyncTheme();
        BuildShortcuts();
        string m = AppSettings.Current.Motion;
        MotionOn.IsChecked = m is not ("System" or "Off");
        MotionSystem.IsChecked = m == "System";
        MotionOff.IsChecked = m == "Off";
        if (App.MainWindow is not null) App.MainWindow.ThemeChanged += SyncTheme;
        Unloaded += (_, _) => { if (App.MainWindow is not null) App.MainWindow.ThemeChanged -= SyncTheme; };
    }

    private void SyncTheme()
    {
        bool dark = App.MainWindow?.IsDark ?? AppSettings.Current.Theme == "Dark";
        LightChoice.IsChecked = !dark;
        DarkChoice.IsChecked = dark;
    }

    private void Theme_Click(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton rb && rb.Tag is string theme && App.MainWindow is { } w)
            w.SwitchTheme(theme, w.CenterOf(rb));
    }

    private void Motion_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not RadioButton rb || rb.Tag is not string v) return;
        AppSettings.Current.Motion = v;
        AppSettings.Current.Save();
    }

    /// <summary>Scrolls the shortcut list into view (Ctrl+/).</summary>
    public void ShowShortcuts() => ShortcutsCard.StartBringIntoView(new BringIntoViewOptions { AnimationDesired = true, VerticalAlignmentRatio = 0.1 });

    private void BuildShortcuts()
    {
        var style = (Style)Application.Current.Resources["KeyCapBorderStyle"];
        var mono = (FontFamily)Application.Current.Resources["MonoFontFamily"];
        foreach (var group in Shortcuts.All.GroupBy(s => s.Group))
        {
            var block = new StackPanel { Spacing = 2 };
            block.Children.Add(new TextBlock
            {
                Text = group.Key.ToUpperInvariant(), FontFamily = mono, FontSize = 11, CharacterSpacing = 160,
                Opacity = 0.6, Margin = new Thickness(0, 0, 0, 6),
            });
            foreach (var s in group)
            {
                var row = new Grid { Padding = new Thickness(0, 6, 0, 6), ColumnSpacing = 16 };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                row.Children.Add(new TextBlock { Text = s.Description, VerticalAlignment = VerticalAlignment.Center });
                var keys = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
                for (int i = 0; i < s.Keys.Count; i++)
                {
                    if (i > 0) keys.Children.Add(new TextBlock { Text = "+", Opacity = 0.5, VerticalAlignment = VerticalAlignment.Center, FontSize = 12 });
                    keys.Children.Add(new Border
                    {
                        Style = style,
                        Child = new TextBlock { Text = s.Keys[i], FontFamily = mono, FontSize = 12, HorizontalAlignment = HorizontalAlignment.Center },
                    });
                }
                Grid.SetColumn(keys, 1);
                row.Children.Add(keys);
                block.Children.Add(row);
            }
            ShortcutList.Children.Add(block);
        }
    }

    private void OpenLogs_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string logDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "kHzAnalyzer", "logs");
            Directory.CreateDirectory(logDir);
            Process.Start(new ProcessStartInfo { FileName = logDir, UseShellExecute = true, Verb = "open" });
        }
        catch (Exception ex)
        {
            Logger.Log($"Could not open the logs folder: {ex.Message}");
        }
    }
}
