using System;
using System.IO;
using System.Numerics;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using KhzAnalyzer.Core;
using KhzAnalyzer.Core.Media;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Shapes;
using Windows.UI;
using IOPath = System.IO.Path;

namespace KhzAnalyzer.Utils;

/// <summary>
/// Full-length spectrogram, Spek-style: time across, frequency up, loudness as colour.
/// The ramp is a scope palette (deep blue → cyan → green → yellow → red), not Spek's pink.
/// </summary>
public static class SpectrogramDialog
{
    private const int Window = 2048;        // 2048-point Hann, ~21 Hz bins at 44.1 kHz
    private const int MaxColumns = 1400;    // one column per screen pixel is plenty
    private const double FloorDb = -120.0;  // bottom of the colour ramp and of the dB ruler
    private const int PlotHeight = 420;

    public static readonly string[] Extensions = { ".wav", ".wave", ".aif", ".aiff", ".aifc", ".flac", ".mp3", ".m4a" };

    public static bool Handles(string path) =>
        Array.IndexOf(Extensions, IOPath.GetExtension(path).ToLowerInvariant()) >= 0;

    public static async Task ShowAsync(XamlRoot root, string path)
    {
        var header = new TextBlock
        {
            FontFamily = (FontFamily)Application.Current.Resources["MonoFontFamily"],
            FontSize = 12.5,
            Foreground = (Brush)Application.Current.Resources["InkMutedBrush"],
            Text = "Reading the whole track…",
        };

        var plot = new Image { Stretch = Stretch.Fill, Height = PlotHeight };
        var overlay = new Grid { Height = PlotHeight };
        overlay.Children.Add(plot);

        var body = new Grid { MinWidth = 980, RowSpacing = 10 };
        body.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        body.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        body.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Grid.SetRow(header, 0);
        body.Children.Add(header);

        var chart = new Grid { ColumnSpacing = 8 };
        chart.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });  // kHz ruler
        chart.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        chart.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });  // dB ruler
        Grid.SetRow(chart, 1);
        Grid.SetColumn(overlay, 1);
        chart.Children.Add(overlay);
        body.Children.Add(chart);

        var times = new Grid();
        Grid.SetRow(times, 2);
        body.Children.Add(times);

        var dialog = new ContentDialog
        {
            XamlRoot = root,
            Title = IOPath.GetFileName(path),
            Content = new ScrollViewer { Content = body, HorizontalScrollMode = ScrollMode.Disabled },
            CloseButtonText = "Close",
            DefaultButton = ContentDialogButton.Close,
        };
        dialog.Resources["ContentDialogMaxWidth"] = 1400.0;   // the default 540 clips the dB ruler
        var showing = dialog.ShowAsync();

        try
        {
            var job = await Task.Run(() => Render(path, CancellationToken.None));

            var bmp = new WriteableBitmap(job.Columns, job.Bins);
            using (var s = bmp.PixelBuffer.AsStream()) s.Write(job.Pixels, 0, job.Pixels.Length);
            bmp.Invalidate();
            plot.Source = bmp;

            header.Text = job.Header;
            BuildFrequencyRuler(chart, job.Nyquist);
            BuildDecibelRuler(chart);
            BuildTimeRuler(times, job.Seconds);
        }
        catch (Exception ex)
        {
            header.Text = $"This file cannot be read: {ex.Message}";
        }

        await showing;
    }

    // ------------------------------------------------------------ render --

    private sealed record Job(byte[] Pixels, int Columns, int Bins, double Nyquist, double Seconds, string Header);

    private static Job Render(string path, CancellationToken ct)
    {
        var (mono, rate, bits, codec) = Load(path, ct);
        if (mono.Length < Window) throw new InvalidDataException("The track is too short to plot.");

        int bins = Window / 2;
        int columns = Math.Clamp(mono.Length / (Window / 2), 200, MaxColumns);
        int hop = Math.Max(1, (mono.Length - Window) / Math.Max(1, columns - 1));
        double[] win = Fft.HannWindow(Window);

        var pixels = new byte[columns * bins * 4];
        var buffer = new Complex[Window];
        double maxHz = 0;

        for (int col = 0; col < columns; col++)
        {
            ct.ThrowIfCancellationRequested();
            long start = (long)col * hop;

            for (int i = 0; i < Window; i++)
            {
                long k = start + i;
                buffer[i] = new Complex(k < mono.Length ? mono[k] * win[i] : 0, 0);
            }
            Fft.Transform(buffer);

            for (int bin = 0; bin < bins; bin++)
            {
                double mag = buffer[bin].Magnitude * 2.0 / Window;
                double db = 20.0 * Math.Log10(mag + 1e-12);
                double t = Math.Clamp((db - FloorDb) / -FloorDb, 0, 1);

                if (t > 0.30) maxHz = Math.Max(maxHz, (double)bin * rate / Window);

                var (r, g, b) = Scope(t);
                int row = bins - 1 - bin;                  // 0 Hz at the bottom
                int o = (row * columns + col) * 4;
                pixels[o] = b; pixels[o + 1] = g; pixels[o + 2] = r; pixels[o + 3] = 255;
            }
        }

        double seconds = (double)mono.Length / rate;
        string head = string.Create(System.Globalization.CultureInfo.InvariantCulture,
            $"{codec}  ·  {rate:N0} Hz  ·  {(bits > 0 ? bits + "-bit" : "float")}  ·  content up to {maxHz:N0} Hz  ·  {TimeSpan.FromSeconds(seconds):m\\:ss}");

        return new Job(pixels, columns, bins, rate / 2.0, seconds, head);
    }

    /// <summary>WAV and AIFF are read straight; everything else goes through FFmpeg into a temp WAV.</summary>
    private static (float[] mono, int rate, int bits, string codec) Load(string path, CancellationToken ct)
    {
        string ext = IOPath.GetExtension(path).ToLowerInvariant();
        string codec = ext.TrimStart('.').ToUpperInvariant();

        if (ext is ".wav" or ".wave" or ".aif" or ".aiff" or ".aifc")
        {
            var direct = WavAiffReader.Read(path);
            return (direct.Samples, direct.SampleRate, direct.Bits, codec);
        }

        string? ffmpeg = MediaTools.Ffmpeg
            ?? throw new FileNotFoundException("FFmpeg is needed to decode this format and was not found.");

        string temp = IOPath.Combine(IOPath.GetTempPath(), $"khz-spec-{Guid.NewGuid():N}.wav");
        try
        {
            int code = MediaTools.RunAsync(
                ffmpeg,
                new[] { "-v", "error", "-y", "-i", path, "-ac", "1", "-c:a", "pcm_s16le", temp },
                null, null, ct).GetAwaiter().GetResult();
            if (code != 0 || !File.Exists(temp)) throw new InvalidDataException("FFmpeg could not decode this file.");

            var decoded = WavAiffReader.Read(temp);
            return (decoded.Samples, decoded.SampleRate, 16, codec);
        }
        finally
        {
            try { if (File.Exists(temp)) File.Delete(temp); } catch { }
        }
    }

    /// <summary>Scope ramp: near-black → deep blue → cyan → neon green → yellow → red at 0 dB.</summary>
    private static (byte r, byte g, byte b) Scope(double t)
    {
        ReadOnlySpan<double> stops = stackalloc double[] { 0.00, 0.16, 0.34, 0.50, 0.64, 0.78, 0.90, 1.00 };
        (byte r, byte g, byte b)[] ramp =
        {
            (4, 7, 16), (10, 26, 78), (16, 92, 200), (22, 206, 240),
            (43, 240, 138), (190, 245, 40), (255, 150, 30), (255, 43, 43),
        };

        for (int i = 1; i < stops.Length; i++)
        {
            if (t > stops[i] && i < stops.Length - 1) continue;
            double f = (t - stops[i - 1]) / (stops[i] - stops[i - 1]);
            f = Math.Clamp(f, 0, 1);
            var a = ramp[i - 1]; var b = ramp[i];
            return ((byte)(a.r + (b.r - a.r) * f), (byte)(a.g + (b.g - a.g) * f), (byte)(a.b + (b.b - a.b) * f));
        }
        return ramp[^1];
    }

    // ------------------------------------------------------------- rulers --

    private static TextBlock Tick(string text) => new()
    {
        Text = text,
        FontFamily = (FontFamily)Application.Current.Resources["MonoFontFamily"],
        FontSize = 11,
        Foreground = (Brush)Application.Current.Resources["InkFaintBrush"],
    };

    private static void BuildFrequencyRuler(Grid chart, double nyquist)
    {
        var panel = new Grid { Width = 54, Height = PlotHeight };
        for (int khz = 0; khz <= (int)(nyquist / 1000); khz += 4)
        {
            var label = Tick($"{khz} kHz");
            label.HorizontalAlignment = HorizontalAlignment.Right;
            label.VerticalAlignment = VerticalAlignment.Top;
            double y = (1 - khz * 1000 / nyquist) * PlotHeight;
            label.Margin = new Thickness(0, Math.Clamp(y - 7, 0, PlotHeight - 14), 0, 0);
            panel.Children.Add(label);
        }
        Grid.SetColumn(panel, 0);
        chart.Children.Add(panel);
    }

    private static void BuildDecibelRuler(Grid chart)
    {
        var strip = new Rectangle
        {
            Width = 10,
            Height = PlotHeight,
            RadiusX = 5,
            RadiusY = 5,
            Fill = RampBrush(),
        };

        var labels = new Grid { Width = 58, Height = PlotHeight };
        for (int db = 0; db >= -120; db -= 30)
        {
            var label = Tick($"{db} dB");
            label.VerticalAlignment = VerticalAlignment.Top;
            double y = -db / 120.0 * PlotHeight;
            label.Margin = new Thickness(0, Math.Clamp(y - 7, 0, PlotHeight - 14), 0, 0);
            labels.Children.Add(label);
        }

        var holder = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        holder.Children.Add(strip);
        holder.Children.Add(labels);
        Grid.SetColumn(holder, 2);
        chart.Children.Add(holder);
    }

    private static LinearGradientBrush RampBrush()
    {
        var brush = new LinearGradientBrush { StartPoint = new Windows.Foundation.Point(0, 0), EndPoint = new Windows.Foundation.Point(0, 1) };
        for (int i = 10; i >= 0; i--)
        {
            var (r, g, b) = Scope(i / 10.0);
            brush.GradientStops.Add(new GradientStop { Offset = (10 - i) / 10.0, Color = Color.FromArgb(255, r, g, b) });
        }
        return brush;
    }

    private static void BuildTimeRuler(Grid times, double seconds)
    {
        times.Margin = new Thickness(62, 0, 68, 0);
        const int slices = 6;
        for (int i = 0; i < slices; i++)
            times.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        for (int i = 0; i <= slices; i++)
        {
            var label = Tick(TimeSpan.FromSeconds(seconds * i / slices).ToString(@"m\:ss"));
            label.HorizontalAlignment = i == slices ? HorizontalAlignment.Right : HorizontalAlignment.Left;
            Grid.SetColumn(label, Math.Min(i, slices - 1));
            times.Children.Add(label);
        }
    }
}

