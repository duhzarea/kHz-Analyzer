using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System.Threading.Tasks;
using System;
using KhzAnalyzer.Utils;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace KhzAnalyzer;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : Application
{
    private Window? _window;

    /// <summary>The single app window. Pages use it to switch the theme.</summary>
    public static MainWindow? MainWindow { get; private set; }
    
    /// <summary>
    /// Initializes the singleton application object.  This is the first line of authored code
    /// executed, and as such is the logical equivalent of main() or WinMain().
    /// </summary>
    public App()
    {
        // English-only UI (spec v2.1): keeps built-in control texts such as "Settings"
        // in English even when Windows runs in another language.
        try { Microsoft.Windows.Globalization.ApplicationLanguages.PrimaryLanguageOverride = "en-US"; }
        catch (Exception ex) { Logger.Log($"Could not set the UI language: {ex.Message}"); }

        InitializeComponent();
        
        this.UnhandledException += App_UnhandledException;
        TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
    }

    private void App_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        Logger.Log($"UI Unhandled Exception: {e.Exception}");
        e.Handled = false; // Fail fast após o log, não engole o erro
    }

    private void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        Logger.Log($"Task Unobserved Exception: {e.Exception}");
        e.SetObserved();
    }

    private void CurrentDomain_UnhandledException(object sender, System.UnhandledExceptionEventArgs e)
    {
        Logger.Log($"AppDomain Unhandled Exception: {e.ExceptionObject}");
    }

    /// <summary>
    /// Invoked when the application is launched.
    /// </summary>
    /// <param name="args">Details about the launch request and process.</param>
    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        var uiSettings = new Windows.UI.ViewManagement.UISettings();
        if (!Utils.Motion.Enabled)
        {
            var zeroTime = Microsoft.UI.Xaml.Media.Animation.KeyTime.FromTimeSpan(System.TimeSpan.Zero);
            this.Resources["DurationFastKeyTime"] = zeroTime;
            this.Resources["DurationNormalKeyTime"] = zeroTime;
            this.Resources["DurationSlowKeyTime"] = zeroTime;
        }

        var janela = new MainWindow();
        _window = janela;
        MainWindow = janela;
        janela.Activate();
    }
}
