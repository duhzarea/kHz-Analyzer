using System.IO;
using System.Text.RegularExpressions;

namespace KhzAnalyzer.Core;

/// <summary>Extrai Key (Camelot), BPM e "Artista - Título (Remix)" do nome do arquivo, quando possível.</summary>
public static class TrackNameParser
{
    private static readonly Regex CamelotRegex = new(@"^\d{1,2}[AB]$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static (string key, string bpm, string faixa) Parse(string caminho)
    {
        string nome = Path.GetFileNameWithoutExtension(caminho);
        var partes = new System.Collections.Generic.List<string>(nome.Split(" - "));
        for (int i = 0; i < partes.Count; i++) partes[i] = partes[i].Trim();

        string key = "", bpm = "";

        if (partes.Count > 0 && CamelotRegex.IsMatch(partes[0]))
        {
            key = partes[0];
            partes.RemoveAt(0);
        }
        if (partes.Count > 0 && IsAllDigits(partes[0]))
        {
            bpm = partes[0];
            partes.RemoveAt(0);
        }

        string faixa;
        if (partes.Count >= 2)
            faixa = $"{partes[0]} - {string.Join(" - ", partes.GetRange(1, partes.Count - 1))}";
        else if (partes.Count == 1)
            faixa = partes[0];
        else
            faixa = nome;

        return (key, bpm, faixa);
    }

    private static bool IsAllDigits(string s)
    {
        if (s.Length == 0) return false;
        foreach (char c in s)
            if (!char.IsDigit(c)) return false;
        return true;
    }
}
