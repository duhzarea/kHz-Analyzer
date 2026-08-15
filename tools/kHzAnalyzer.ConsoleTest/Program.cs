using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Collections.Generic;
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

var opcoes = new EnumerationOptions
{
    RecurseSubdirectories = true,
    IgnoreInaccessible = true,
    AttributesToSkip = FileAttributes.System | FileAttributes.Temporary
};

var cronometro = Stopwatch.StartNew();
var arquivos = new List<string>();
int offlinePulados = 0;

foreach (var arquivo in Directory.EnumerateFiles(pasta, "*.*", opcoes))
{
    string ext = Path.GetExtension(arquivo).ToLowerInvariant();
    if (extensoes.Contains(ext))
    {
        if (File.GetAttributes(arquivo).HasFlag(FileAttributes.Offline))
        {
            offlinePulados++;
            continue;
        }
        arquivos.Add(arquivo);
    }
}
cronometro.Stop();

Console.WriteLine($"Enumeração concluída em {cronometro.Elapsed}.");
Console.WriteLine($"Encontrados {arquivos.Count} arquivos válidos ({offlinePulados} offline pulados).\n");

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
