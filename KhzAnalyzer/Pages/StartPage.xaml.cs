using System;
using KhzAnalyzer.Controls;
using KhzAnalyzer.Utils;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace KhzAnalyzer.Pages;

/// <summary>
/// Start screen: the brand hero with a live spectrum that flips between a lossless file and an
/// MP3 saved as WAV, then the hub with the three modules. Footer carries the signature.
/// </summary>
public sealed partial class StartPage : Page
{
    private readonly DispatcherQueueTimer _flip;
    private DateTime _pinnedUntil = DateTime.MinValue;
    private bool _auto;

    public StartPage()
    {
        InitializeComponent();
        _flip = DispatcherQueue.CreateTimer();
        _flip.Interval = TimeSpan.FromMilliseconds(3600);
        _flip.Tick += (_, _) =>
        {
            if (DateTime.UtcNow < _pinnedUntil) return;
            _auto = true;
            if (ModeLossless.IsChecked == true) ModeMp3.IsChecked = true; else ModeLossless.IsChecked = true;
            _auto = false;
        };
        Loaded += (_, _) =>
        {
            Motion.Reveal(TitleKhz, TitleClip, 80);
            Motion.Reveal(TitleAnalyzer, TitleClip2, 200);
            Motion.Entrance(new UIElement[] { Tagline }, startDelayMs: 520);
            Motion.Entrance(new UIElement[] { SpectrumCard }, startDelayMs: 300, distance: 36);
            Motion.Entrance(new UIElement[] { Hub }, startDelayMs: 900, distance: 18);
            Motion.Entrance(new UIElement[] { TileAnalyzer, TileDownload, TileSnippets }, staggerMs: 110, startDelayMs: 1000, distance: 30);
            Motion.Entrance(new UIElement[] { Signature }, startDelayMs: 1400, distance: 8);
            _flip.Start();
            DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, FocusDefault);
        };
        Unloaded += (_, _) => _flip.Stop();
    }

    public void FocusDefault()
    {
        // Pointer state: focus lands here for shortcuts without drawing a focus ring.
        TileAnalyzer.Focus(FocusState.Pointer);
    }

    private void Mode_Checked(object sender, RoutedEventArgs e)
    {
        if (Spectrum is null || VerdictPill is null || ModeMp3 is null || VerdictText is null) return;
        if (!_auto) _pinnedUntil = DateTime.UtcNow.AddSeconds(12);
        bool mp3 = ModeMp3.IsChecked == true;
        Spectrum.Mode = mp3 ? SpectrumMode.Mp3 : SpectrumMode.Lossless;
        MaxFreqRun.Text = mp3 ? "16,229 Hz" : "21,480 Hz";
        string k = mp3 ? "Fail" : "Pass";
        VerdictPill.Background = (Brush)Application.Current.Resources[$"Badge{k}Background"];
        VerdictPill.BorderBrush = (Brush)Application.Current.Resources[$"Badge{k}Border"];
        var fg = (Brush)Application.Current.Resources[$"Badge{k}Foreground"];
        VerdictGlyph.Foreground = fg; VerdictText.Foreground = fg;
        VerdictGlyph.Glyph = mp3 ? "" : "";
        VerdictText.Text = mp3 ? "Below 20 kHz, check" : "Reaches 20 kHz+";
    }

    private void Tile_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is string tag) App.MainWindow?.OpenModule(tag);
    }

    private void OpenAnalyzer_Click(object sender, RoutedEventArgs e) => App.MainWindow?.OpenModule("analyzer");
}
