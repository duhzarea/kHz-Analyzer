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

var cronometroAnalise = Stopwatch.StartNew();
int ok = 0, ruim = 0, erro = 0;

int workers = Math.Min(4, Math.Max(1, Environment.ProcessorCount / 2));
Console.WriteLine($"Executando com {workers} workers...");

var semaforo = new SemaphoreSlim(workers);
var tarefas = new List<Task>();
var r_lock = new object();

foreach (var caminho in arquivos)
{
    semaforo.Wait();
    tarefas.Add(Task.Run(() =>
    {
        Thread.CurrentThread.Priority = ThreadPriority.BelowNormal;
        try
        {
            var r = SpectrumAnalyzer.Analisar(caminho);
            lock (r_lock)
            {
                if (r.Erro is not null) erro++;
                else if (r.EhOk) ok++;
                else ruim++;
            }
        }
        catch { lock (r_lock) erro++; }
        finally { semaforo.Release(); }
    }));
}

Task.WaitAll(tarefas.ToArray());
cronometroAnalise.Stop();

long peakMb = Process.GetCurrentProcess().PeakWorkingSet64 / (1024 * 1024);
Console.WriteLine($"\nAnálise concluída em {cronometroAnalise.Elapsed}.");
Console.WriteLine($"Total: {arquivos.Count}  ·  OK: {ok}  ·  Abaixo: {ruim}  ·  Erros: {erro}");
Console.WriteLine($"Pico de Memória (Peak Working Set): {peakMb} MB");
