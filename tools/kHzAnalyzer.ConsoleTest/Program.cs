using System;
using System.IO;
using System.Linq;
using KhzAnalyzer.Core;

if (args.Length < 1)
{
    Console.WriteLine("Uso: dotnet run -- \"C:\\caminho\\da\\pasta\"");
    return;
}

string pasta = args[0];
if (!Directory.Exists(pasta))
{
    Console.WriteLine($"Pasta não encontrada: {pasta}");
    return;
}

string[] extensoes = { ".wav", ".aiff", ".aif" };
var arquivos = Directory.EnumerateFiles(pasta, "*.*", SearchOption.AllDirectories)
    .Where(f => extensoes.Contains(Path.GetExtension(f).ToLowerInvariant()))
    .ToList();

Console.WriteLine($"Encontrados {arquivos.Count} arquivos.\n");

int ok = 0, ruim = 0, erro = 0;

foreach (var caminho in arquivos)
{
    var r = SpectrumAnalyzer.Analisar(caminho);
    var (key, bpm, faixa) = TrackNameParser.Parse(caminho);

    if (r.Erro is not null)
    {
        erro++;
        Console.WriteLine($"[ERRO] {faixa} — {r.Erro}");
    }
    else if (r.EhOk)
    {
        ok++;
        Console.WriteLine($"[OK]   {faixa} ({key} {bpm}bpm) — {r.SampleRate}Hz/{r.Bits}bit — até {r.FreqMaximaDetectada}Hz");
    }
    else
    {
        ruim++;
        Console.WriteLine($"[BAIXO] {faixa} — {r.Veredito}");
    }
}

Console.WriteLine($"\nTotal: {arquivos.Count}  ·  OK: {ok}  ·  Abaixo: {ruim}  ·  Erros: {erro}");
