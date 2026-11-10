using System;
using System.Diagnostics;
using System.IO;
using KhzAnalyzer.Utils;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace KhzAnalyzer.Pages;

public sealed partial class SettingsPage : Page
{
    private bool _carregando = true;

    public SettingsPage()
    {
        InitializeComponent();
        ThemeChoice.SelectedIndex = AppSettings.Current.Theme == "Dark" ? 1 : 0;
        _carregando = false;
    }

    private void ThemeChoice_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_carregando) return;
        if (ThemeChoice.SelectedItem is not RadioButton escolhido) return;

        string tema = escolhido.Tag as string ?? "Light";
        AppSettings.Current.Theme = tema;
        AppSettings.Current.Save();
        App.MainWindow?.ApplyTheme(tema);
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
