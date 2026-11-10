using System.Reflection;
using Microsoft.UI.Xaml.Controls;

namespace KhzAnalyzer.Pages;

public sealed partial class AboutPage : Page
{
    public AboutPage()
    {
        InitializeComponent();
        var v = Assembly.GetExecutingAssembly().GetName().Version;
        VersionText.Text = v is null ? "" : $"Version {v.Major}.{v.Minor}.{v.Build}";
    }
}
