using System.Diagnostics;
using System.Text;

namespace KhzAnalyzer.Core.Media;

/// <summary>Finds the external programs (FFmpeg, yt-dlp) and runs them without a console window.</summary>
public static class MediaTools
{
    public static string? Ffmpeg => Find("ffmpeg.exe");
    public static string? YtDlp => Find("yt-dlp.exe");

    /// <summary>
    /// Looks in the "tools" folder shipped next to the app first, then in PATH.
    /// The shipped copy wins so that every install behaves the same.
    /// </summary>
    public static string? Find(string exeName)
    {
        string local = Path.Combine(AppContext.BaseDirectory, "tools", exeName);
        if (File.Exists(local)) return local;

        string path = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (string dir in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                string candidato = Path.Combine(dir.Trim().Trim('"'), exeName);
                if (File.Exists(candidato)) return candidato;
            }
            catch (ArgumentException) { /* malformed PATH entry */ }
        }
        return null;
    }

    /// <summary>
    /// Runs a program and reports each output line. Cancelling kills the whole process tree
    /// (yt-dlp starts FFmpeg as a child).
    /// </summary>
    public static async Task<int> RunAsync(
        string exe, IEnumerable<string> args,
        Action<string>? onOutput, Action<string>? onError, CancellationToken token)
    {
        var psi = new ProcessStartInfo(exe)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        foreach (string a in args) psi.ArgumentList.Add(a);

        // yt-dlp looks for its helpers (deno, ffmpeg) in PATH: put the program's own folder first.
        string? pastaExe = Path.GetDirectoryName(exe);
        if (!string.IsNullOrEmpty(pastaExe))
            psi.Environment["PATH"] = pastaExe + Path.PathSeparator + (Environment.GetEnvironmentVariable("PATH") ?? "");

        using var p = new Process { StartInfo = psi };
        p.OutputDataReceived += (_, e) => { if (e.Data is not null) onOutput?.Invoke(e.Data); };
        p.ErrorDataReceived += (_, e) => { if (e.Data is not null) onError?.Invoke(e.Data); };

        p.Start();
        p.BeginOutputReadLine();
        p.BeginErrorReadLine();

        using var registro = token.Register(() =>
        {
            try { if (!p.HasExited) p.Kill(entireProcessTree: true); }
            catch (InvalidOperationException) { /* already gone */ }
        });

        await p.WaitForExitAsync(CancellationToken.None);
        token.ThrowIfCancellationRequested();
        return p.ExitCode;
    }
}
