using Microsoft.UI.Xaml.Controls;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace KhzAnalyzer.Pages;

public sealed partial class AboutPage : Page
{
    public AboutPage()
    {
        InitializeComponent();
    }

    private void AbrirLogs_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string logDir = System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData), "kHzAnalyzer", "logs");
            System.IO.Directory.CreateDirectory(logDir); // Garante que existe antes de abrir
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = logDir,
                UseShellExecute = true,
                Verb = "open"
            });
        }
        catch { }
    }
}
