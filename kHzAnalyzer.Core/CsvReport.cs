using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace KhzAnalyzer.Core;

public static class CsvReport
{
    public static void Generate(string path, IEnumerable<AnalysisResult> results)
    {
        using var sw = new StreamWriter(path, false, new System.Text.UTF8Encoding(true));
        sw.WriteLine("TRACK;KEY;BPM;TYPE;SAMPLE RATE (Hz);BITS;MAX FREQ. (Hz);VERDICT;DETAIL");

        var inv = CultureInfo.InvariantCulture;
        foreach (var r in results.OrderBy(r => TrackNameParser.Parse(r.Arquivo).Item3))
        {
            var (key, bpm, faixa) = TrackNameParser.Parse(r.Arquivo);
            string formato = Path.GetExtension(r.Arquivo).TrimStart('.').ToUpperInvariant();
            bool measured = r.Erro is null;
            // Absolute values, never rounded to the threshold: the user decides with the real number.
            string sr = measured ? r.SampleRate.ToString(inv) : "";
            string bits = measured ? r.Bits.ToString(inv) : "";
            string freq = measured ? r.FreqMaximaDetectada.ToString("F0", inv) : "";
            string verdict = Verdict.Label(r.Kind);
            string detail = r.Erro ?? "";

            sw.WriteLine(string.Join(";", new[] { faixa, key, bpm, formato, sr, bits, freq, verdict, detail }.Select(Escape)));
        }
    }

    private static string Escape(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        if (s.Contains(';') || s.Contains('"') || s.Contains('\n'))
            return "\"" + s.Replace("\"", "\"\"") + "\"";
        return s;
    }
}
