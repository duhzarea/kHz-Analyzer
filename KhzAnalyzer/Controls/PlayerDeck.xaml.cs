using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using KhzAnalyzer.Services;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.UI;

namespace KhzAnalyzer.Controls;

/// <summary>Now Playing deck: title, format line, mirrored waveform, red scrubber, transport and volume.</summary>
public sealed partial class PlayerDeck : UserControl
{
    private const int Bars = 72;
    private static readonly SolidColorBrush Played = new(Color.FromArgb(0xF2, 0xFF, 0xFF, 0xFF));
    private static readonly SolidColorBrush Unplayed = new(Color.FromArgb(0x52, 0xFF, 0xFF, 0xFF));
    private readonly List<Rectangle> _bars = new();
    private float[] _env = new float[Bars];
    private readonly DispatcherQueueTimer _tick;
    private readonly Player _p = Player.Instance;
    private int _lastPlayed = -1;

    public PlayerDeck()
    {
        InitializeComponent();
        for (int i = 0; i < Bars; i++)
        {
            var r = new Rectangle { RadiusX = 1, RadiusY = 1, Fill = Unplayed };
            _bars.Add(r); Wave.Children.Add(r);
            _env[i] = 0.06f;
        }
        Wave.SizeChanged += (_, _) => LayoutWave();
        Scrub.Seeking += (v, final) => _p.Seek(TimeSpan.FromSeconds(v * _p.Duration.TotalSeconds));
        _tick = DispatcherQueue.CreateTimer();
        _tick.Interval = TimeSpan.FromMilliseconds(100);
        _tick.Tick += (_, _) => UpdatePosition();
        _p.TrackChanged += OnTrackChanged;
        _p.StateChanged += OnStateChanged;
        _p.Volume = VolumeSlider.Value / 100.0;
        Loaded += (_, _) => { _tick.Start(); OnTrackChanged(); };
        Unloaded += (_, _) => _tick.Stop();
    }

    private static string Fmt(TimeSpan t) =>
        t.TotalHours >= 1 ? t.ToString(@"h\:mm\:ss", CultureInfo.InvariantCulture) : t.ToString(@"mm\:ss", CultureInfo.InvariantCulture);

    private async void OnTrackChanged()
    {
        var row = _p.Current;
        if (row is null)
        {
            TitleText.Text = "Nothing playing";
            MetaText.Text = "Double-click a track, or press Space";
            for (int i = 0; i < Bars; i++) _env[i] = 0.06f;
            LayoutWave(); UpdatePosition(); OnStateChanged();
            return;
        }
        TitleText.Text = row.Faixa;
        string sr = row.SampleRate.Replace(",000 Hz", " kHz").Replace(",100 Hz", ".1 kHz").Replace(",050 Hz", ".05 kHz");
        MetaText.Text = $"{row.Formato} · {sr} · {row.Bits} · {row.FreqMax}";
        ToolTipService.SetToolTip(TitleText, row.Faixa);
        string path = row.CaminhoCompleto;
        try
        {
            var env = await Task.Run(() => { using var f = new PcmFile(path); return f.Envelope(Bars); });
            if (_p.Current?.CaminhoCompleto == path) { _env = env; _lastPlayed = -1; LayoutWave(); }
        }
        catch { /* the waveform is decoration; playback reports its own errors */ }
        OnStateChanged();
    }

    private void OnStateChanged()
    {
        bool playing = _p.IsPlaying;
        PlayGlyph.Glyph = playing ? "" : "";
        PlayGlyph.Margin = new Thickness(playing ? 0 : 2, 0, 0, 0);
        AutomationPropertiesHelper(playing);
        Halo.Opacity = playing ? 0.55 : 0.28;
    }

    private void AutomationPropertiesHelper(bool playing) =>
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(PlayButton, playing ? "Pause" : "Play");

    private void UpdatePosition()
    {
        double dur = _p.Duration.TotalSeconds;
        var pos = _p.Position;
        double k = dur > 0 ? Math.Clamp(pos.TotalSeconds / dur, 0, 1) : 0;
        if (!Scrub.IsDragging) Scrub.Value = k;
        else k = Scrub.Value;
        ElapsedText.Text = Fmt(TimeSpan.FromSeconds(k * dur));
        RemainingText.Text = "-" + Fmt(TimeSpan.FromSeconds(Math.Max(0, dur - k * dur)));
        int played = (int)Math.Floor(k * Bars);
        if (played != _lastPlayed)
        {
            for (int i = 0; i < Bars; i++) _bars[i].Fill = i < played ? Played : Unplayed;
            _lastPlayed = played;
        }
    }

    private void LayoutWave()
    {
        double w = Wave.ActualWidth, h = Wave.ActualHeight;
        if (w <= 0 || h <= 0) return;
        double slot = w / Bars, bw = Math.Max(1.5, slot * 0.45), mid = h / 2;
        for (int i = 0; i < Bars; i++)
        {
            double bh = Math.Max(2, _env[i] * (h - 4));
            var r = _bars[i];
            r.Width = bw; r.Height = bh;
            Canvas.SetLeft(r, i * slot + (slot - bw) / 2);
            Canvas.SetTop(r, mid - bh / 2);
        }
    }

    private void PlayButton_Click(object sender, RoutedEventArgs e) => _p.Toggle();
    private void PrevButton_Click(object sender, RoutedEventArgs e) => _p.Previous();
    private void NextButton_Click(object sender, RoutedEventArgs e) => _p.Next();
    private void RepeatButton_Click(object sender, RoutedEventArgs e) => _p.Repeat = RepeatButton.IsChecked == true;

    private void VolumeSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        _p.Volume = e.NewValue / 100.0;
        if (VolumeGlyph is not null) VolumeGlyph.Glyph = e.NewValue <= 0 ? "" : "";
    }
}
