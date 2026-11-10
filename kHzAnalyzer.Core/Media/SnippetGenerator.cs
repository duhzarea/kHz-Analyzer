using System.Diagnostics;
using System.Globalization;

namespace KhzAnalyzer.Core.Media;

public sealed class SnippetOptions
{
    public int StartSeconds { get; init; } = 90;
    public int DurationSeconds { get; init; } = 120;
    public int BitrateKbps { get; init; } = 192;
    public bool Recursive { get; init; }
    /// <summary>Report what would be created, write nothing.</summary>
    public bool DryRun { get; init; }
}

public enum SnippetOutcome { Created, WouldCreate, Failed }

/// <summary>One track that was handled. Tracks that already had a snippet are only counted.</summary>
public sealed record SnippetEvent(string SourcePath, string OutputPath, SnippetOutcome Outcome, string Message);

public sealed record SnippetProgress(int Done, int Total, string CurrentFile, SnippetEvent? Finished);

public sealed record SnippetSummary(int Created, int AlreadyExisted, int Failed, TimeSpan Elapsed, bool DryRun);

/// <summary>
/// Batch preview generator: for every track without a snippet, cuts a section and saves it as MP3
/// in a "Snippets" subfolder next to the track. Never modifies, moves or deletes an original.
/// Port of GerarSnippets.ps1.
/// </summary>
public static class SnippetGenerator
{
    public static readonly string[] Extensions = { ".wav", ".mp3", ".aiff", ".aif", ".flac", ".m4a", ".ogg", ".wma", ".alac" };

    /// <summary>Folder the new snippets are written to.</summary>
    public const string OutputFolderName = "Snippets";

    /// <summary>Folders that hold snippets: never used as a source, always checked for existing snippets.</summary>
    public static readonly string[] SnippetFolderNames = { "Snippets", "Snippet", "Preview", "Previews", "PREVIEW" };

    /// <summary>Name suffixes that mark a file as being a snippet already.</summary>
    public static readonly string[] SnippetSuffixes = { "[Snippets]", "[Snippet]", "[PREVIEW]", "[Preview]", "[PREV]" };

    public const string NewSuffix = "[Snippets]";

    private const long MinValidBytes = 10 * 1024;
    private static readonly StringComparison Ci = StringComparison.OrdinalIgnoreCase;

    public sealed record Plan(List<(string Source, string Output)> ToDo, int AlreadyExisted);

    /// <summary>Lists what has to be created. Touches nothing on disk.</summary>
    public static Plan BuildPlan(IEnumerable<string> folders, bool recursive, CancellationToken token = default)
    {
        var aFazer = new List<(string, string)>();
        int existentes = 0;
        var vistos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (string raiz in folders)
        {
            if (!Directory.Exists(raiz)) continue;
            foreach (string pasta in SourceFolders(raiz, recursive))
            {
                token.ThrowIfCancellationRequested();
                if (!vistos.Add(Path.GetFullPath(pasta))) continue;   // a folder listed twice, or inside another one

                foreach (string faixa in Tracks(pasta))
                {
                    string nome = Path.GetFileNameWithoutExtension(faixa);
                    if (HasSnippet(pasta, nome)) { existentes++; continue; }
                    aFazer.Add((faixa, Path.Combine(pasta, OutputFolderName, $"{nome} {NewSuffix}.mp3")));
                }
            }
        }
        return new Plan(aFazer, existentes);
    }

    public static async Task<SnippetSummary> RunAsync(IEnumerable<string> folders, SnippetOptions options,
        IProgress<SnippetProgress>? progress, CancellationToken token)
    {
        string ffmpeg = MediaTools.Ffmpeg ?? throw new MediaToolException("FFmpeg was not found.");
        var relogio = Stopwatch.StartNew();
        var inv = CultureInfo.InvariantCulture;

        var plano = await Task.Run(() => BuildPlan(folders, options.Recursive, token), token);
        int criados = 0, falhas = 0, feitos = 0, total = plano.ToDo.Count;
        progress?.Report(new SnippetProgress(0, total, "", null));

        foreach (var (fonte, saida) in plano.ToDo)
        {
            token.ThrowIfCancellationRequested();
            progress?.Report(new SnippetProgress(feitos, total, Path.GetFileName(fonte), null));

            SnippetEvent evento;
            if (options.DryRun)
            {
                criados++;
                evento = new SnippetEvent(fonte, saida, SnippetOutcome.WouldCreate, "Would be created");
            }
            else
            {
                // The destination folder is created only when there is something to write.
                Directory.CreateDirectory(Path.GetDirectoryName(saida)!);
                var erros = new List<string>();
                var t0 = Stopwatch.StartNew();
                try
                {
                    // -ss before -i = fast seek; -t = duration.
                    await MediaTools.RunAsync(ffmpeg, new[]
                    {
                        "-y", "-hide_banner", "-loglevel", "error",
                        "-ss", options.StartSeconds.ToString(inv), "-i", fonte, "-t", options.DurationSeconds.ToString(inv),
                        "-vn", "-codec:a", "libmp3lame", "-b:a", $"{options.BitrateKbps}k", saida,
                    }, null, l => erros.Add(l), token);
                }
                catch (OperationCanceledException)
                {
                    TryDelete(saida);   // half-written snippet
                    throw;
                }

                if (File.Exists(saida) && new FileInfo(saida).Length > MinValidBytes)
                {
                    criados++;
                    evento = new SnippetEvent(fonte, saida, SnippetOutcome.Created, string.Create(inv, $"Created in {t0.Elapsed.TotalSeconds:F1} s"));
                }
                else
                {
                    // Nothing, or almost nothing, was written: the track ends before the cut point.
                    TryDelete(saida);
                    falhas++;
                    string motivo = erros.LastOrDefault(l => !string.IsNullOrWhiteSpace(l))?.Trim()
                                    ?? $"Track is shorter than the start point ({options.StartSeconds} s)";
                    evento = new SnippetEvent(fonte, saida, SnippetOutcome.Failed, motivo);
                }
            }

            feitos++;
            progress?.Report(new SnippetProgress(feitos, total, Path.GetFileName(fonte), evento));
        }

        return new SnippetSummary(criados, plano.AlreadyExisted, falhas, relogio.Elapsed, options.DryRun);
    }

    // ------------------------------------------------------------ Rules --

    /// <summary>The folder itself, then (if recursive) every subfolder that is not a snippet or backup folder.</summary>
    private static IEnumerable<string> SourceFolders(string raiz, bool recursive)
    {
        yield return raiz;
        if (!recursive) yield break;

        var opcoes = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.System };
        var subs = Directory.EnumerateDirectories(raiz, "*", opcoes).Where(d => !IsIgnored(raiz, d)).ToList();
        subs.Sort(StringComparer.OrdinalIgnoreCase);
        foreach (string s in subs) yield return s;
    }

    private static bool IsIgnored(string raiz, string pasta)
    {
        // Only the part below the chosen folder counts, as in the original script.
        string relativo = Path.GetRelativePath(raiz, pasta);
        string[] partes = relativo.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (partes.Any(p => SnippetFolderNames.Contains(p, StringComparer.OrdinalIgnoreCase))) return true;
        return partes[^1].Contains("BACKUP", Ci);
    }

    private static IEnumerable<string> Tracks(string pasta)
    {
        IEnumerable<string> arquivos;
        try { arquivos = Directory.EnumerateFiles(pasta).ToList(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { yield break; }

        foreach (string f in arquivos.OrderBy(a => a, StringComparer.OrdinalIgnoreCase))
        {
            if (!Extensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase)) continue;
            string nome = Path.GetFileNameWithoutExtension(f);
            if (SnippetSuffixes.Any(s => nome.Contains(s, Ci))) continue;   // already a snippet
            yield return f;
        }
    }

    /// <summary>Removes any snippet suffix, so "Track [PREVIEW]" and "Track" compare equal.</summary>
    private static string StripSuffixes(string nome)
    {
        foreach (string s in SnippetSuffixes)
            nome = nome.Replace(" " + s, "", Ci).Replace(s, "", Ci);
        return nome.Trim();
    }

    /// <summary>True if any snippet folder next to the track holds a file for it, under any known suffix.</summary>
    private static bool HasSnippet(string pasta, string nomeFaixa)
    {
        string limpo = StripSuffixes(nomeFaixa);
        foreach (string nomePasta in SnippetFolderNames.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            string dir = Path.Combine(pasta, nomePasta);
            if (!Directory.Exists(dir)) continue;
            try
            {
                if (Directory.EnumerateFiles(dir).Any(f => StripSuffixes(Path.GetFileNameWithoutExtension(f)).Equals(limpo, Ci)))
                    return true;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
        return false;
    }

    private static void TryDelete(string caminho)
    {
        try { if (File.Exists(caminho)) File.Delete(caminho); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }
}
