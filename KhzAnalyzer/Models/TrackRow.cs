using Microsoft.UI;
using Microsoft.UI.Xaml.Media;
using KhzAnalyzer.Core;

namespace KhzAnalyzer.Models;

/// <summary>
/// Uma linha da tabela de resultados, já com os valores formatados para exibição.
/// Expõe o pincel de cor direto para evitar a necessidade de conversores no XAML.
/// </summary>
public sealed class TrackRow
{
    public string CaminhoCompleto { get; init; } = "";
    public string Faixa { get; init; } = "";
    public string Key { get; init; } = "";
    public string Bpm { get; init; } = "";
    public string Formato { get; init; } = "";
    public string SampleRate { get; init; } = "";
    public string Bits { get; init; } = "";
    public string FreqMax { get; init; } = "";
    public string Veredito { get; init; } = "";
    public string StatusGlyph { get; init; } = "";
    public SolidColorBrush StatusBrush { get; init; } = new(Colors.Gray);
    public StatusFaixa Status { get; init; }

    public static TrackRow FromResult(AnalysisResult r)
    {
        var (key, bpm, faixa) = TrackNameParser.Parse(r.Arquivo);
        string formato = System.IO.Path.GetExtension(r.Arquivo).TrimStart('.').ToUpperInvariant();

        if (r.Erro == "Arquivo na nuvem (não baixado)")
        {
            return new TrackRow
            {
                CaminhoCompleto = r.Arquivo,
                Faixa = faixa,
                Key = string.IsNullOrEmpty(key) ? "—" : key,
                Bpm = string.IsNullOrEmpty(bpm) ? "—" : bpm,
                Formato = formato,
                SampleRate = "—",
                Bits = "—",
                FreqMax = "—",
                Veredito = r.Erro,
                StatusGlyph = "\uE753",
                StatusBrush = new SolidColorBrush(ColorHelper.FromArgb(255, 0x8B, 0x8B, 0x8B)),
                Status = StatusFaixa.Offline,
            };
        }

        if (r.Erro is not null)
        {
            return new TrackRow
            {
                CaminhoCompleto = r.Arquivo,
                Faixa = faixa,
                Key = string.IsNullOrEmpty(key) ? "—" : key,
                Bpm = string.IsNullOrEmpty(bpm) ? "—" : bpm,
                Formato = formato,
                SampleRate = "—",
                Bits = "—",
                FreqMax = "—",
                Veredito = r.Erro,
                StatusGlyph = "\uEA39",
                StatusBrush = new SolidColorBrush(ColorHelper.FromArgb(255, 0xF5, 0x9E, 0x0B)),
                Status = StatusFaixa.Erro,
            };
        }

        bool ok = r.EhOk;
        return new TrackRow
        {
            CaminhoCompleto = r.Arquivo,
            Faixa = faixa,
            Key = string.IsNullOrEmpty(key) ? "—" : key,
            Bpm = string.IsNullOrEmpty(bpm) ? "—" : bpm,
            Formato = formato,
            SampleRate = $"{r.SampleRate:N0} Hz",
            Bits = $"{r.Bits} bit",
            FreqMax = $"{r.FreqMaximaDetectada:N0} Hz",
            Veredito = ok ? "Chega em 20 kHz+" : r.Veredito,
            StatusGlyph = ok ? "\uE73E" : "\uE7BA",
            StatusBrush = ok
                ? new SolidColorBrush(ColorHelper.FromArgb(255, 0x2E, 0xA0, 0x43))
                : new SolidColorBrush(ColorHelper.FromArgb(255, 0xE0, 0x4A, 0x3F)),
            Status = ok ? StatusFaixa.Ok : StatusFaixa.Abaixo,
        };
    }
}

public enum StatusFaixa
{
    Ok,
    Abaixo,
    Erro,
    Offline
}
