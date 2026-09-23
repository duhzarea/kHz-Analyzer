using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace KhzAnalyzer.Core;

public static class CsvReport
{
    public static void Generate(string path, IEnumerable<AnalysisResult> results)
    {
        using var sw = new StreamWriter(path, false, System.Text.Encoding.UTF8);
        sw.WriteLine("FAIXA;KEY;BPM;TIPO;SAMPLE RATE;BITS;FREQ. MÁX.;VEREDITO");
        
        var ordered = results.OrderBy(r =>
        {
            var (_, _, faixa) = TrackNameParser.Parse(r.Arquivo);
            return faixa;
        });

        var culture = new System.Globalization.CultureInfo("pt-BR");

        foreach (var r in ordered)
        {
            var (key, bpm, faixa) = TrackNameParser.Parse(r.Arquivo);
            string formato = Path.GetExtension(r.Arquivo).TrimStart('.').ToUpperInvariant();
            string sr = r.Erro != null ? "-" : r.SampleRate.ToString("N0", culture) + " Hz";
            string bits = r.Erro != null ? "-" : r.Bits.ToString();
            string freq = r.Erro != null ? "-" : r.FreqMaximaDetectada.ToString("N0", culture) + " Hz";
            string veredito = r.Erro != null ? r.Erro : (r.EhOk ? "Chega em 19 kHz+" : r.Veredito);
            
            sw.WriteLine($"{Escape(faixa)};{Escape(key)};{Escape(bpm)};{Escape(formato)};{Escape(sr)};{Escape(bits)};{Escape(freq)};{Escape(veredito)}");
        }
    }
    
    private static string Escape(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        if (s.Contains(";") || s.Contains("\"") || s.Contains("\n"))
            return "\"" + s.Replace("\"", "\"\"") + "\"";
        return s;
    }
}
