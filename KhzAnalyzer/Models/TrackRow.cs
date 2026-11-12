using Microsoft.UI;
using Microsoft.UI.Xaml.Media;
using KhzAnalyzer.Core;

namespace KhzAnalyzer.Models;

/// <summary>
/// One row of the results table, formatted for display. The verdict comes from
/// <see cref="Verdict"/> (20 kHz rule): anything below 20 kHz carries the attention glyph.
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
    public double FreqMaxHz { get; init; }
    public string Veredito { get; init; } = "";
    public string Detalhe { get; init; } = "";
    public string StatusGlyph { get; init; } = "";
    public SolidColorBrush StatusBrush { get; init; } = new(Colors.Gray);
    public StatusFaixa Status { get; init; }
    public VerdictKind Kind { get; init; }

    /// <summary>True for every verdict that needs the user to look at the file (all ⚠ rows and errors).</summary>
    public bool PrecisaVerificar => Status is StatusFaixa.Abaixo or StatusFaixa.Mp3 or StatusFaixa.Erro;

    private static readonly System.Globalization.CultureInfo Inv = System.Globalization.CultureInfo.InvariantCulture;
    private const string GlyphCheck = "", GlyphWarning = "", GlyphError = "", GlyphCloud = "";

    private static bool EhVazio(string caminho)
    {
        try { return new System.IO.FileInfo(caminho).Length == 0; } catch { return false; }
    }

    private static SolidColorBrush Brush(byte r, byte g, byte b) => new(ColorHelper.FromArgb(255, r, g, b));

    public static TrackRow FromResult(AnalysisResult r)
    {
        var (key, bpm, faixa) = TrackNameParser.Parse(r.Arquivo);
        string formato = System.IO.Path.GetExtension(r.Arquivo).TrimStart('.').ToUpperInvariant();
        var kind = r.Kind;
        bool measured = kind is VerdictKind.Pass or VerdictKind.Mp3Range or VerdictKind.Check;

        var (status, glyph, brush) = kind switch
        {
            VerdictKind.Pass => (StatusFaixa.Ok, GlyphCheck, Brush(0x2E, 0xA0, 0x43)),
            VerdictKind.Mp3Range => (StatusFaixa.Mp3, GlyphWarning, Brush(0xF5, 0x9E, 0x0B)),
            VerdictKind.Check => (StatusFaixa.Abaixo, GlyphWarning, Brush(0xEF, 0x44, 0x44)),
            VerdictKind.Unreadable => (StatusFaixa.Erro, GlyphError, Brush(0xF5, 0x9E, 0x0B)),
            _ => (StatusFaixa.Offline, GlyphCloud, Brush(0x94, 0xA3, 0xB8)),
        };

        string detalhe = kind switch
        {
            VerdictKind.Unreadable => EhVazio(r.Arquivo) ? "Empty file (0 bytes)" : $"Could not read: {r.Erro}",
            VerdictKind.NotDownloaded => "Cloud-only file, not on this computer",
            _ => string.Create(Inv, $"Content measured up to {r.FreqMaximaDetectada:N0} Hz"),
        };

        return new TrackRow
        {
            CaminhoCompleto = r.Arquivo,
            Faixa = faixa,
            Key = string.IsNullOrEmpty(key) ? "—" : key,
            Bpm = string.IsNullOrEmpty(bpm) ? "—" : bpm,
            Formato = formato,
            SampleRate = measured ? string.Create(Inv, $"{r.SampleRate:N0} Hz") : "—",
            Bits = measured ? $"{r.Bits} bit" : "—",
            FreqMax = measured ? string.Create(Inv, $"{r.FreqMaximaDetectada:N0} Hz") : "—",
            FreqMaxHz = measured ? r.FreqMaximaDetectada : 0,
            Veredito = Verdict.Label(kind),
            Detalhe = detalhe,
            StatusGlyph = glyph,
            StatusBrush = brush,
            Status = status,
            Kind = kind,
        };
    }
}

public enum StatusFaixa
{
    Ok,
    Mp3,
    Abaixo,
    Erro,
    Offline
}
