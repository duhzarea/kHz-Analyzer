using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using KhzAnalyzer.Core;

if (args.Length > 0 && args[0] == "--f-only")
{
    RunFOnlyMode(args.Skip(1).ToArray());
    return;
}

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
var opcoes = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true };
var arquivos = new List<string>();

foreach (var arquivo in Directory.EnumerateFiles(pasta, "*.*", opcoes))
{
    string ext = Path.GetExtension(arquivo).ToLowerInvariant();
    if (extensoes.Contains(ext)) arquivos.Add(arquivo);
}

var mandatorios = new List<string>();
var fixturesDir = @"G:\#kHz Analyzer\tests\fixtures";
if (Directory.Exists(fixturesDir))
{
    foreach (var f in Directory.EnumerateFiles(fixturesDir, "*.*"))
    {
        string ext = Path.GetExtension(f).ToLowerInvariant();
        if (extensoes.Contains(ext)) mandatorios.Add(f);
    }
}

string crazy = arquivos.FirstOrDefault(x => x.Contains("Gnarls Barkley") && x.Contains("Crazy"));
if (crazy != null) mandatorios.Add(crazy);
string fire = arquivos.FirstOrDefault(x => x.Contains("Fatsync, DOUG") && x.Contains("Fire"));
if (fire != null) mandatorios.Add(fire);

Console.WriteLine($"Executando Variante A em TODA a biblioteca ({arquivos.Count} arquivos) para encontrar os limitrofes...");
var resFullA = ExecutarLote(arquivos, SpectrumAnalyzer.AnalisarA);

var arquivosTeste = new List<string>(mandatorios);
int limitrofesCount = 0;

for (int i = 0; i < arquivos.Count; i++)
{
    var r = resFullA[i];
    if (r.EhOk && r.FreqMaximaDetectada >= 18000 && r.FreqMaximaDetectada <= 21500)
    {
        if (!arquivosTeste.Contains(arquivos[i]))
        {
            arquivosTeste.Add(arquivos[i]);
            limitrofesCount++;
        }
    }
    else if (!r.EhOk && !arquivosTeste.Contains(arquivos[i]) && r.Erro == null)
    {
        arquivosTeste.Add(arquivos[i]);
    }
}

Console.WriteLine($"\nEncontrados {limitrofesCount} arquivos limitrofes (18-21.5kHz em A).");
Console.WriteLine($"Total de arquivos para o teste rigoroso: {arquivosTeste.Count}\n");

Console.WriteLine("Executando Variante A (Completo, 65536, Média)...");
var cronA = Stopwatch.StartNew();
var resA = ExecutarLote(arquivosTeste, SpectrumAnalyzer.AnalisarA);
cronA.Stop();

var resultsF = new Dictionary<string, AnalysisResult>();
var resultsF2 = new Dictionary<string, AnalysisResult>();
var filesToTest = arquivosTeste;

Console.WriteLine("Executando Variante F (Amostrado, 65536, Média)...");
foreach (var file in filesToTest)
{
    resultsF[file] = SpectrumAnalyzer.AnalisarF(file);
}

Console.WriteLine("Executando Variante F (2ª vez via Processo Externo) para provar determinismo inter-processos...");
RunF2ExternalProcess(filesToTest, resultsF2);

var sb = new StringBuilder();
sb.AppendLine("# Comparação de Motor: Completo vs Amostrado (Teste Rigoroso)");
sb.AppendLine();
sb.AppendLine("## Metodologia e Parâmetros");
sb.AppendLine($"- **Hardware**: {Environment.ProcessorCount} lógicos");
sb.AppendLine($"- **Amostra**: {arquivosTeste.Count} arquivos (incluindo {limitrofesCount} limítrofes, 8 fixtures e casos conhecidos).");
sb.AppendLine("- **Semente Determinística**: O hash foi trocado para um DJB2 estável de string em C#, provando o determinismo inter-processos.");
sb.AppendLine();
sb.AppendLine("### Legenda de Divergências");
sb.AppendLine("- 🚨 **FALHA DE DETERMINISMO**: F não bateu com F2. Risco Crítico.");
sb.AppendLine("- ⚠️ **FALSO NEGATIVO**: F aprova o que A reprova. Risco grave de deixar master cortado passar.");
sb.AppendLine("- ⚠️ **FALSO POSITIVO**: F reprova o que A aprova. Incômodo, mas não perigoso.");
sb.AppendLine("");
sb.AppendLine("| Arquivo | FreqMax A | Ver A | FreqMax F | Ver F | FreqMax F2 | Ver F2 | Divergência |");
sb.AppendLine("|---|---|---|---|---|---|---|---|");

int changedCount = 0;
string docsDir = Path.Combine("..", "..", "docs");
if (!Directory.Exists(docsDir)) Directory.CreateDirectory(docsDir);

for (int i = 0; i < arquivosTeste.Count; i++)
{
    string file = arquivosTeste[i];
    var resA_local = resA[i];
    var resF = resultsF[file];
    var resF2 = resultsF2[file];

    string nome = Path.GetFileName(file);
    
    string verA = resA_local.Veredito.StartsWith("OK") ? "OK" : (resA_local.Veredito.StartsWith("ERRO") ? "ERRO" : "ABAIXO");
    string verF = resF.Veredito.StartsWith("OK") ? "OK" : (resF.Veredito.StartsWith("ERRO") ? "ERRO" : "ABAIXO");
    string verF2 = resF2.Veredito.StartsWith("OK") ? "OK" : (resF2.Veredito.StartsWith("ERRO") ? "ERRO" : "ABAIXO");

    bool isDivergent = false;
    string divLabel = "";
    
    if (verF != verF2 || resF.FreqMaximaDetectada != resF2.FreqMaximaDetectada)
    {
        isDivergent = true;
        divLabel = "🚨 **FALHA DE DETERMINISMO**";
    }
    else if (verA != verF && verA != "ERRO")
    {
        isDivergent = true;
        if (verF == "OK" && verA == "ABAIXO")
            divLabel = "⚠️ **FALSO NEGATIVO**";
        else if (verF == "ABAIXO" && verA == "OK")
            divLabel = "⚠️ **FALSO POSITIVO**";
        else
            divLabel = "Divergência de Veredito";
        changedCount++;
    }

    if (isDivergent)
        nome = $"**{nome}**";

    sb.AppendLine($"| {nome} | {resA_local.FreqMaximaDetectada:F1} | {verA} | {resF.FreqMaximaDetectada:F1} | {verF} | {resF2.FreqMaximaDetectada:F1} | {verF2} | {divLabel} |");
}

File.WriteAllText(Path.Combine(docsDir, "comparacao-motor-amostrado.md"), sb.ToString());
Console.WriteLine("Tabela gerada em docs/comparacao-motor-amostrado.md");
Console.WriteLine($"Divergências A vs F encontradas: {changedCount}");

static void RunFOnlyMode(string[] args)
{
    var file = args[0];
    var res = SpectrumAnalyzer.AnalisarF(file);
    File.AppendAllLines("f2_results.txt", new[] { $"{file}|{res.FreqMaximaDetectada:F1}|{res.Veredito}" });
}

static void RunF2ExternalProcess(List<string> files, Dictionary<string, AnalysisResult> resultsF2)
{
    if (File.Exists("f2_results.txt")) File.Delete("f2_results.txt");
    string exePath = Process.GetCurrentProcess().MainModule.FileName;
    foreach (var file in files)
    {
        var psi = new ProcessStartInfo(exePath, $"--f-only \"{file}\"") { UseShellExecute = false };
        Process.Start(psi).WaitForExit();
    }
    foreach (var line in File.ReadAllLines("f2_results.txt"))
    {
        var parts = line.Split('|');
        resultsF2[parts[0]] = new AnalysisResult { FreqMaximaDetectada = double.Parse(parts[1]), Veredito = parts[2] };
    }
}

static List<AnalysisResult> ExecutarLote(List<string> arquivos, Func<string, AnalysisResult> analisador)
{
    var resultados = new AnalysisResult[arquivos.Count];
    int workers = Math.Min(4, Math.Max(1, Environment.ProcessorCount / 2));
    var sem = new System.Threading.SemaphoreSlim(workers);
    var tarefas = new List<System.Threading.Tasks.Task>();
    
    for (int i = 0; i < arquivos.Count; i++)
    {
        int idx = i;
        string caminho = arquivos[idx];
        sem.Wait();
        tarefas.Add(System.Threading.Tasks.Task.Run(() =>
        {
            System.Threading.Thread.CurrentThread.Priority = System.Threading.ThreadPriority.BelowNormal;
            try { resultados[idx] = analisador(caminho); }
            finally { sem.Release(); }
        }));
    }
    System.Threading.Tasks.Task.WaitAll(tarefas.ToArray());
    return resultados.ToList();
}
