using System;
using System.IO;

namespace KhzAnalyzer.Core;

public enum VerdictKind { Pass, Mp3Range, Check, Unreadable, NotDownloaded }

/// <summary>
/// The single place that turns a measurement into a verdict (decided 2026-11-11, replaces the 19 kHz rule):
/// a track passes when its sustained content reaches 20 kHz. Anything below 20 kHz is flagged with ⚠ for
/// checking, never passed silently. The one tolerance: an original MP3 file, whose encoder low-pass
/// legitimately sits around 19–20 kHz, is accepted from 19 kHz and labelled as such.
/// The measured frequency is always reported as an absolute value so the user decides.
/// </summary>
public static class Verdict
{
    public const double ThresholdHz = 20000;
    public const double Mp3ToleranceHz = 19000;
    public const string Attention = "⚠";

    public static VerdictKind Of(AnalysisResult r)
    {
        if (r.IsOffline) return VerdictKind.NotDownloaded;
        if (r.Erro is not null) return VerdictKind.Unreadable;
        if (r.FreqMaximaDetectada >= ThresholdHz) return VerdictKind.Pass;
        if (IsMp3(r.Arquivo) && r.FreqMaximaDetectada >= Mp3ToleranceHz) return VerdictKind.Mp3Range;
        return VerdictKind.Check;
    }

    public static bool IsMp3(string path) => Path.GetExtension(path).Equals(".mp3", StringComparison.OrdinalIgnoreCase);

    public static string Label(VerdictKind k) => k switch
    {
        VerdictKind.Pass => "Reaches 20 kHz+",
        VerdictKind.Mp3Range => Attention + " MP3 range (19 kHz), tolerated",
        VerdictKind.Check => Attention + " Below 20 kHz, check",
        VerdictKind.Unreadable => "Unreadable file",
        _ => "Not downloaded",
    };

    /// <summary>Short label for badges, without the symbol (the badge draws its own glyph).</summary>
    public static string ShortLabel(VerdictKind k) => k switch
    {
        VerdictKind.Pass => "Reaches 20 kHz+",
        VerdictKind.Mp3Range => "MP3 range",
        VerdictKind.Check => "Below 20 kHz",
        VerdictKind.Unreadable => "Unreadable file",
        _ => "Not downloaded",
    };
}
