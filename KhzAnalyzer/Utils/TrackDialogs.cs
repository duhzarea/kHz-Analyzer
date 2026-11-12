using System;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using KhzAnalyzer.Controls;
using KhzAnalyzer.Core;
using KhzAnalyzer.Models;
using KhzAnalyzer.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace KhzAnalyzer.Utils;

/// <summary>Track information and full-analysis dialogs (solid surfaces, so nothing behind bleeds through).</summary>
public static class TrackDialogs
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    private static ContentDialog NewDialog(XamlRoot root, string title)
    {
        var d = new ContentDialog
        {
            XamlRoot = root,
            Title = title,
            CloseButtonText = "Close",
            DefaultButton = ContentDialogButton.Close,
            RequestedTheme = App.MainWindow?.IsDark == true ? ElementTheme.Dark : ElementTheme.Light,
        };
        d.Resources["ContentDialogMaxWidth"] = 720d;
        return d;
    }

    private static TextBlock Label(string t) => new()
    {
        Text = t, Opacity = 0.7, Margin = new Thickness(0, 0, 18, 0),
    };

    private static TextBlock Value(string t, bool mono = true) => new()
    {
        Text = t, TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = true,
        FontFamily = mono ? (FontFamily)Application.Current.Resources["MonoFontFamily"] : FontFamily.XamlAutoFontFamily,
    };

    public static async Task ShowInfoAsync(XamlRoot root, TrackRow row)
    {
        string canais = "—", duracao = "—", tamanho = "—";
        await Task.Run(() =>
        {
            try { tamanho = $"{new FileInfo(row.CaminhoCompleto).Length / 1048576.0:0.0} MB".Replace(',', '.'); } catch { }
            try
            {
                var m = WavAiffReader.GetMetadata(row.CaminhoCompleto);
                canais = m.Channels switch { 1 => "1 (mono)", 2 => "2 (stereo)", _ => m.Channels.ToString(Inv) };
                var t = TimeSpan.FromSeconds((double)m.TotalFrames / Math.Max(1, m.SampleRate));
                duracao = t.TotalHours >= 1 ? t.ToString(@"h\:mm\:ss", Inv) : t.ToString(@"m\:ss", Inv);
            }
            catch { }
        });

        var grid = new Grid { RowSpacing = 8, MinWidth = 520 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        (string, string, bool)[] rows =
        {
            ("Track", row.Faixa, false),
            ("Key / BPM", $"{row.Key} / {row.Bpm}", true),
            ("Format", row.Formato, true),
            ("Sample rate", row.SampleRate, true),
            ("Bit depth", row.Bits, true),
            ("Channels", canais, true),
            ("Duration", duracao, true),
            ("Size", tamanho, true),
            ("Max freq.", row.FreqMax, true),
            ("Verdict", row.Veredito, false),
            ("Detail", row.Detalhe, false),
            ("File", row.CaminhoCompleto, false),
        };
        for (int i = 0; i < rows.Length; i++)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var l = Label(rows[i].Item1); Grid.SetRow(l, i);
            var v = Value(rows[i].Item2, rows[i].Item3); Grid.SetRow(v, i); Grid.SetColumn(v, 1);
            if (rows[i].Item1 == "Verdict") v.Foreground = row.BadgeForeground;
            grid.Children.Add(l); grid.Children.Add(v);
        }
        var d = NewDialog(root, "Track information");
        d.Content = new ScrollViewer { Content = grid, MaxHeight = 520 };
        d.PrimaryButtonText = "Play";
        d.PrimaryButtonClick += (_, _) => _ = Player.Instance.PlayAsync(row);
        await d.ShowAsync();
    }

    public static async Task ShowAnalysisAsync(XamlRoot root, TrackRow row)
    {
        var view = new SpectrumView { Live = false, Height = 260, MarkerHz = Verdict.ThresholdHz };
        var summary = new TextBlock { TextWrapping = TextWrapping.Wrap, Opacity = 0.85 };
        var panel = new StackPanel { Spacing = 14, MinWidth = 600 };
        var card = new Border
        {
            Padding = new Thickness(18, 16, 18, 10), CornerRadius = new CornerRadius(16),
            Background = (Brush)Application.Current.Resources["DeckBrush"], RequestedTheme = ElementTheme.Dark, Child = view,
        };
        panel.Children.Add(card);
        panel.Children.Add(summary);
        var d = NewDialog(root, "Full analysis");
        d.Content = panel;
        summary.Text = "Measuring…";
        var show = d.ShowAsync();

        try
        {
            if (row.Status is StatusFaixa.Erro or StatusFaixa.Offline) throw new InvalidOperationException(row.Detalhe);
            var (levels, nyquist) = await Task.Run(() => Measure(row.CaminhoCompleto, 96));
            view.CutoffHz = row.FreqMaxHz > 0 ? row.FreqMaxHz : nyquist;
            view.SetLevels(levels, nyquist);
            view.Mode = row.Kind is VerdictKind.Check or VerdictKind.Mp3Range ? SpectrumMode.Mp3 : SpectrumMode.Lossless;
            summary.Text = string.Create(Inv,
                $"Average spectrum of the middle of the track (up to 30 s, Welch, 8192-point Hann). Sustained content measured up to {row.FreqMaxHz:N0} Hz; the threshold is {Verdict.ThresholdHz:N0} Hz. {row.Veredito}. The verdict comes from the full-length scan, not from this preview.");
        }
        catch (Exception ex)
        {
            summary.Text = $"This file cannot be measured: {ex.Message}";
        }
        await show;
    }

    /// <summary>Welch average of up to 30 s from the middle of the file, in <paramref name="bars"/> bands, scaled 0..1 over 90 dB.</summary>
    private static (double[] levels, double nyquist) Measure(string path, int bars)
    {
        using var f = new PcmFile(path);
        int sr = f.Meta.SampleRate;
        long total = f.Meta.TotalFrames;
        int frames = (int)Math.Min(total, sr * 30L);
        long start = Math.Max(0, total / 2 - frames / 2);
        var st = f.ReadFrames(start, frames, out int got);
        var mono = new float[got];
        for (int i = 0; i < got; i++) mono[i] = 0.5f * (st[i * 2] + st[i * 2 + 1]);
        var (freqs, psd) = Fft.Welch(mono, sr, 8192);
        double nyq = sr / 2.0;
        var lv = new double[bars];
        var cnt = new int[bars];
        for (int i = 1; i < freqs.Length; i++)
        {
            int b = Math.Min(bars - 1, (int)(freqs[i] / nyq * bars));
            lv[b] += psd[i]; cnt[b]++;
        }
        double max = double.MinValue;
        for (int b = 0; b < bars; b++) { lv[b] = 10 * Math.Log10(cnt[b] > 0 ? lv[b] / cnt[b] + 1e-30 : 1e-30); max = Math.Max(max, lv[b]); }
        for (int b = 0; b < bars; b++) lv[b] = Math.Clamp((lv[b] - (max - 90)) / 90.0, 0, 1);
        return (lv, nyq);
    }
}
