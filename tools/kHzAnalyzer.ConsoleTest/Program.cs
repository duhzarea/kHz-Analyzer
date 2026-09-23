using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using KhzAnalyzer.Core;

class Program
{
    static void Main(string[] args)
    {
        string pasta = @"G:\NOT SHARE";
        string[] extensoes = { ".wav", ".aiff", ".aif" };
        var opcoes = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true };
        var arquivos = new List<string>();

        foreach (var arquivo in Directory.EnumerateFiles(pasta, "*.*", opcoes))
        {
            if (extensoes.Contains(Path.GetExtension(arquivo).ToLowerInvariant())) arquivos.Add(arquivo);
        }

        Console.WriteLine($"Total de arquivos: {arquivos.Count}");
        
        // Medir tempo total e pico de memória
        var proc = Process.GetCurrentProcess();
        
        Console.WriteLine("Rodando config T (Triagem: Estágio 1 + 2)...");
        var cronT = Stopwatch.StartNew();
        ExecutarLote(arquivos, c => 
        {
            var res1 = SpectrumAnalyzer.AnalisarF(c);
            if (res1.Erro != null || res1.FreqMaximaDetectada < 18000.0) return res1;
            return SpectrumAnalyzer.AnalisarA(c);
        });
        cronT.Stop();
        
        proc.Refresh();
        var peakMemT = proc.PeakWorkingSet64 / (1024.0 * 1024.0);

        Console.WriteLine("Rodando config S (A-Streaming puro)...");
        var cronS = Stopwatch.StartNew();
        ExecutarLote(arquivos, SpectrumAnalyzer.AnalisarA);
        cronS.Stop();
        
        proc.Refresh();
        var peakMemS = proc.PeakWorkingSet64 / (1024.0 * 1024.0);

        Console.WriteLine($"Tempo T (Triagem): {cronT.Elapsed.TotalSeconds:F2} s");
        Console.WriteLine($"Tempo S (Puro A): {cronS.Elapsed.TotalSeconds:F2} s");
        Console.WriteLine($"Pico de Memória: {Math.Max(peakMemT, peakMemS):F2} MB");
    }
    
    static List<AnalysisResult> ExecutarLote(List<string> arquivos, Func<string, AnalysisResult> analisador)
    {
        var resultados = new AnalysisResult[arquivos.Count];
        int workers = 4;
        var sem = new System.Threading.SemaphoreSlim(workers);
        var tarefas = new List<System.Threading.Tasks.Task>();
        
        for (int i = 0; i < arquivos.Count; i++)
        {
            int idx = i;
            string caminho = arquivos[idx];
            sem.Wait();
            tarefas.Add(System.Threading.Tasks.Task.Run(() =>
            {
                try { resultados[idx] = analisador(caminho); }
                finally { sem.Release(); }
            }));
        }
        System.Threading.Tasks.Task.WaitAll(tarefas.ToArray());
        return resultados.ToList();
    }
}
