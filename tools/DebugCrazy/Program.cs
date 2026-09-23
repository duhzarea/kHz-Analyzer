using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using KhzAnalyzer.Core;

class Program
{
    static void Main()
    {
        string targetDir = @"G:\NOT SHARE";
        var files = Directory.EnumerateFiles(targetDir, "*.*", SearchOption.AllDirectories)
                             .Where(f => f.EndsWith(".wav", StringComparison.OrdinalIgnoreCase) || 
                                         f.EndsWith(".aiff", StringComparison.OrdinalIgnoreCase) || 
                                         f.EndsWith(".aif", StringComparison.OrdinalIgnoreCase))
                             .ToList();

        int workers = Math.Max(1, Environment.ProcessorCount / 2);
        var sem = new SemaphoreSlim(workers);
        
        double minF_for_OkA = double.MaxValue;
        double maxF_for_BelowA = double.MinValue;
        
        string minF_for_OkA_file = "";
        string maxF_for_BelowA_file = "";

        int processed = 0;

        Parallel.ForEach(files, f =>
        {
            sem.Wait();
            try
            {
                var resA = SpectrumAnalyzer.AnalisarA(f);
                if (resA.Erro != null) return;
                
                var resF = SpectrumAnalyzer.AnalisarF(f);
                if (resF.Erro != null) return;
                
                bool isOkA = resA.FreqMaximaDetectada >= 19000;
                
                lock (sem)
                {
                    if (isOkA)
                    {
                        if (resF.FreqMaximaDetectada < minF_for_OkA)
                        {
                            minF_for_OkA = resF.FreqMaximaDetectada;
                            minF_for_OkA_file = f;
                        }
                    }
                    else
                    {
                        if (resF.FreqMaximaDetectada > maxF_for_BelowA)
                        {
                            maxF_for_BelowA = resF.FreqMaximaDetectada;
                            maxF_for_BelowA_file = f;
                        }
                    }
                }
            }
            finally 
            { 
                int p = Interlocked.Increment(ref processed);
                if (p % 50 == 0) Console.WriteLine($"Progresso: {p}/{files.Count}");
                sem.Release(); 
            }
        });

        Console.WriteLine($"minF_for_OkA: {minF_for_OkA:F1} Hz ({minF_for_OkA_file})");
        Console.WriteLine($"maxF_for_BelowA: {maxF_for_BelowA:F1} Hz ({maxF_for_BelowA_file})");
    }
}
