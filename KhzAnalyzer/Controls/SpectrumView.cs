using System;
using System.Collections.Generic;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.UI;

namespace KhzAnalyzer.Controls;

/// <summary>
/// Bar spectrum with a 20 kHz marker. Live mode animates a plausible spectrum: full band in
/// <see cref="SpectrumMode.Lossless"/>, and a hard cut (shaded, red edge) in <see cref="SpectrumMode.Mp3"/>,
/// the shelf a transcode leaves. Static mode draws real magnitudes (0..1) from <see cref="SetLevels"/>.
/// </summary>
public enum SpectrumMode { Lossless, Mp3 }

public sealed class SpectrumView : Grid
{
    private readonly Canvas _canvas = new();
    private readonly Rectangle _shade = new() { Opacity = 0 };
    private readonly Rectangle _edge = new() { Width = 2, Opacity = 0 };
    private readonly Line _marker = new() { StrokeThickness = 1.2, StrokeDashArray = new DoubleCollection { 3, 3 } };
    private readonly TextBlock _markerLabel = new() { Text = "20 kHz", FontSize = 11 };
    private readonly List<Rectangle> _bars = new();
    private readonly Grid _axis = new() { Height = 18 };
    private double[] _h = Array.Empty<double>();
    private double[]? _static;
    private DispatcherQueueTimer? _timer;
    private readonly Random _rnd = new(7);
    private double _t;

    public int BarCount { get; set; } = 64;
    public double MaxHz { get; set; } = 22050;
    public double CutoffHz { get; set; } = 16229;
    public double MarkerHz { get; set; } = 20000;
    public bool Live { get; set; } = true;

    private SpectrumMode _mode = SpectrumMode.Lossless;
    public SpectrumMode Mode { get => _mode; set { _mode = value; UpdateOverlay(true); } }

    public SpectrumView()
    {
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _shade.Fill = new SolidColorBrush(Color.FromArgb(0x2E, 0xFA, 0x24, 0x3C));
        _edge.Fill = new SolidColorBrush(Color.FromArgb(0xFF, 0xFA, 0x24, 0x3C));
        _marker.Stroke = new SolidColorBrush(Color.FromArgb(0xCC, 0xFF, 0x8A, 0x9B));
        _markerLabel.Foreground = new SolidColorBrush(Color.FromArgb(0xFF, 0xFF, 0x8A, 0x9B));
        _canvas.Children.Add(_shade);
        Children.Add(_canvas);
        SetRow(_axis, 1);
        Children.Add(_axis);
        Loaded += (_, _) =>
        {
            Build();
            if (_static is not null) { for (int i = 0; i < _h.Length && i < _static.Length; i++) _h[i] = _static[i]; Layout(); }
            UpdateOverlay(false);
            if (Live) Start();
        };
        Unloaded += (_, _) => _timer?.Stop();
        SizeChanged += (_, _) => Layout();
        _canvas.SizeChanged += (_, _) => Layout();
    }

    /// <summary>Static mode: real levels (0..1) from low to high frequency.</summary>
    public void SetLevels(double[] levels, double maxHz)
    {
        Live = false; _timer?.Stop();
        _static = levels; MaxHz = maxHz; BarCount = levels.Length;
        Build();
        for (int i = 0; i < _h.Length; i++) _h[i] = levels[i];
        Layout();
    }

    private static Color SpecColor(double k)
    {
        // cyan → green → yellow → orange → red, the design system's spectrum ramp
        (double p, Color c)[] stops =
        {
            (0, Color.FromArgb(255, 0x22, 0xD3, 0xEE)), (0.32, Color.FromArgb(255, 0x4A, 0xDE, 0x80)),
            (0.55, Color.FromArgb(255, 0xFA, 0xCC, 0x15)), (0.78, Color.FromArgb(255, 0xFB, 0x92, 0x3C)),
            (1, Color.FromArgb(255, 0xF0, 0x38, 0x4E)),
        };
        for (int i = 1; i < stops.Length; i++)
            if (k <= stops[i].p)
            {
                double f = (k - stops[i - 1].p) / (stops[i].p - stops[i - 1].p);
                Color a = stops[i - 1].c, b = stops[i].c;
                return Color.FromArgb(255, (byte)(a.R + (b.R - a.R) * f), (byte)(a.G + (b.G - a.G) * f), (byte)(a.B + (b.B - a.B) * f));
            }
        return stops[^1].c;
    }

    private void Build()
    {
        foreach (var b in _bars) _canvas.Children.Remove(b);
        _bars.Clear();
        _h = new double[BarCount];
        for (int i = 0; i < BarCount; i++)
        {
            var r = new Rectangle { RadiusX = 1.5, RadiusY = 1.5, Fill = new SolidColorBrush(SpecColor((double)i / Math.Max(1, BarCount - 1))) };
            _bars.Add(r); _canvas.Children.Add(r);
            _h[i] = 0.05;
        }
        _canvas.Children.Remove(_edge); _canvas.Children.Add(_edge);
        _canvas.Children.Remove(_marker); _canvas.Children.Add(_marker);
        _canvas.Children.Remove(_markerLabel); _canvas.Children.Add(_markerLabel);

        _axis.Children.Clear();
        _axis.ColumnDefinitions.Clear();
        foreach (var hz in new[] { 0, 5000, 10000, 15000, 20000 })
        {
            if (hz > MaxHz) break;
            var t = new TextBlock
            {
                Text = hz == 0 ? "0" : $"{hz / 1000}k", FontSize = 11, Opacity = 0.6,
                FontFamily = (FontFamily)Application.Current.Resources["MonoFontFamily"],
                HorizontalAlignment = HorizontalAlignment.Left, Tag = hz,
            };
            _axis.Children.Add(t);
        }
        Layout();
    }

    private void Layout()
    {
        double w = _canvas.ActualWidth, h = _canvas.ActualHeight;
        if (w <= 0 || h <= 0 || _bars.Count == 0) return;
        double slot = w / _bars.Count, bw = Math.Max(1.5, slot * 0.62);
        for (int i = 0; i < _bars.Count; i++)
        {
            double bh = Math.Max(2, _h[i] * (h - 16));
            _bars[i].Width = bw; _bars[i].Height = bh;
            Canvas.SetLeft(_bars[i], i * slot + (slot - bw) / 2);
            Canvas.SetTop(_bars[i], h - bh);
        }
        double mx = w * Math.Min(1, MarkerHz / MaxHz);
        _marker.X1 = _marker.X2 = mx; _marker.Y1 = 14; _marker.Y2 = h;
        _markerLabel.Visibility = MarkerHz <= MaxHz ? Visibility.Visible : Visibility.Collapsed;
        _marker.Visibility = _markerLabel.Visibility;
        Canvas.SetLeft(_markerLabel, Math.Max(0, mx - 46)); Canvas.SetTop(_markerLabel, -2);
        double cx = w * Math.Min(1, CutoffHz / MaxHz);
        Canvas.SetLeft(_shade, cx); Canvas.SetTop(_shade, 0); _shade.Width = Math.Max(0, w - cx); _shade.Height = h;
        Canvas.SetLeft(_edge, cx - 1); Canvas.SetTop(_edge, 0); _edge.Height = h;
        foreach (var child in _axis.Children)
            if (child is TextBlock t && t.Tag is int hz)
                t.Margin = new Thickness(Math.Min(w - 26, w * hz / MaxHz), 2, 0, 0);
    }

    private void UpdateOverlay(bool animate)
    {
        double o = _mode == SpectrumMode.Mp3 ? 1 : 0;
        _shade.Opacity = o; _edge.Opacity = o;
    }

    private void Start()
    {
        _timer ??= DispatcherQueue.CreateTimer();
        _timer.Interval = TimeSpan.FromMilliseconds(45);
        _timer.Tick -= Tick; _timer.Tick += Tick;
        _timer.Start();
        UpdateOverlay(false);
    }

    private void Tick(DispatcherQueueTimer s, object e)
    {
        _t += 0.045;
        int n = _h.Length;
        for (int i = 0; i < n; i++)
        {
            double f = (i + 0.5) / n * MaxHz, k = f / MaxHz;
            double env = 0.92 - 0.42 * k + 0.08 * Math.Sin(k * 9 + _t * 0.7);
            double wobble = 0.16 * Math.Sin(_t * (2.1 + i % 5 * 0.37) + i * 0.9) + 0.10 * (_rnd.NextDouble() - 0.5);
            double target = Math.Clamp(env + wobble, 0.06, 1);
            if (_mode == SpectrumMode.Lossless && f > 21500) target *= 0.35;
            if (_mode == SpectrumMode.Mp3 && f > CutoffHz) target = 0.015 + 0.01 * _rnd.NextDouble();
            _h[i] += (target - _h[i]) * 0.28;
        }
        Layout();
    }
}
