using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using KhzAnalyzer.Core;

// kHz Analyzer — console tool.
//
//   dotnet run -c Release -- "<folder>"               lists every track with its verdict
//   dotnet run -c Release -- --verificar "<folder>"   writes docs/handoff/verificacao.txt
//
// The verification mode produces raw numbers only (no interpretation): regression
// fixtures, identity between the original in-memory method and the streaming engine,
// thread-safety (1 worker vs 4 workers), timings, memory, and cache behaviour.
static class Program
{
    static readonly string[] Ext = { ".wav", ".aiff", ".aif" };
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    static int Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;

        if (args.Length >= 2 && args[0] == "--verificar")
            return Verificar(args[1]);

        if (args.Length >= 1 && Directory.Exists(args[0]))
            return Listar(args[0]);

        Console.WriteLine("Usage:");
        Console.WriteLine("  dotnet run -c Release -- \"<folder>\"");
        Console.WriteLine("  dotnet run -c Release -- --verificar \"<folder>\"");
        return 1;
    }

    // ------------------------------------------------------------------ list --

    static int Listar(string pasta)
    {
        var arquivos = Enumerar(pasta, out int offline);
        Console.WriteLine($"{arquivos.Count} files ({offline} offline skipped)\n");
        int ok = 0, abaixo = 0, erro = 0;
        foreach (var f in arquivos)
        {
            var r = SpectrumAnalyzer.Analisar(f);
            string st = Status(r);
            if (st == "OK") ok++; else if (st == "ABAIXO") abaixo++; else erro++;
            Console.WriteLine($"[{st,-6}] {r.FreqMaximaDetectada.ToString("F1", Inv),9} Hz  {Path.GetFileName(f)}");
        }
        Console.WriteLine($"\nTotal {arquivos.Count} · OK {ok} · Below {abaixo} · Errors {erro}");
        return 0;
    }

    // ---------------------------------------------------------------- verify --

    static int Verificar(string pasta)
    {
        if (!Directory.Exists(pasta))
        {
            Console.WriteLine($"Folder not found: {pasta}");
            return 1;
        }

        try { Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.BelowNormal; } catch { }

        string raiz = AcharRaiz();
        string saida = Path.Combine(raiz, "docs", "handoff", "verificacao.txt");
        string fixturesDir = Path.Combine(raiz, "tests", "fixtures");
        string cachePath = Path.Combine(Path.GetTempPath(), "khz_verificacao_cache.db");

        var secA = new StringBuilder();
        var secB = new StringBuilder();
        var secC = new StringBuilder();
        var secD = new StringBuilder();
        var secE = new StringBuilder();
        var secF = new StringBuilder();
        var secG = new StringBuilder();
        var secH = new StringBuilder();

        // ---- A: environment
        Console.WriteLine("[A] environment");
        var biblioteca = Enumerar(pasta, out int offline);
        secA.AppendLine($"Date/time ........ {DateTime.Now:yyyy-MM-dd HH:mm:ss zzz}");
        secA.AppendLine($"Logical cores .... {Environment.ProcessorCount}");
        secA.AppendLine($".NET ............. {Environment.Version}");
        secA.AppendLine($"Build ............ {(IsDebug() ? "Debug" : "Release")}");
        secA.AppendLine($"Threshold ........ {SpectrumAnalyzer.FreqAlvo.ToString("F0", Inv)} Hz, margin {SpectrumAnalyzer.MargemDb.ToString("F1", Inv)} dB");
        secA.AppendLine($"Library .......... {pasta}");
        secA.AppendLine($"Audio files ...... {biblioteca.Count} ({offline} offline placeholders skipped)");

        // ---- B: regression fixtures
        Console.WriteLine("[B] fixtures");
        var fixtures = new List<string>();
        int bPass = 0, bFail = 0;
        string expectedPath = Path.Combine(fixturesDir, "expected.md");
        if (!File.Exists(expectedPath))
        {
            secB.AppendLine("expected.md NOT FOUND");
        }
        else
        {
            secB.AppendLine($"{"fixture",-30} {"expected",-8} {"got",-8} {"FreqMax",10}  result");
            foreach (var linha in File.ReadAllLines(expectedPath))
            {
                var m = Regex.Match(linha, @"^- `([^`]+)`:\s*(OK|ABAIXO|ERRO)");
                if (!m.Success) continue;
                string nome = m.Groups[1].Value, esperado = m.Groups[2].Value;
                string caminho = Path.Combine(fixturesDir, nome);
                if (!File.Exists(caminho))
                {
                    secB.AppendLine($"{nome,-30} {esperado,-8} {"-",-8} {"-",10}  FILE MISSING");
                    bFail++;
                    continue;
                }
                fixtures.Add(caminho);
                var r = SpectrumAnalyzer.Analisar(caminho);
                string obtido = Status(r);
                bool passou = obtido == esperado;
                if (passou) bPass++; else bFail++;
                secB.AppendLine($"{nome,-30} {esperado,-8} {obtido,-8} {r.FreqMaximaDetectada.ToString("F1", Inv),10}  {(passou ? "PASS" : "FAIL")}");
            }
            secB.AppendLine($"TOTAL: {bPass} passed, {bFail} failed");
        }

        // ---- E: performance (also yields the per-file results used by C, D, F)
        Console.WriteLine($"[E] full library, cold cache, 4 workers ({biblioteca.Count} files)");
        try { if (File.Exists(cachePath)) File.Delete(cachePath); } catch { }

        var resultados = new ConcurrentDictionary<string, AnalysisResult>();
        TimeSpan tFrio, tQuente;
        int hitsFrio = 0, hitsQuente = 0;
        long pico;

        using (var cache = new AnalysisCache(cachePath))
        {
            var sw = Stopwatch.StartNew();
            int feitos = 0;
            Parallel.ForEach(biblioteca, new ParallelOptions { MaxDegreeOfParallelism = 4 }, f =>
            {
                var r = cache.TryGet(f);
                if (r is null)
                {
                    r = SpectrumAnalyzer.Analisar(f);
                    cache.Save(r);
                }
                else Interlocked.Increment(ref hitsFrio);
                resultados[f] = r;
                int n = Interlocked.Increment(ref feitos);
                if (n % 25 == 0) Console.WriteLine($"    {n}/{biblioteca.Count}  {sw.Elapsed:mm\\:ss}");
            });
            sw.Stop();
            tFrio = sw.Elapsed;
            pico = Process.GetCurrentProcess().PeakWorkingSet64;

            Console.WriteLine("[E] full library, warm cache");
            sw.Restart();
            Parallel.ForEach(biblioteca, new ParallelOptions { MaxDegreeOfParallelism = 4 }, f =>
            {
                var r = cache.TryGet(f);
                if (r is null) r = SpectrumAnalyzer.Analisar(f);
                else Interlocked.Increment(ref hitsQuente);
            });
            sw.Stop();
            tQuente = sw.Elapsed;
        }

        int ok = resultados.Values.Count(r => Status(r) == "OK");
        int abaixo = resultados.Values.Count(r => Status(r) == "ABAIXO");
        int erro = resultados.Values.Count(r => Status(r) == "ERRO");
        secE.AppendLine($"Workers ...................... 4");
        secE.AppendLine($"Cold cache, total time ....... {tFrio:hh\\:mm\\:ss\\.ff}  (cache hits: {hitsFrio})");
        secE.AppendLine($"Cold cache, per file ......... {(biblioteca.Count > 0 ? tFrio.TotalSeconds / biblioteca.Count : 0).ToString("F2", Inv)} s");
        secE.AppendLine($"Peak working set ............. {(pico / 1048576.0).ToString("F1", Inv)} MB");
        secE.AppendLine($"Warm cache, total time ....... {tQuente:hh\\:mm\\:ss\\.ff}  (cache hits: {hitsQuente} of {biblioteca.Count})");
        secE.AppendLine($"Library verdicts ............. OK {ok} · BELOW {abaixo} · ERROR {erro}");
        foreach (var r in resultados.Values.Where(r => Status(r) != "OK").OrderBy(r => r.Arquivo, StringComparer.OrdinalIgnoreCase))
            secE.AppendLine($"   [{Status(r),-6}] {r.FreqMaximaDetectada.ToString("F1", Inv),9}  {Path.GetFileName(r.Arquivo)}  {r.Erro}");

        // ---- F: effect of 20 kHz -> 19 kHz
        var mudaram = resultados.Values
            .Where(r => r.Erro is null && r.FreqMaximaDetectada >= 19000 && r.FreqMaximaDetectada < 20000)
            .OrderBy(r => r.FreqMaximaDetectada).ToList();
        foreach (var r in mudaram)
            secF.AppendLine($"{r.FreqMaximaDetectada.ToString("F1", Inv),9} Hz  {Path.GetFileName(r.Arquivo)}");
        secF.AppendLine($"TOTAL: {mudaram.Count} track(s) between 19000 and 19999 Hz (BELOW at 20 kHz, OK at 19 kHz)");

        // ---- rigorous set: fixtures + every non-OK track + every borderline track
        var rigoroso = new List<string>(fixtures);
        rigoroso.AddRange(resultados.Values
            .Where(r => r.Erro is null && (Status(r) == "ABAIXO" || (r.FreqMaximaDetectada >= 18000 && r.FreqMaximaDetectada <= 21500)))
            .Select(r => r.Arquivo)
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase));

        // ---- C: identity, original in-memory method vs streaming engine
        Console.WriteLine($"[C] identity, {rigoroso.Count} files");
        int cDif = 0;
        secC.AppendLine($"{"in-memory",10} {"streaming",10} {"diff",8}  {"mem",-6} {"str",-6}  file");
        foreach (var f in rigoroso)
        {
            var (fRef, stRef) = Referencia(f);
            var r = SpectrumAnalyzer.Analisar(f);
            double dif = r.FreqMaximaDetectada - fRef;
            bool diferente = dif != 0 || stRef != Status(r);
            if (diferente) cDif++;
            secC.AppendLine($"{fRef.ToString("F1", Inv),10} {r.FreqMaximaDetectada.ToString("F1", Inv),10} {dif.ToString("F1", Inv),8}  {stRef,-6} {Status(r),-6}  {Path.GetFileName(f)}{(diferente ? "   <-- DIFFERENT" : "")}");
        }
        secC.AppendLine($"TOTAL: {rigoroso.Count} files compared, {cDif} with a difference");

        // ---- D: thread safety, 1 worker vs 4 workers
        Console.WriteLine($"[D] 1 worker vs 4 workers, {rigoroso.Count} files");
        var um = new Dictionary<string, AnalysisResult>();
        foreach (var f in rigoroso) um[f] = SpectrumAnalyzer.Analisar(f);
        var quatro = new ConcurrentDictionary<string, AnalysisResult>();
        Parallel.ForEach(rigoroso, new ParallelOptions { MaxDegreeOfParallelism = 4 }, f => quatro[f] = SpectrumAnalyzer.Analisar(f));
        int dDif = 0;
        secD.AppendLine($"{"1 worker",10} {"4 workers",10} {"diff",8}  file");
        foreach (var f in rigoroso)
        {
            double a = um[f].FreqMaximaDetectada, b = quatro[f].FreqMaximaDetectada;
            bool diferente = a != b || Status(um[f]) != Status(quatro[f]);
            if (diferente) dDif++;
            secD.AppendLine($"{a.ToString("F1", Inv),10} {b.ToString("F1", Inv),10} {(b - a).ToString("F1", Inv),8}  {Path.GetFileName(f)}{(diferente ? "   <-- DIFFERENT" : "")}");
        }
        secD.AppendLine($"TOTAL: {rigoroso.Count} files compared, {dDif} diverged");

        // ---- G: leftover "20 kHz" strings
        Console.WriteLine("[G] text search");
        var padrao = new Regex(@"20\s?kHz|20000|20\.000", RegexOptions.IgnoreCase);
        string[] extTexto = { ".cs", ".xaml", ".md", ".csproj", ".txt" };
        string[] excluir = { "\\bin\\", "\\obj\\", "\\_quarentena\\", "\\.git\\", "\\docs\\handoff\\historico\\" };
        int gTotal = 0;
        foreach (var arq in Directory.EnumerateFiles(raiz, "*", SearchOption.AllDirectories).OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
        {
            if (!extTexto.Contains(Path.GetExtension(arq).ToLowerInvariant())) continue;
            if (excluir.Any(e => arq.Contains(e, StringComparison.OrdinalIgnoreCase))) continue;
            if (arq.Equals(saida, StringComparison.OrdinalIgnoreCase)) continue;
            string[] linhas;
            try { linhas = File.ReadAllLines(arq); } catch { continue; }
            for (int i = 0; i < linhas.Length; i++)
            {
                if (!padrao.IsMatch(linhas[i])) continue;
                gTotal++;
                string trecho = linhas[i].Trim();
                if (trecho.Length > 110) trecho = trecho[..110] + "…";
                secG.AppendLine($"{Path.GetRelativePath(raiz, arq)}:{i + 1}: {trecho}");
            }
        }
        secG.AppendLine($"TOTAL: {gTotal} occurrence(s)");

        // ---- H: cache, repeated open
        Console.WriteLine("[H] cache reopen");
        for (int i = 1; i <= 3; i++)
        {
            try
            {
                using var c = new AnalysisCache(cachePath);
                secH.AppendLine($"Open #{i}: OK");
            }
            catch (Exception e)
            {
                secH.AppendLine($"Open #{i}: EXCEPTION {e}");
            }
        }
        try { secH.AppendLine($"Database size: {(new FileInfo(cachePath).Length / 1024.0).ToString("F1", Inv)} KB ({cachePath})"); } catch { }

        // ---- write
        var doc = new StringBuilder();
        doc.AppendLine("kHz Analyzer — verification output (raw data, generated by tools/kHzAnalyzer.ConsoleTest --verificar)");
        doc.AppendLine();
        void Sec(string titulo, StringBuilder sb) { doc.AppendLine("=== " + titulo + " ==="); doc.Append(sb); doc.AppendLine(); }
        Sec("A. ENVIRONMENT", secA);
        Sec("B. REGRESSION FIXTURES", secB);
        Sec("C. IDENTITY: ORIGINAL IN-MEMORY METHOD vs STREAMING ENGINE", secC);
        Sec("D. THREAD SAFETY: 1 WORKER vs 4 WORKERS", secD);
        Sec("E. PERFORMANCE", secE);
        Sec("F. EFFECT OF THE 20 kHz -> 19 kHz THRESHOLD", secF);
        Sec("G. LEFTOVER \"20 kHz\" STRINGS", secG);
        Sec("H. CACHE: REPEATED OPEN", secH);

        Directory.CreateDirectory(Path.GetDirectoryName(saida)!);
        File.WriteAllText(saida, doc.ToString(), new UTF8Encoding(false));
        Console.WriteLine($"\nWritten: {saida}");
        return 0;
    }

    // --------------------------------------------------------------- helpers --

    static string Status(AnalysisResult r) => r.Erro is not null ? "ERRO" : r.EhOk ? "OK" : "ABAIXO";

    static bool IsDebug()
    {
#if DEBUG
        return true;
#else
        return false;
#endif
    }

    static List<string> Enumerar(string pasta, out int offline)
    {
        offline = 0;
        var opcoes = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.System | FileAttributes.Temporary,
        };
        var lista = new List<string>();
        foreach (var f in Directory.EnumerateFiles(pasta, "*", opcoes))
        {
            if (!Ext.Contains(Path.GetExtension(f).ToLowerInvariant())) continue;
            try
            {
                if (File.GetAttributes(f).HasFlag(FileAttributes.Offline)) { offline++; continue; }
            }
            catch { }
            lista.Add(f);
        }
        lista.Sort(StringComparer.OrdinalIgnoreCase);
        return lista;
    }

    static string AcharRaiz()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "kHzAnalyzer.Core")))
            dir = dir.Parent;
        return dir?.FullName ?? Directory.GetCurrentDirectory();
    }

    /// <summary>
    /// Reference: the original validated method. Loads the whole file into memory and runs
    /// Welch over it (window 65536, 50% overlap, mean), then applies the same criterion.
    /// Kept here, outside the production path, only to prove the streaming engine is identical.
    /// </summary>
    static (double freqMax, string status) Referencia(string caminho)
    {
        AudioData data;
        try { data = WavAiffReader.Read(caminho); }
        catch { return (0, "ERRO"); }

        if (data.Samples.Length < 1024) return (0, "ERRO");

        double nyquist = data.SampleRate / 2.0;
        if (nyquist < SpectrumAnalyzer.FreqAlvo) return (nyquist, "ABAIXO");

        var (freqs, psd) = Fft.Welch(data.Samples, data.SampleRate, Math.Min(65536, data.Samples.Length));

        double maxLog = double.NegativeInfinity;
        var psdDb = new double[psd.Length];
        for (int i = 0; i < psd.Length; i++)
        {
            psdDb[i] = 10 * Math.Log10(psd[i] + 1e-20);
            if (psdDb[i] > maxLog) maxLog = psdDb[i];
        }

        double limiar = SpectrumAnalyzer.PisoDeRuidoTeorico(data.Bits) + SpectrumAnalyzer.MargemDb;

        double freqMax = 0;
        bool alta = false;
        for (int i = 0; i < freqs.Length; i++)
        {
            if (freqs[i] >= 18000 && (psdDb[i] - maxLog) > limiar)
            {
                alta = true;
                if (freqs[i] > freqMax) freqMax = freqs[i];
            }
        }
        if (!alta)
        {
            for (int i = 0; i < freqs.Length; i++)
                if (freqs[i] < 18000 && (psdDb[i] - maxLog) > limiar && freqs[i] > freqMax)
                    freqMax = freqs[i];
        }

        freqMax = Math.Round(freqMax, 1);
        return (freqMax, freqMax >= SpectrumAnalyzer.FreqAlvo ? "OK" : "ABAIXO");
    }
}
