using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace KhzAnalyzer.Pages;

/// <summary>Placeholder for tools that are specified but not built yet.</summary>
public sealed partial class ComingSoonPage : Page
{
    public ComingSoonPage()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        if (e.Parameter as string == "download")
        {
            TitleText.Text = "Download & Trim";
            Icon.Glyph = "";
            BodyText.Text = "Paste a YouTube or SoundCloud link, set start and end time codes, and export to WAV, M4A, MP3 or MP4.";
        }
        else
        {
            TitleText.Text = "Snippets";
            Icon.Glyph = "";
            BodyText.Text = "Creates a short MP3 preview of every track in a folder, skipping the tracks that already have one.";
        }
    }
}
